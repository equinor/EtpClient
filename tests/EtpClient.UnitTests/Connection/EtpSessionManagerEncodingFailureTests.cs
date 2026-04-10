using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EtpClient.UnitTests.Connection;

/// <summary>
/// Unit tests for encoding-related failure mapping in <see cref="EtpSessionManager"/>.
/// T020 [US2]: Validates that encoding mismatches produce observable, categorized failures.
/// </summary>
public sealed class EtpSessionManagerEncodingFailureTests
{
    private static readonly EtpConnectionOptions BinaryOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass",
            messageEncoding: EtpMessageEncoding.Binary);

    private static readonly EtpConnectionOptions JsonOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass",
            messageEncoding: EtpMessageEncoding.Json);

    // ── Encoding mismatch detection ────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_BinarySelected_ServerRespondsWithText_ThrowsProtocol()
    {
        // Server responds with a text (JSON) frame but client expects binary
        var transport = BuildTransportWithFrameType(WebSocketMessageType.Text);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(BinaryOptions, CancellationToken.None));

        Assert.Equal(EtpConnectionFailureCategory.Protocol, ex.Category);
        Assert.DoesNotContain("user", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pass", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectAsync_JsonSelected_ServerRespondsWithBinary_ThrowsProtocol()
    {
        // Server responds with a binary frame but client expects text (JSON)
        var transport = BuildTransportWithFrameType(WebSocketMessageType.Binary);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(JsonOptions, CancellationToken.None));

        Assert.Equal(EtpConnectionFailureCategory.Protocol, ex.Category);
        Assert.DoesNotContain("user", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pass", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Corrupted/invalid encoded data ───────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_JsonSelected_ServerSendsInvalidJson_ThrowsProtocol()
    {
        // Server sends text frame but with garbage content (not valid JSON)
        var garbage = System.Text.Encoding.UTF8.GetBytes("not-valid-json");
        var transport = BuildTransportWithFrame(garbage, WebSocketMessageType.Text);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(JsonOptions, CancellationToken.None));

        // Encoding decode failure maps to Protocol category
        Assert.Equal(EtpConnectionFailureCategory.Protocol, ex.Category);
    }

    // ── Transport failures are still categorized correctly ────────────────────

    [Fact]
    public async Task ConnectAsync_TransportError_StillCategorizedAsTransport()
    {
        var transport = Substitute.For<IWebSocketTransport>();
        transport.State.Returns(WebSocketState.None);
        transport.HttpStatusCode.Returns((int?)null);
        transport
            .ConnectAsync(default!, default!, default, default)
            .ThrowsForAnyArgs(new WebSocketException("connection refused"));

        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(BinaryOptions, CancellationToken.None));

        Assert.Equal(EtpConnectionFailureCategory.Transport, ex.Category);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a transport that responds with an empty frame of the given type.
    /// This simulates the mismatch scenario: server speaks the wrong frame type.
    /// </summary>
    private static IWebSocketTransport BuildTransportWithFrameType(WebSocketMessageType responseFrameType)
    {
        var emptyFrame = System.Text.Encoding.UTF8.GetBytes("{}");
        return BuildTransportWithFrame(emptyFrame, responseFrameType);
    }

    private static IWebSocketTransport BuildTransportWithFrame(
        byte[] responseData,
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
                var buf = (Memory<byte>)ci[0];
                responseData.AsSpan().CopyTo(buf.Span);
                return new ValueTask<ValueWebSocketReceiveResult>(
                    new ValueWebSocketReceiveResult(responseData.Length, frameType, true));
            });
        return transport;
    }
}
