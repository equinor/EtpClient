using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using EtpClient.UnitTests.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EtpClient.UnitTests.Connection;

/// <summary>
/// Unit tests for encoding consistency in <see cref="EtpSessionManager"/>.
/// T027 [US3]: Validates that the session uses one encoding throughout and
/// that the result reports the same encoding as the options.
/// </summary>
public sealed class EtpSessionManagerEncodingConsistencyTests
{
    private static readonly EtpConnectionOptions BinaryOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass",
            messageEncoding: EtpMessageEncoding.Binary);

    private static readonly EtpConnectionOptions JsonOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass",
            messageEncoding: EtpMessageEncoding.Json);

    // ── Result reports selected encoding on success ───────────────────────────

    [Fact]
    public async Task ConnectAsync_BinaryOptions_ResultReportsBinaryEncoding()
    {
        var openFrame = BinarySessionCodecTests.BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(openFrame, WebSocketMessageType.Binary);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var result = await manager.ConnectAsync(BinaryOptions, CancellationToken.None);

        Assert.Equal(EtpMessageEncoding.Binary, result.MessageEncoding);
    }

    [Fact]
    public async Task ConnectAsync_JsonOptions_ResultReportsJsonEncoding()
    {
        var openFrame = JsonSessionCodecTests.BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(openFrame, WebSocketMessageType.Text);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var result = await manager.ConnectAsync(JsonOptions, CancellationToken.None);

        Assert.Equal(EtpMessageEncoding.Json, result.MessageEncoding);
    }

    // ── Transport used the correct frame type ─────────────────────────────────

    [Fact]
    public async Task ConnectAsync_BinaryOptions_SendsOnBinaryFrame()
    {
        var openFrame = BinarySessionCodecTests.BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(openFrame, WebSocketMessageType.Binary);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await manager.ConnectAsync(BinaryOptions, CancellationToken.None);

        // Verify that SendAsync was called with Binary frame type
        await transport.Received().SendAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            WebSocketMessageType.Binary,
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectAsync_JsonOptions_SendsOnTextFrame()
    {
        var openFrame = JsonSessionCodecTests.BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(openFrame, WebSocketMessageType.Text);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await manager.ConnectAsync(JsonOptions, CancellationToken.None);

        // Verify that SendAsync was called with Text frame type
        await transport.Received().SendAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            WebSocketMessageType.Text,
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IWebSocketTransport BuildSuccessTransport(
        ReadOnlyMemory<byte> responseFrame,
        WebSocketMessageType frameType)
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
        transport
            .ReceiveAsync(default, default)
            .ReturnsForAnyArgs(ci =>
            {
                var buffer = (Memory<byte>)ci[0];
                responseFrame.Span.CopyTo(buffer.Span);
                return new ValueTask<ValueWebSocketReceiveResult>(
                    new ValueWebSocketReceiveResult(responseFrame.Length, frameType, true));
            });
        return transport;
    }
}
