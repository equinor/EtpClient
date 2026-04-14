using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EtpClient.UnitTests.Connection;

/// <summary>
/// Unit tests for EtpSessionManager Protocol 1 orchestration:
/// StartChannelStreamingAsync (live events, ChannelRemove, ProtocolException),
/// StopChannelStreamingAsync (sends ChannelStreamingStop, session stays Connected),
/// RequestChannelRangeAsync (single / multipart / ProtocolException / reconnect-incomplete).
/// Referenced by T017 [US2], T025 [US3].
/// </summary>
public sealed class EtpSessionManagerChannelStreamingTests
{
    private static readonly EtpConnectionOptions DefaultOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass");

    // ── T017 [US2]: StartChannelStreamingAsync – live events ─────────────────

    [Fact]
    public async Task StartChannelStreamingAsync_ServerSendsChannelData_YieldsDataEvent()
    {
        var dataFrame = BuildChannelDataFrame(
            channelId: 1L, indexValue: 1000L, doubleValue: 3.14,
            correlationId: 0L, messageId: 3L, finalPart: true);

        var stopFrame = BuildChannelRemoveFrame(channelId: 1L, reason: null, messageId: 4L);
        var transport = BuildHandshakeAndFrameTransport([dataFrame, stopFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var subscriptions = new[] { new ChannelSubscriptionInfo(1L, startLatest: true, receiveChangeNotifications: false) };
        var events = new List<ChannelEvent>();
        await foreach (var ev in manager.StartChannelStreamingAsync(subscriptions, CancellationToken.None))
            events.Add(ev);

        // Expect Data event + Remove event (stream ends on Remove)
        Assert.Equal(2, events.Count);
        Assert.Equal(ChannelEventKind.Data, events[0].Kind);
        Assert.Single(events[0].DataItems);
        Assert.Equal(1L, events[0].DataItems[0].ChannelId);
    }

    [Fact]
    public async Task DescribeChannelsAsync_SendsProtocol1StartBeforeChannelDescribe()
    {
        var protocolExceptionFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 1003, message: "still testing");
        var transport = BuildHandshakeAndFrameTransport([protocolExceptionFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var sentFrames = new List<ReadOnlyMemory<byte>>();
        transport.SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ci =>
            {
                sentFrames.Add((ReadOnlyMemory<byte>)ci[0]);
                return ValueTask.CompletedTask;
            });

        await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => manager.DescribeChannelsAsync(["eml://witsml14/logcurveinfo(RPM)"], CancellationToken.None));

        Assert.Equal(2, sentFrames.Count);
        Assert.Equal(EtpChannelStreamingMessageType.Start, new BinaryEtpSessionCodec().DecodeHeader(sentFrames[0]).MessageType);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelDescribe, new BinaryEtpSessionCodec().DecodeHeader(sentFrames[1]).MessageType);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_ServerSendsChannelRemove_YieldsRemoveEventAndCompletes()
    {
        var removeFrame = BuildChannelRemoveFrame(channelId: 2L, reason: "Server shutdown", messageId: 3L);
        var transport = BuildHandshakeAndFrameTransport([removeFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var subscriptions = new[] { new ChannelSubscriptionInfo(2L, startLatest: true, receiveChangeNotifications: false) };
        var events = new List<ChannelEvent>();
        await foreach (var ev in manager.StartChannelStreamingAsync(subscriptions, CancellationToken.None))
            events.Add(ev);

        Assert.Single(events);
        Assert.Equal(ChannelEventKind.Remove, events[0].Kind);
        Assert.Equal(2L, events[0].ChannelId);
        Assert.Equal("Server shutdown", events[0].RemoveReason);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_MultiSubscription_ContinuesAfterPartialRemove()
    {
        // Two subscriptions: server removes channel 10 first, then channel 11.
        // The stream should continue after the first Remove and only complete once both are removed.
        var dataFrame = BuildChannelDataFrame(
            channelId: 11L, indexValue: 1000L, doubleValue: 1.5,
            correlationId: 0L, messageId: 3L, finalPart: true);
        var removeFrame1 = BuildChannelRemoveFrame(channelId: 10L, reason: "Partial stop", messageId: 4L);
        var removeFrame2 = BuildChannelRemoveFrame(channelId: 11L, reason: "Final stop", messageId: 5L);
        var transport = BuildHandshakeAndFrameTransport([dataFrame, removeFrame1, removeFrame2]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var subscriptions = new[]
        {
            new ChannelSubscriptionInfo(10L, startLatest: true, receiveChangeNotifications: false),
            new ChannelSubscriptionInfo(11L, startLatest: true, receiveChangeNotifications: false),
        };
        var events = new List<ChannelEvent>();
        await foreach (var ev in manager.StartChannelStreamingAsync(subscriptions, CancellationToken.None))
            events.Add(ev);

        // Data, Remove(10), Remove(11) — should not stop early after Remove(10)
        Assert.Equal(3, events.Count);
        Assert.Equal(ChannelEventKind.Data, events[0].Kind);
        Assert.Equal(ChannelEventKind.Remove, events[1].Kind);
        Assert.Equal(10L, events[1].ChannelId);
        Assert.Equal(ChannelEventKind.Remove, events[2].Kind);
        Assert.Equal(11L, events[2].ChannelId);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_ServerSendsStatusChange_YieldsStatusChangeEvent()
    {
        var changeFrame = BuildChannelStatusChangeFrame(channelId: 3L, statusIndex: 1 /* Inactive */, messageId: 3L);
        var removeFrame = BuildChannelRemoveFrame(channelId: 3L, reason: null, messageId: 4L);
        var transport = BuildHandshakeAndFrameTransport([changeFrame, removeFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var subscriptions = new[] { new ChannelSubscriptionInfo(3L, startLatest: true, receiveChangeNotifications: true) };
        var events = new List<ChannelEvent>();
        await foreach (var ev in manager.StartChannelStreamingAsync(subscriptions, CancellationToken.None))
            events.Add(ev);

        var statusEvent = events.FirstOrDefault(e => e.Kind == ChannelEventKind.StatusChange);
        Assert.NotNull(statusEvent);
        Assert.Equal(3L, statusEvent.ChannelId);
        Assert.Equal("Inactive", statusEvent.NewStatus);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_ProtocolException_ThrowsEtpChannelStreamingException()
    {
        var exFrame = BuildProtocolExceptionFrame(correlationId: 0L, errorCode: 4, message: "Not authorized");
        var transport = BuildHandshakeAndFrameTransport([exFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var subscriptions = new[] { new ChannelSubscriptionInfo(1L, startLatest: true, receiveChangeNotifications: false) };

        var ex = await Assert.ThrowsAsync<EtpChannelStreamingException>(async () =>
        {
            await foreach (var _ in manager.StartChannelStreamingAsync(subscriptions, CancellationToken.None)) { }
        });

        Assert.Equal(4, ex.EtpErrorCode);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_NotConnected_ThrowsInvalidOperationException()
    {
        var transport = Substitute.For<IWebSocketTransport>();
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in manager.StartChannelStreamingAsync([], CancellationToken.None)) { }
        });
    }

    // ── T017 [US2]: StopChannelStreamingAsync ────────────────────────────────

    [Fact]
    public async Task StopChannelStreamingAsync_SendsChannelStreamingStopFrame()
    {
        var transport = BuildHandshakeTransport();
        ReadOnlyMemory<byte> sentFrame = default;
        transport.SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ci =>
            {
                sentFrame = (ReadOnlyMemory<byte>)ci[0];
                return ValueTask.CompletedTask;
            });

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        // Reset capture (first send was RequestSession, 2nd was part of the open)
        var capturedFrames = new List<ReadOnlyMemory<byte>>();
        transport.SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ci =>
            {
                capturedFrames.Add((ReadOnlyMemory<byte>)ci[0]);
                return ValueTask.CompletedTask;
            });

        await manager.StopChannelStreamingAsync([1L, 2L], CancellationToken.None);

        Assert.Single(capturedFrames);
        var header = new BinaryEtpSessionCodec().DecodeHeader(capturedFrames[0]);
        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelStreamingStop, header.MessageType);
    }

    [Fact]
    public async Task StopChannelStreamingAsync_SessionRemainsConnectedAfterStop()
    {
        var transport = BuildHandshakeTransport();
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        await manager.StopChannelStreamingAsync([1L], CancellationToken.None);

        Assert.Equal(EtpConnectionState.Connected, manager.State);
    }

    [Fact]
    public async Task RequestChannelRangeAsync_AfterDescribe_DoesNotSendSecondProtocol1Start()
    {
        var describeFailureFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 1003, message: "describe done");
        var rangeFailureFrame = BuildProtocolExceptionFrame(correlationId: 4L, errorCode: 1003, message: "range done");
        var transport = BuildHandshakeAndFrameTransport([describeFailureFrame, rangeFailureFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var sentFrames = new List<ReadOnlyMemory<byte>>();
        transport.SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ci =>
            {
                sentFrames.Add((ReadOnlyMemory<byte>)ci[0]);
                return ValueTask.CompletedTask;
            });

        await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => manager.DescribeChannelsAsync(["eml://witsml14/logcurveinfo(RPM)"], CancellationToken.None));

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 1L,
            ToIndex = 2L,
        };

        await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => manager.RequestChannelRangeAsync(request, CancellationToken.None));

        var headers = sentFrames.Select(frame => new BinaryEtpSessionCodec().DecodeHeader(frame)).ToList();
        Assert.Equal(3, headers.Count);
        Assert.Equal(EtpChannelStreamingMessageType.Start, headers[0].MessageType);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelDescribe, headers[1].MessageType);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelRangeRequest, headers[2].MessageType);
        Assert.Single(headers, header => header.MessageType == EtpChannelStreamingMessageType.Start);
    }

    // ── T025 [US3]: RequestChannelRangeAsync ─────────────────────────────────

    [Fact]
    public async Task RequestChannelRangeAsync_ServerReturnsSinglePartResponse_ReturnsDataItems()
    {
        var dataFrame = BuildChannelDataFrame(
            channelId: 1L, indexValue: 1500L, doubleValue: 77.7,
            correlationId: 2L, messageId: 3L, finalPart: true);
        var transport = BuildHandshakeAndFrameTransport([dataFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 1000L,
            ToIndex = 2000L,
        };
        var result = await manager.RequestChannelRangeAsync(request, CancellationToken.None);

        Assert.Equal(ChannelRangeResultState.Completed, result.State);
        Assert.Single(result.Samples);
        Assert.Equal(1L, result.Samples[0].ChannelId);
        Assert.False(result.WasMultipart);
    }

    [Fact]
    public async Task RequestChannelRangeAsync_MultipartResponse_AggregatesAllSamples()
    {
        var frame1 = BuildChannelDataFrame(
            channelId: 1L, indexValue: 1000L, doubleValue: 10.0,
            correlationId: 2L, messageId: 3L, finalPart: false);
        var frame2 = BuildChannelDataFrame(
            channelId: 1L, indexValue: 1500L, doubleValue: 15.0,
            correlationId: 2L, messageId: 4L, finalPart: false);
        var frame3 = BuildChannelDataFrame(
            channelId: 1L, indexValue: 2000L, doubleValue: 20.0,
            correlationId: 2L, messageId: 5L, finalPart: true);
        var transport = BuildHandshakeAndFrameTransport([frame1, frame2, frame3]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 1000L,
            ToIndex = 2000L,
        };
        var result = await manager.RequestChannelRangeAsync(request, CancellationToken.None);

        Assert.Equal(ChannelRangeResultState.Completed, result.State);
        Assert.Equal(3, result.Samples.Count);
        Assert.True(result.WasMultipart);
    }

    [Fact]
    public async Task RequestChannelRangeAsync_ProtocolException_ThrowsEtpChannelStreamingException()
    {
        var exFrame = BuildProtocolExceptionFrame(correlationId: 2L, errorCode: 7, message: "Invalid range");
        var transport = BuildHandshakeAndFrameTransport([exFrame]);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 100L,
            ToIndex = 50L, // invalid
        };
        var ex = await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => manager.RequestChannelRangeAsync(request, CancellationToken.None));

        Assert.Equal(7, ex.EtpErrorCode);
    }

