using System.Diagnostics;
using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Instrumentation;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace EtpClient.UnitTests.Instrumentation;

[Collection("EtpInstrumentation")]
public sealed class ConnectionInstrumentationTests
{
    private static readonly EtpConnectionOptions DefaultOptions =
        new(new Uri("wss://example.com:443/etp"), "user", "pass");

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IWebSocketTransport BuildSuccessTransport(ReadOnlyMemory<byte> responseFrame)
    {
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
            responseFrame.Span.CopyTo(buffer.Span);
            return new ValueTask<ValueWebSocketReceiveResult>(
                new ValueWebSocketReceiveResult(responseFrame.Length, WebSocketMessageType.Binary, true));
        });
        return transport;
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(2); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(2);
        w.WriteString("TestServer");
        w.WriteString("1.0");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(0);
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildProtocolExceptionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(1000); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(2);
        w.WriteInt(14);
        w.WriteString("Unsupported protocol");
        return w.ToArray();
    }

    // ── T011a: etp.connect span on success ───────────────────────────────────

    [Fact]
    public async Task ConnectAsync_Success_ProducesConnectSpanWithAttributes()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var transport = BuildSuccessTransport(BuildOpenSessionFrame());
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.connect");
        Assert.NotNull(span);
        Assert.Equal("example.com", span.GetTagItem("server.address") as string);
        Assert.Equal(443, (int)span.GetTagItem("server.port")!);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        // Credentials must NOT appear in any tag
        foreach (var tag in span.TagObjects)
        {
            Assert.DoesNotContain("user", tag.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pass", tag.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── T011b: etp.connect span on failure ───────────────────────────────────

    [Fact]
    public async Task ConnectAsync_Failure_ProducesConnectSpanWithErrorStatus()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var transport = BuildSuccessTransport(BuildProtocolExceptionFrame());
        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(DefaultOptions, CancellationToken.None));
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.connect");
        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.NotNull(span.GetTagItem("error.type"));
        // No credentials in any tag
        foreach (var tag in span.TagObjects)
        {
            Assert.DoesNotContain("pass", tag.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── T011b (WebSocketException / transport error) ─────────────────────────

    [Fact]
    public async Task ConnectAsync_WebSocketThrows_ProducesConnectSpanWithErrorStatus()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var transport = Substitute.For<IWebSocketTransport>();
        transport.HttpStatusCode.Returns((int?)null);
        transport.ConnectAsync(default!, default!, default, default)
            .ThrowsForAnyArgs(new WebSocketException("refused"));

        var manager = new EtpSessionManager(transport, NullLogger.Instance);

        await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(DefaultOptions, CancellationToken.None));
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.connect");
        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    // ── T011c: etp.disconnect span ───────────────────────────────────────────

    [Fact]
    public async Task CloseAsync_Connected_ProducesDisconnectSpan()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        var transport = BuildSuccessTransport(BuildOpenSessionFrame());
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);

        exported.Clear(); // only care about the disconnect span
        await manager.CloseAsync(CancellationToken.None);
        provider.ForceFlush();

        var span = exported.FirstOrDefault(a => a.OperationName == "etp.disconnect");
        Assert.NotNull(span);
        Assert.Equal("example.com", span.GetTagItem("server.address") as string);
    }

    // ── T011d: no spans without AddEtpInstrumentation ────────────────────────

    [Fact]
    public async Task ConnectAsync_WithoutTracerRegistration_ProducesNoSpans()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            // intentionally NOT calling AddEtpInstrumentation
            .AddInMemoryExporter(exported)
            .Build();

        var transport = BuildSuccessTransport(BuildOpenSessionFrame());
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        provider.ForceFlush();

        Assert.Empty(exported);
    }
}
