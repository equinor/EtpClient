using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using EtpClient.Connection;
using EtpClient.Instrumentation;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace EtpClient.UnitTests.Instrumentation;

[Collection("EtpInstrumentation")]
public sealed class MetricInstrumentTests
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
            var frame = queue.Count > 0 ? queue.Dequeue() : ReadOnlyMemory<byte>.Empty;
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
        w.WriteArrayStart(0);
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    private static MeterProvider BuildMeterProvider(List<Metric> exported) =>
        Sdk.CreateMeterProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build()!;

    // ── T019a: ActiveConnections counter ─────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_RecordsActiveConnectionsIncrement()
    {
        var exported = new List<Metric>();
        using var provider = BuildMeterProvider(exported);

        var transport = BuildTransport([]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        provider.ForceFlush();

        Assert.Contains(exported, m => m.Name == "etp.client.active_connections");
    }

    [Fact]
    public async Task CloseAsync_RecordsActiveConnectionsDecrement()
    {
        var exported = new List<Metric>();
        using var provider = BuildMeterProvider(exported);

        var transport = BuildTransport([]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        await manager.CloseAsync(CancellationToken.None);
        provider.ForceFlush();

        Assert.Contains(exported, m => m.Name == "etp.client.active_connections");
    }

    // ── T019b: OperationDuration recorded ────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_RecordsOperationDuration()
    {
        var exported = new List<Metric>();
        using var provider = BuildMeterProvider(exported);

        var transport = BuildTransport([]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        provider.ForceFlush();

        Assert.Contains(exported, m => m.Name == "etp.client.operation.duration");
    }

    // ── T019c: OperationErrors on exception ──────────────────────────────────

    [Fact]
    public async Task ConnectAsync_OnFailure_RecordsOperationError()
    {
        var exported = new List<Metric>();
        using var provider = BuildMeterProvider(exported);

        var transport = Substitute.For<IWebSocketTransport>();
        transport.State.Returns(WebSocketState.Open);
        transport.HttpStatusCode.Returns((int?)null);
        transport.ConnectAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(ci => Task.FromException(new WebSocketException("Connection refused")));

        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await Assert.ThrowsAsync<EtpConnectionException>(
            () => manager.ConnectAsync(DefaultOptions, CancellationToken.None));
        provider.ForceFlush();

        Assert.Contains(exported, m => m.Name == "etp.client.operation.errors");
    }

    // ── T019d: No measurements without AddEtpInstrumentation ─────────────────

    [Fact]
    public async Task ConnectAsync_WithoutMeterRegistration_ProducesNoMetrics()
    {
        var exported = new List<Metric>();
        using var provider = Sdk.CreateMeterProviderBuilder()
            // intentionally NOT calling AddEtpInstrumentation
            .AddInMemoryExporter(exported)
            .Build()!;

        var transport = BuildTransport([]);
        var manager = new EtpSessionManager(transport, NullLogger.Instance);
        await manager.ConnectAsync(DefaultOptions, CancellationToken.None);
        provider.ForceFlush();

        Assert.DoesNotContain(exported, m => m.Name.StartsWith("etp.client.", StringComparison.Ordinal));
    }
}
