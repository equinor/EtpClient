using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EtpClient.UnitTests.Connection;

/// <summary>
/// Unit tests for <see cref="EtpClient.DiscoverResourcesAsync"/> and
/// <see cref="EtpSessionManager.DiscoverResourcesAsync"/> state-guard behaviour.
/// T003 [US1]: Validates that discovery is rejected when no session is active.
/// </summary>
public sealed class DiscoverResourcesStateTests
{
    // ── EtpClient: state guard ────────────────────────────────────────────────

    [Fact]
    public async Task EtpClient_DiscoverResourcesAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange: fresh client — never connected
        var transport = Substitute.For<IWebSocketTransport>();
        await using var client = new global::EtpClient.EtpClient(
            () => transport, NullLogger.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.DiscoverResourcesAsync("eml://"));
    }

    [Fact]
    public async Task EtpClient_DiscoverResourcesAsync_AfterClose_ThrowsInvalidOperationException()
    {
        // Arrange: connect then close
        var openFrame = BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(openFrame);

        await using var client = new global::EtpClient.EtpClient(
            () => transport, NullLogger.Instance);

        var options = new EtpConnectionOptions(new Uri("wss://example.com/etp"), "user", "pass");
        await client.ConnectAsync(options);
        await client.CloseAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.DiscoverResourcesAsync("eml://"));
    }

    [Fact]
    public async Task EtpClient_DiscoverResourcesAsync_WhenDiscoveryNotNegotiated_ThrowsEtpDiscoveryException()
    {
        var openFrame = BuildOpenSessionFrame();
        var transport = BuildSuccessTransport(openFrame);

        await using var client = new global::EtpClient.EtpClient(
            () => transport, NullLogger.Instance);

        var options = new EtpConnectionOptions(new Uri("wss://example.com/etp"), "user", "pass");
        await client.ConnectAsync(options);

        var ex = await Assert.ThrowsAsync<EtpDiscoveryException>(
            () => client.DiscoverResourcesAsync("eml://"));

        Assert.Equal("eml://", ex.RequestedUri);
        Assert.Contains("not negotiated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

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
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("TestServer");
        w.WriteString("1.0");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(0);
        w.WriteArrayStart(0);
        return w.ToArray();
    }
}
