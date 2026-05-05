using System.Diagnostics;
using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Instrumentation;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace EtpClient.UnitTests.Instrumentation;

[Collection("EtpInstrumentation")]
public sealed class ChannelOperationInstrumentationTests
{
    private static readonly EtpConnectionOptions DefaultOptions =
        new(new Uri("wss://example.com:443/etp"), "user", "pass");

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IWebSocketTransport BuildTransport(IReadOnlyList<ReadOnlyMemory<byte>> frames)
    {
        var openSessionFrame = BuildOpenSessionFrame();
        var queue = new Queue<ReadOnlyMemory<byte>>();
        queue.Enqueue(openSessionFrame);
        foreach (var f in frames) queue.Enqueue(f);

        var transport = Substitute.For<IWebSocketTransport>();
        transport.State.Returns(WebSocketState.Open);
        transport.HttpStatusCode.Returns((int?)null);
        transport.ConnectAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        transport.SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ValueTask.CompletedTask);
        transport.CloseOutputAsync(default, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        transport.ReceiveAsync(default, default).ReturnsForAnyArgs(ci =>
        {
            var buffer = (Memory<byte>)ci[0];
            var frame = queue.Dequeue();
            frame.Span.CopyTo(buffer.Span);
            return new ValueTask<ValueWebSocketReceiveResult>(
                new ValueWebSocketReceiveResult(frame.Length, WebSocketMessageType.Binary, true));
        });
        return transport;
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("TestServer"); w.WriteString("1.0");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(1);
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(ProtocolVersion.Etp11.Major); w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision); w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("producer");
        w.WriteMapStart(0);
        w.WriteArrayEnd();
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    /// <summary>Builds a minimal ChannelMetadata frame with one channel.</summary>
    private static ReadOnlyMemory<byte> BuildChannelMetadataFrame(long correlationId, string channelUri)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelMetadata);
        w.WriteLong(correlationId); w.WriteLong(correlationId + 1); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteArrayStart(1);
        // ChannelMetadataRecord fields
        w.WriteString(channelUri);          // channelUri
        w.WriteLong(1L);                    // channelId
        // indexes: 1 primary index
        w.WriteArrayStart(1);
        w.WriteInt(0);      // indexType=Time
        w.WriteString("ms"); // uom
        w.WriteLong(0L);    // depthDatum=null
        w.WriteInt(0);      // direction=Increasing
        w.WriteLong(0L);    // mnemonic=null
        w.WriteLong(0L);    // description=null
        w.WriteLong(0L);    // uri=null
        w.WriteMapEnd();    // customData=empty
        w.WriteInt(3);      // scale
        w.WriteLong(0L);    // timeDatum=null
        w.WriteArrayEnd();
        w.WriteString("TestChannel");  // channelName
        w.WriteString("double");       // dataType
        w.WriteString("rpm");          // uom
        w.WriteLong(0L);               // startIndex=null
        w.WriteLong(0L);               // endIndex=null
        w.WriteString("");             // description
        w.WriteInt(0);                 // status=Active
        w.WriteLong(0L);               // contentType=null
        w.WriteString("");             // source
        w.WriteString("");             // measureClass
        w.WriteLong(0L);               // uuid=null
        w.WriteMapEnd();               // customData=empty
        w.WriteLong(0L);               // domainObject=null
        w.WriteArrayEnd();
        return w.ToArray();
    }

    /// <summary>Builds a ChannelData frame suitable as a range-request response.</summary>
    private static ReadOnlyMemory<byte> BuildChannelDataFrame(long correlationId)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelData);
        w.WriteLong(correlationId); w.WriteLong(correlationId + 1); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteArrayStart(1);
        w.WriteArrayStart(1); w.WriteLong(1000L); w.WriteArrayEnd(); // indexes
        w.WriteLong(1L);      // channelId
        w.WriteLong(1L); w.WriteDouble(3.14); // value: double
        w.WriteArrayEnd();    // valueAttributes
        w.WriteArrayEnd();    // items
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildProtocolExceptionFrame(long correlationId, int errorCode)
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.ProtocolException);
        w.WriteLong(correlationId); w.WriteLong(correlationId + 1); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteInt(errorCode); w.WriteString("Operation failed");
        return w.ToArray();
    }

    // ── T015a: etp.channel.describe span with etp.channel_target and etp.channel_count ────

    [Fact]
    public async Task DescribeChannelsAsync_Success_ProducesDescribeSpanWithAttributes()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var uri = "eml://witsml14/well(abc)/log(L1)/channel(RPM)";
        var metadataFrame = BuildChannelMetadataFrame(correlationId: 2L, channelUri: uri);
        var transport = BuildTransport([metadataFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        await manager.DescribeChannelsAsync([uri], CancellationToken.None);
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.channel.describe");
        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(1, (int)span.GetTagItem("etp.channel_count")!);
        // etp.channel_target should be the first URI
        Assert.Equal(uri, span.GetTagItem("etp.channel_target") as string);
    }

    // ── T015b: etp.channel.range_request span with etp.channel_count ─────────

    [Fact]
    public async Task RequestChannelRangeAsync_Success_ProducesRangeRequestSpanWithChannelCount()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var dataFrame = BuildChannelDataFrame(correlationId: 2L);
        var transport = BuildTransport([dataFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L, 2L],
            FromIndex = 0L,
            ToIndex = 9999L,
        };
        await manager.RequestChannelRangeAsync(request, CancellationToken.None);
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.channel.range_request");
        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        // etp.channel_count reflects number of channels in the request
        Assert.Equal(2, (int)span.GetTagItem("etp.channel_count")!);
    }

    // ── T015c: Error status and etp.error_code on EtpChannelStreamingException ────

    [Fact]
    public async Task DescribeChannelsAsync_ProtocolException_ProducesErrorSpanWithErrorCode()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var exceptionFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 7);
        var transport = BuildTransport([exceptionFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => manager.DescribeChannelsAsync(["eml://witsml14/well(abc)"], CancellationToken.None));
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.channel.describe");
        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(7, (int)span.GetTagItem("etp.error_code")!);
    }

    [Fact]
    public async Task RequestChannelRangeAsync_ProtocolException_ProducesErrorSpanWithErrorCode()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var exceptionFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 7);
        var transport = BuildTransport([exceptionFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        var request = new ChannelRangeRequestModel { ChannelIds = [1L], FromIndex = 0L, ToIndex = 100L };
        await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => manager.RequestChannelRangeAsync(request, CancellationToken.None));
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.channel.range_request");
        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(7, (int)span.GetTagItem("etp.error_code")!);
    }

    // ── T015d: no spans when tracer not registered ────────────────────────────

    [Fact]
    public async Task DescribeChannelsAsync_WithoutTracerRegistration_ProducesNoSpans()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            // intentionally NOT calling AddEtpInstrumentation
            .AddInMemoryExporter(exported)
            .Build();

        var exceptionFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 4);
        var transport = BuildTransport([exceptionFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => manager.DescribeChannelsAsync(["eml://witsml14"], CancellationToken.None));
        provider.ForceFlush();

        Assert.Empty(exported);
    }
}
