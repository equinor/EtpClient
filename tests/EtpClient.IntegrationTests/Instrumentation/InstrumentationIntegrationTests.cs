#pragma warning disable CS0618 // TestServer(IWebHostBuilder) is obsolete but required by test infrastructure
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using EtpClient.Connection;
using EtpClient.Instrumentation;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace EtpClient.IntegrationTests.Instrumentation;

/// <summary>
/// Integration tests verifying that AddEtpInstrumentation() produces real spans and metrics
/// when connect and close operations run against an in-process TestServer.
/// T020 [US5].
/// </summary>
public sealed class InstrumentationIntegrationTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    public void Dispose() { }

    // ── T020a: connect produces etp.connect span ──────────────────────────────

    [Fact]
    public async Task ConnectAsync_ProducesConnectSpan()
    {
        var exportedSpans = new List<Activity>();
        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exportedSpans)
            .Build();

        using var server = BuildEchoServer();
        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        traceProvider.ForceFlush();

        Assert.Contains(exportedSpans, a => a.OperationName == "etp.connect");
        var span = exportedSpans.First(a => a.OperationName == "etp.connect");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal("localhost", span.GetTagItem("server.address") as string);
    }

    // ── T020b: close produces etp.disconnect span ─────────────────────────────

    [Fact]
    public async Task CloseAsync_ProducesDisconnectSpan()
    {
        var exportedSpans = new List<Activity>();
        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exportedSpans)
            .Build();

        using var server = BuildEchoServer();
        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());
        exportedSpans.Clear();

        await client.CloseAsync();

        traceProvider.ForceFlush();

        Assert.Contains(exportedSpans, a => a.OperationName == "etp.disconnect");
    }

    // ── T020c: active connections metric is recorded ──────────────────────────

    [Fact]
    public async Task ConnectAsync_RecordsActiveConnectionsMetric()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        using var server = BuildEchoServer();
        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        meterProvider.ForceFlush();

        Assert.Contains(exportedMetrics, m => m.Name == "etp.client.active_connections");
    }

    // ── T020d: operation duration is recorded ────────────────────────────────

    [Fact]
    public async Task ConnectAsync_RecordsOperationDurationMetric()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        using var server = BuildEchoServer();
        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        meterProvider.ForceFlush();

        Assert.Contains(exportedMetrics, m => m.Name == "etp.client.operation.duration");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static EtpConnectionOptions ValidOptions() =>
        new(new Uri("ws://localhost"), ValidUser, ValidPass);

    private static EtpClient BuildClient(TestServer server) =>
        new(() => new TestServerTransport(server), NullLogger<EtpClient>.Instance);

    private static TestServer BuildEchoServer()
    {
        var builder = new WebHostBuilder().Configure(app =>
        {
            app.UseWebSockets();
            app.Run(async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    return;
                }

                var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var expected = "Basic " +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidUser}:{ValidPass}"));
                if (authHeader != expected)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }

                var ws = await ctx.WebSockets.AcceptWebSocketAsync("etp12.energistics.org");

                // Read RequestSession
                var segment = new byte[8192];
                var stream = new System.IO.MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(segment), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                        return;
                    }
                    stream.Write(segment, 0, result.Count);
                } while (!result.EndOfMessage);

                // Send OpenSession
                var frame = BuildOpenSessionFrame().ToArray();
                await ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, CancellationToken.None);

                // Keep alive until closed
                while (ws.State == WebSocketState.Open)
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(segment), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged", CancellationToken.None);
                }
            });
        });

        return new TestServer(builder);
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("InstrumentationTestServer");
        w.WriteString("1.0.0-test");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(0);
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    private sealed class TestServerTransport : IWebSocketTransport
    {
        private readonly TestServer _server;
        private WebSocket? _ws;

        internal TestServerTransport(TestServer server)
        {
            _server = server;
        }

        public WebSocketState State => _ws?.State ?? WebSocketState.None;
        public int? HttpStatusCode { get; private set; }

        public async Task ConnectAsync(Uri uri, string authorizationHeaderValue, TimeSpan keepAliveInterval, CancellationToken ct)
        {
            var client = _server.CreateWebSocketClient();
            client.ConfigureRequest = req => req.Headers["Authorization"] = authorizationHeaderValue;

            try
            {
                var baseAddr = _server.BaseAddress;
                var wsUri = new UriBuilder(baseAddr)
                {
                    Scheme = baseAddr.Scheme.Replace("http", "ws"),
                    Path = "/etp"
                }.Uri;
                _ws = await client.ConnectAsync(wsUri, ct);
            }
            catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                HttpStatusCode = 401;
                throw new WebSocketException(WebSocketError.NotAWebSocket, ex.Message);
            }
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct)
            => _ws!.SendAsync(buffer, messageType, endOfMessage, ct);

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct)
            => _ws!.ReceiveAsync(buffer, ct);

        public Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken ct)
            => _ws!.CloseOutputAsync(status, description, ct);

        public ValueTask DisposeAsync()
        {
            _ws?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
#pragma warning restore CS0618
