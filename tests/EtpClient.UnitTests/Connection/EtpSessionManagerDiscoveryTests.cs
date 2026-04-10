using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EtpClient.UnitTests.Connection;

/// <summary>
/// Unit tests for <see cref="EtpSessionManager.DiscoverResourcesAsync"/>:
/// multipart aggregation, Acknowledge-as-empty, ProtocolException mapping, and
/// message-ID sequencing.
/// T017 [US2]: Validates discovery orchestration through a mock transport.
/// </summary>
public sealed class EtpSessionManagerDiscoveryTests
{
    private static readonly EtpConnectionOptions DefaultOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass");

    // ── happy path: single resource with FinalPart ────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_SingleResourceFinalPart_ReturnsOneResource()
    {
        var resource = CreateSampleResource("eml://witsml20");
        var responseFrame = BuildResponseFrame(resource, correlationId: 2L, messageId: 3L, finalPart: true);
        var transport = BuildHandshakeAndDiscoveryTransport(responseFrame);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var result = await manager.DiscoverResourcesAsync("eml://", CancellationToken.None);

        Assert.Single(result.Resources);
        Assert.Equal("eml://witsml20", result.Resources[0].Uri);
        Assert.False(result.WasEmptyAcknowledged);
        Assert.Equal(DiscoveryResultState.CompletedWithResources, result.State);
    }

    // ── multipart: FinalPart only on last message ─────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_TwoResources_AccumulatesBothBeforeFinalPart()
    {
        var res1 = CreateSampleResource("eml://witsml20");
        var res2 = CreateSampleResource("eml://eml21");
        var frame1 = BuildResponseFrame(res1, correlationId: 2L, messageId: 3L, finalPart: false);
        var frame2 = BuildResponseFrame(res2, correlationId: 2L, messageId: 4L, finalPart: true);
        var transport = BuildHandshakeAndMultiframeTransport([frame1, frame2]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var result = await manager.DiscoverResourcesAsync("eml://", CancellationToken.None);

        Assert.Equal(2, result.Resources.Count);
        Assert.Equal("eml://witsml20", result.Resources[0].Uri);
        Assert.Equal("eml://eml21", result.Resources[1].Uri);
    }

    // ── Acknowledge path: empty result ────────────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_ServerAcknowledges_ReturnsEmpty()
    {
        var ackFrame = BuildAcknowledgeFrame(correlationId: 2L);
        var transport = BuildHandshakeAndDiscoveryTransport(ackFrame);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var result = await manager.DiscoverResourcesAsync("eml://leaf", CancellationToken.None);

        Assert.Empty(result.Resources);
        Assert.True(result.WasEmptyAcknowledged);
        Assert.Equal(DiscoveryResultState.CompletedEmpty, result.State);
    }

    // ── ProtocolException path ────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_ProtocolException_ThrowsEtpDiscoveryException()
    {
        var exFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 4, message: "Invalid URI");
        var transport = BuildHandshakeAndDiscoveryTransport(exFrame);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<EtpDiscoveryException>(
            () => manager.DiscoverResourcesAsync("eml://bad", CancellationToken.None));

        Assert.Equal(4, ex.EtpErrorCode);
        Assert.Equal("eml://bad", ex.RequestedUri);
    }

    // ── encoding is propagated ────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_BinaryCodec_ResultEncodingIsBinary()
    {
        var resource = CreateSampleResource("eml://witsml20");
        var responseFrame = BuildResponseFrame(resource, correlationId: 2L, messageId: 3L, finalPart: true);
        var transport = BuildHandshakeAndDiscoveryTransport(responseFrame);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var result = await manager.DiscoverResourcesAsync("eml://", CancellationToken.None);

        Assert.Equal(EtpMessageEncoding.Binary, result.MessageEncoding);
    }