    [Fact]
    public async Task RequestChannelRangeAsync_NotConnected_ThrowsInvalidOperationException()
    {
        var transport = Substitute.For<IWebSocketTransport>();
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 1000L,
            ToIndex = 2000L,
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RequestChannelRangeAsync(request, CancellationToken.None));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IWebSocketTransport BuildHandshakeTransport()
    {
        var openSessionFrame = BuildOpenSessionFrame();
        var transport = Substitute.For<IWebSocketTransport>();
        transport.State.Returns(WebSocketState.Open);
        transport.HttpStatusCode.Returns((int?)null);
        transport.ConnectAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        transport.SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ValueTask.CompletedTask);

        var called = false;
        transport.ReceiveAsync(default, default)
            .ReturnsForAnyArgs(ci =>
            {
                if (called) return new ValueTask<ValueWebSocketReceiveResult>(
                    new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));
                called = true;
                var buffer = (Memory<byte>)ci[0];
                openSessionFrame.Span.CopyTo(buffer.Span);
                return new ValueTask<ValueWebSocketReceiveResult>(
                    new ValueWebSocketReceiveResult(openSessionFrame.Length, WebSocketMessageType.Binary, true));
            });

        return transport;
    }

    private static IWebSocketTransport BuildHandshakeAndFrameTransport(
        IReadOnlyList<ReadOnlyMemory<byte>> frames)
    {
        var openSessionFrame = BuildOpenSessionFrame();
        var receiveQueue = new Queue<ReadOnlyMemory<byte>>();
        receiveQueue.Enqueue(openSessionFrame);
        foreach (var f in frames)
            receiveQueue.Enqueue(f);

        var transport = Substitute.For<IWebSocketTransport>();
        transport.State.Returns(WebSocketState.Open);
        transport.HttpStatusCode.Returns((int?)null);
        transport.ConnectAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        transport.SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ValueTask.CompletedTask);
        transport.ReceiveAsync(default, default)
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
        // protocols: Protocol 1 (ChannelStreaming) as producer
        w.WriteArrayStart(1);
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(ProtocolVersion.Etp11.Major);
        w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision);
        w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("producer");
        w.WriteMapStart(0);
        w.WriteArrayEnd();
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildChannelDataFrame(
        long channelId, long indexValue, double doubleValue,
        long correlationId, long messageId, bool finalPart)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelData);
        w.WriteLong(correlationId);
        w.WriteLong(messageId);
        w.WriteInt(finalPart ? EtpMessageFlags.FinalPart : 0);

        // data: array<DataItem> — one item
        w.WriteArrayStart(1);
        w.WriteArrayStart(1); w.WriteLong(indexValue); w.WriteArrayEnd(); // indexes
        w.WriteLong(channelId); // channelId
        w.WriteLong(1L); w.WriteDouble(doubleValue); // value: double (index 1)
        w.WriteArrayEnd(); // valueAttributes: empty
        w.WriteArrayEnd();
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildChannelRemoveFrame(long channelId, string? reason, long messageId)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelRemove);
        w.WriteLong(0L); w.WriteLong(messageId); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteLong(channelId);
        if (reason is null) w.WriteLong(0L);
        else { w.WriteLong(1L); w.WriteString(reason); }
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildChannelStatusChangeFrame(long channelId, int statusIndex, long messageId)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelStatusChange);
        w.WriteLong(0L); w.WriteLong(messageId); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteLong(channelId);
        w.WriteLong(statusIndex); // status enum
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

