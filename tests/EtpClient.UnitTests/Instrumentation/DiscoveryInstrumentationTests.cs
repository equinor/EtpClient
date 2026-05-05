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
public sealed class DiscoveryInstrumentationTests
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
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(ProtocolVersion.Etp11.Major); w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision); w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("store");
        w.WriteMapStart(0);
        w.WriteArrayEnd();
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildResourceResponseFrame(string uri, long correlationId)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(EtpDiscoveryMessageType.GetResourcesResponse);
        w.WriteLong(correlationId); w.WriteLong(correlationId + 1); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString(uri);
        w.WriteString("application/x-witsml+xml");
        w.WriteString("test"); w.WriteBool(false);
        w.WriteMapStart(0);
        w.WriteString("Channel"); w.WriteInt(-1);
        w.WriteLong(0L);
        w.WriteLong(0L); w.WriteBool(false);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildProtocolExceptionFrame(long correlationId, int errorCode)
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.ProtocolException);
        w.WriteLong(correlationId); w.WriteLong(correlationId + 1); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteInt(errorCode); w.WriteString("Discovery failed");
        return w.ToArray();
    }

    // ── T014a: etp.discovery span with etp.uri and etp.resource_count ────────

    [Fact]
    public async Task DiscoverResourcesAsync_Success_ProducesDiscoverySpanWithAttributes()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var responseFrame = BuildResourceResponseFrame("eml://witsml20", correlationId: 2L);
        var transport = BuildTransport([responseFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear(); // focus on the discovery span only

        await manager.DiscoverResourcesAsync("eml://witsml20", CancellationToken.None);
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.discovery");
        Assert.NotNull(span);
        Assert.Equal("eml://witsml20", span.GetTagItem("etp.uri") as string);
        Assert.Equal(1, (int)span.GetTagItem("etp.resource_count")!);
        Assert.Equal("example.com", span.GetTagItem("server.address") as string);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
    }

    // ── T014b: etp.uri truncated at 512 chars ────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_LongUri_TruncatesEtpUriAttribute()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var longUri = "eml://" + new string('x', 600);
        var responseFrame = BuildResourceResponseFrame(longUri, correlationId: 2L);
        var transport = BuildTransport([responseFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        await manager.DiscoverResourcesAsync(longUri, CancellationToken.None);
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.discovery");
        Assert.NotNull(span);
        var actualUri = span.GetTagItem("etp.uri") as string;
        Assert.NotNull(actualUri);
        Assert.True(actualUri.Length <= 512, $"Expected ≤512 chars, got {actualUri.Length}");
    }

    // ── T014c: Error status and etp.error_code on EtpDiscoveryException ──────

    [Fact]
    public async Task DiscoverResourcesAsync_ProtocolException_ProducesErrorSpanWithErrorCode()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var exceptionFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 14);
        var transport = BuildTransport([exceptionFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        await Assert.ThrowsAsync<EtpDiscoveryException>(
            () => manager.DiscoverResourcesAsync("eml://", CancellationToken.None));
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.discovery");
        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(14, (int)span.GetTagItem("etp.error_code")!);
    }

    // ── T014d/e: span parenting ───────────────────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_WithAmbientActivity_SpanIsChild()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddSource("test.parent")
            .AddInMemoryExporter(exported)
            .Build();

        using var parentSource = new ActivitySource("test.parent");
        var responseFrame = BuildResourceResponseFrame("eml://", correlationId: 2L);
        var transport = BuildTransport([responseFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        using var parent = parentSource.StartActivity("parent.op");
        Assert.NotNull(parent);
        await manager.DiscoverResourcesAsync("eml://", CancellationToken.None);
        provider.ForceFlush();

        var discoverySpan = exported.FirstOrDefault(a => a.OperationName == "etp.discovery");
        Assert.NotNull(discoverySpan);
        Assert.Equal(parent.SpanId, discoverySpan.ParentSpanId);
    }

    [Fact]
    public async Task DiscoverResourcesAsync_NoAmbientActivity_SpanIsRoot()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var responseFrame = BuildResourceResponseFrame("eml://", correlationId: 2L);
        var transport = BuildTransport([responseFrame]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        exported.Clear();

        // Ensure no ambient activity
        Activity.Current = null;
        await manager.DiscoverResourcesAsync("eml://", CancellationToken.None);
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.discovery");
        Assert.NotNull(span);
        Assert.Equal(default, span.ParentSpanId);
    }
}