    [Fact]
    public async Task DiscoverResourcesAsync_UnrelatedFrameBeforeResponse_IsIgnored()
    {
        var unrelatedFrame = BuildUnrelatedFrame();
        var responseFrame = BuildResponseFrame(CreateSampleResource("eml://witsml20"), correlationId: 2L, messageId: 3L, finalPart: true);
        var transport = BuildHandshakeAndMultiframeTransport([unrelatedFrame, responseFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var result = await manager.DiscoverResourcesAsync("eml://", CancellationToken.None);

        Assert.Single(result.Resources);
        Assert.Equal("eml://witsml20", result.Resources[0].Uri);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static DiscoveredResource CreateSampleResource(string uri) =>
        new()
        {
            Uri = uri,
            ContentType = "application/x-witsml",
            Name = "Sample",
            ChannelSubscribable = false,
            CustomData = new Dictionary<string, string>(),
            ResourceType = "UriProtocol",
            HasChildren = 1,
            Uuid = null,
            LastChanged = 0L,
            ObjectNotifiable = false,
        };

    /// <summary>
    /// Builds a mock transport that completes the handshake (open session) then serves
    /// <paramref name="discoveryFrame"/> as the next received message.
    /// </summary>
    private static IWebSocketTransport BuildHandshakeAndDiscoveryTransport(
        ReadOnlyMemory<byte> discoveryFrame)
    {
        return BuildHandshakeAndMultiframeTransport([discoveryFrame]);
    }

    private static IWebSocketTransport BuildHandshakeAndMultiframeTransport(
        IReadOnlyList<ReadOnlyMemory<byte>> discoveryFrames)
    {
        var openSessionFrame = BuildOpenSessionFrame();
        var receiveQueue = new Queue<ReadOnlyMemory<byte>>();
        receiveQueue.Enqueue(openSessionFrame);
        foreach (var f in discoveryFrames)
            receiveQueue.Enqueue(f);

        var transport = Substitute.For<IWebSocketTransport>();
        transport.State.Returns(WebSocketState.Open);
        transport.HttpStatusCode.Returns((int?)null);

        transport
            .ConnectAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.CompletedTask);

        transport
            .SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ValueTask.CompletedTask);

        transport
            .ReceiveAsync(default, default)
            .ReturnsForAnyArgs(ci =>
            {
                var buffer = (Memory<byte>)ci[0];
                var frame = receiveQueue.Dequeue();
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
        w.WriteString("TestServer");
        w.WriteString("1.0");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(1);
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(ProtocolVersion.Etp11.Major);
        w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision);
        w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("store");
        w.WriteMapStart(0);
        w.WriteArrayEnd();
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildResponseFrame(
        DiscoveredResource resource, long correlationId, long messageId, bool finalPart)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(EtpDiscoveryMessageType.GetResourcesResponse);
        w.WriteLong(correlationId);
        w.WriteLong(messageId);
        w.WriteInt(finalPart ? EtpMessageFlags.FinalPart : 0);

        w.WriteString(resource.Uri);
        w.WriteString(resource.ContentType);
        w.WriteString(resource.Name);
        w.WriteBool(resource.ChannelSubscribable);
        w.WriteMapStart(0); // empty customData
        w.WriteString(resource.ResourceType);
        w.WriteInt(resource.HasChildren);
        w.WriteLong(0L); // null uuid discriminator
        w.WriteLong(resource.LastChanged);
        w.WriteBool(resource.ObjectNotifiable);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildUnrelatedFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(1);
        w.WriteInt(0);
        w.WriteLong(0L);
        w.WriteLong(99L);
        w.WriteInt(EtpMessageFlags.FinalPart);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildAcknowledgeFrame(long correlationId)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(EtpMessageType.Acknowledge);
        w.WriteLong(correlationId);
        w.WriteLong(3L);
        w.WriteInt(EtpMessageFlags.FinalPart);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildProtocolExceptionFrame(long correlationId, int errorCode, string message)
    {
        var w = new AvroWriter();
        w.WriteInt(0); // Core protocol
        w.WriteInt(EtpMessageType.ProtocolException);
        w.WriteLong(correlationId);
        w.WriteLong(3L);
        w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteInt(errorCode);
        w.WriteString(message);
        return w.ToArray();
    }
}
