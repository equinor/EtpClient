using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EtpClient.UnitTests.Connection;

public sealed class EtpSessionManagerTests
{
    private static readonly EtpConnectionOptions DefaultOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass");

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a substitute transport that simulates a successful session handshake:
    /// ConnectAsync succeeds, the first ReceiveAsync returns <paramref name="responseFrame"/>.
    /// </summary>
    private static IWebSocketTransport BuildSuccessTransport(ReadOnlyMemory<byte> responseFrame)
    {
        var transport = Substitute.For<IWebSocketTransport>();
        transport.State.Returns(WebSocketState.Open);
        transport.HttpStatusCode.Returns((int?)null);

        transport
            .ConnectAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.CompletedTask);

        transport
            .SendAsync(default, default, default, default)
            .ReturnsForAnyArgs(ValueTask.CompletedTask);

        // Copy frame into the receive buffer and return a completed result
        transport
            .ReceiveAsync(default, default)
            .ReturnsForAnyArgs(ci =>
            {
                var buffer = (Memory<byte>)ci[0];
                responseFrame.Span.CopyTo(buffer.Span);
                return new ValueTask<ValueWebSocketReceiveResult>(
                    new ValueWebSocketReceiveResult(responseFrame.Length, WebSocketMessageType.Binary, true));
            });

        return transport;
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var instanceId = Guid.NewGuid();
        var w = new AvroWriter();
        // Header: protocol=0, messageType=2, correlationId=1, messageId=2, flags=FinalPart
        w.WriteInt(0); w.WriteInt(2); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(2);
        // applicationName
        w.WriteString("TestServer");
        // applicationVersion
        w.WriteString("1.0");
        // sessionId (UUID string)
        w.WriteString(instanceId.ToString());
        // supportedProtocols — empty array
        w.WriteArrayStart(0);
        // supportedObjects — empty array
        w.WriteArrayStart(0);

        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildProtocolExceptionFrame()
    {
        var w = new AvroWriter();
        // Header: protocol=0, messageType=1000, correlationId=1, messageId=2, flags=FinalPart
        w.WriteInt(0); w.WriteInt(1000); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(2);
        // errorCode (int), message (string)
        w.WriteInt(14);
        w.WriteString("Unsupported protocol");
        return w.ToArray();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsClosed()
    {
        var transport = Substitute.For<IWebSocketTransport>();
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        Assert.Equal(EtpConnectionState.Closed, manager.State);
    }

    [Fact]
    public async Task ConnectAsync_ValidOptions_TransitionsToConnected()
    {
        var frame = BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(frame);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var result = await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        Assert.Equal(EtpConnectionState.Connected, manager.State);
        Assert.NotNull(result);
        Assert.Equal("TestServer", result.Session.ServerApplicationName);
    }

    [Fact]
    public async Task ConnectAsync_ServerSendsProtocolException_ThrowsAndTransitionsToFailed()
    {
        var frame = BuildProtocolExceptionFrame();
        var transport = BuildSuccessTransport(frame);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(DefaultOptions, CancellationToken.None));

        Assert.Equal(EtpConnectionFailureCategory.Protocol, ex.Category);
        Assert.Equal(EtpConnectionState.Failed, manager.State);
    }

    [Fact]
    public async Task ConnectAsync_WebSocketThrows_TransitionsToFailed()
    {
        var transport = Substitute.For<IWebSocketTransport>();
        transport.HttpStatusCode.Returns((int?)null);
        transport
            .ConnectAsync(default!, default!, default, default)
            .ThrowsForAnyArgs(new WebSocketException("connection refused"));

        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(DefaultOptions, CancellationToken.None));

        Assert.Equal(EtpConnectionFailureCategory.Transport, ex.Category);
        Assert.Equal(EtpConnectionState.Failed, manager.State);
    }

    [Fact]
    public async Task ConnectAsync_Http401_ThrowsAuthentication()
    {
        var transport = Substitute.For<IWebSocketTransport>();
        transport.HttpStatusCode.Returns((int?)401);

        // Simulate upgrade failure via WebSocketException; HttpStatusCode reflects the 401
        transport
            .ConnectAsync(default!, default!, default, default)
            .ThrowsForAnyArgs(new WebSocketException(WebSocketError.NotAWebSocket, "401 Unauthorized"));

        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(DefaultOptions, CancellationToken.None));

        Assert.Equal(EtpConnectionFailureCategory.Authentication, ex.Category);
        Assert.Equal(401, ex.HttpStatusCode);
    }

    [Fact]
    public async Task ConnectAsync_Cancelled_TransitionsToCanceled()
    {
        using var cts = new CancellationTokenSource();
        var transport = Substitute.For<IWebSocketTransport>();
        transport.HttpStatusCode.Returns((int?)null);
        transport
            .ConnectAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(ci =>
            {
                cts.Cancel();
                return Task.FromCanceled(cts.Token);
            });

        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.ConnectAsync(DefaultOptions, cts.Token));

        Assert.Equal(EtpConnectionState.Canceled, manager.State);
    }

    [Fact]
    public async Task CloseAsync_AfterConnect_TransitionsToClosed()
    {
        var frame = BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(frame);
        transport
            .CloseOutputAsync(default, default, default)
            .ReturnsForAnyArgs(Task.CompletedTask);

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        await manager.CloseAsync(CancellationToken.None);

        Assert.Equal(EtpConnectionState.Closed, manager.State);
    }

    [Fact]
    public async Task ConnectAsync_NullOptions_ThrowsArgumentNullException()
    {
        var transport = Substitute.For<IWebSocketTransport>();
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => manager.ConnectAsync(null!, CancellationToken.None));
    }
}
