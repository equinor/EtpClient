#pragma warning disable CS0618
using System.Net.WebSockets;
using System.Text;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Integration tests for live Protocol 1 streaming via <see cref="EtpClient.StartChannelStreamingAsync"/>
/// and <see cref="EtpClient.StopChannelStreamingAsync"/>.
/// T018 [US2]: happy path, lifecycle events, selected-channel stop.
/// </summary>
public sealed class StartChannelStreamingAsyncTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    public void Dispose() { }

    // ── T018 [US2]: live stream delivers ChannelData events ───────────────────

    [Fact]
    public async Task StartChannelStreamingAsync_ServerSendsChannelData_EventsAreYielded()
    {
        await using var server = BuildStreamingServer(async (ws, ct) =>
        {
            // Send two ChannelData frames...
            await SendChannelDataAsync(ws, channelId: 1L, indexValue: 1000L, doubleValue: 99.9, messageId: 3L, ct);
            await SendChannelDataAsync(ws, channelId: 1L, indexValue: 2000L, doubleValue: 100.0, messageId: 4L, ct);
            // ...then ChannelRemove to end the stream
            await SendChannelRemoveAsync(ws, channelId: 1L, reason: null, messageId: 5L, ct);
        });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var subscriptions = new[] { new ChannelSubscriptionInfo(1L, startLatest: true, receiveChangeNotifications: false) };
        var events = new List<ChannelEvent>();
        await foreach (var ev in client.StartChannelStreamingAsync(subscriptions))
            events.Add(ev);

        var dataEvents = events.Where(e => e.Kind == ChannelEventKind.Data).ToList();
        Assert.Equal(2, dataEvents.Count);
        Assert.Equal(1L, dataEvents[0].DataItems[0].ChannelId);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_ServerSendsStatusChange_StatusChangeEventDelivered()
    {
        await using var server = BuildStreamingServer(async (ws, ct) =>
        {
            await SendChannelStatusChangeAsync(ws, channelId: 2L, statusIndex: 1 /* Inactive */, messageId: 3L, ct);
            await SendChannelRemoveAsync(ws, channelId: 2L, reason: "Closed", messageId: 4L, ct);
        });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var subscriptions = new[] { new ChannelSubscriptionInfo(2L, startLatest: true, receiveChangeNotifications: true) };
        var events = new List<ChannelEvent>();
        await foreach (var ev in client.StartChannelStreamingAsync(subscriptions))
            events.Add(ev);

        var statusEvent = events.FirstOrDefault(e => e.Kind == ChannelEventKind.StatusChange);
        Assert.NotNull(statusEvent);
        Assert.Equal(2L, statusEvent.ChannelId);
        Assert.Equal("Inactive", statusEvent.NewStatus);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_ServerSendsChannelRemove_StreamEnds()
    {
        await using var server = BuildStreamingServer(async (ws, ct) =>
        {
            await SendChannelRemoveAsync(ws, channelId: 3L, reason: "Shutdown", messageId: 3L, ct);
        });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var subscriptions = new[] { new ChannelSubscriptionInfo(3L, startLatest: true, receiveChangeNotifications: false) };
        var events = new List<ChannelEvent>();
        await foreach (var ev in client.StartChannelStreamingAsync(subscriptions))
            events.Add(ev);

        var removeEvent = events.Single(e => e.Kind == ChannelEventKind.Remove);
        Assert.Equal(3L, removeEvent.ChannelId);
        Assert.Equal("Shutdown", removeEvent.RemoveReason);
    }

    [Fact]
    public async Task StartChannelStreamingAsync_NotConnected_ThrowsInvalidOperationException()
    {
        await using var client = new EtpClient();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.StartChannelStreamingAsync([])) { }
        });
    }

    [Fact]
    public async Task StopChannelStreamingAsync_SessionRemainsConnectedAfterStop()
    {
        await using var server = BuildStreamingServer(async (ws, ct) =>
        {
            await SendChannelRemoveAsync(ws, channelId: 1L, reason: null, messageId: 3L, ct);
            // keep the WebSocket open so the session stays connected after streaming
            var buf = new byte[64];
            while (ws.State == WebSocketState.Open)
            {
                var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (r.MessageType == WebSocketMessageType.Close)
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", ct);
            }
        });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var subscriptions = new[] { new ChannelSubscriptionInfo(1L, startLatest: true, receiveChangeNotifications: false) };
        await foreach (var _ in client.StartChannelStreamingAsync(subscriptions)) { }

        // After streaming ends via ChannelRemove, the session is still connected
        Assert.Equal(EtpConnectionState.Connected, client.State);

        // Calling StopChannelStreamingAsync while not streaming should work (fire-and-forget)
        await client.StopChannelStreamingAsync([1L]);
        Assert.Equal(EtpConnectionState.Connected, client.State);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static EtpConnectionOptions ValidOptions() =>
        new(new Uri("ws://localhost"), ValidUser, ValidPass);

    private static async Task SendChannelDataAsync(
        WebSocket ws, long channelId, long indexValue, double doubleValue, long messageId, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelData);
        w.WriteLong(0L); w.WriteLong(messageId); w.WriteInt(EtpMessageFlags.FinalPart);

        w.WriteArrayStart(1);
        w.WriteArrayStart(1); w.WriteLong(indexValue); w.WriteArrayEnd(); // indexes
        w.WriteLong(channelId);
        w.WriteLong(1L); w.WriteDouble(doubleValue); // DataValue: double
        w.WriteArrayEnd(); // valueAttributes: empty
        w.WriteArrayEnd();

        await ws.SendAsync(w.ToArray().ToArray(), WebSocketMessageType.Binary, true, ct);
    }

    private static async Task SendChannelStatusChangeAsync(
        WebSocket ws, long channelId, int statusIndex, long messageId, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelStatusChange);
        w.WriteLong(0L); w.WriteLong(messageId); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteLong(channelId);
        w.WriteLong(statusIndex);
        await ws.SendAsync(w.ToArray().ToArray(), WebSocketMessageType.Binary, true, ct);
    }

    private static async Task SendChannelRemoveAsync(
        WebSocket ws, long channelId, string? reason, long messageId, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelRemove);
        w.WriteLong(0L); w.WriteLong(messageId); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteLong(channelId);
        if (reason is null) w.WriteLong(0L);
        else { w.WriteLong(1L); w.WriteString(reason); }
        await ws.SendAsync(w.ToArray().ToArray(), WebSocketMessageType.Binary, true, ct);
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("StreamingTestServer");
        w.WriteString("1.0.0-test");
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

    private delegate Task StreamingResponder(WebSocket ws, CancellationToken ct);

    private static StreamingTestServer BuildStreamingServer(StreamingResponder respond)
    {
        var builder = new WebHostBuilder().Configure(app =>
        {
            app.UseWebSockets();
            app.Run(async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }

                var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidUser}:{ValidPass}"));
                if (authHeader != expected) { ctx.Response.StatusCode = 401; return; }

                var ws = await ctx.WebSockets.AcceptWebSocketAsync(subProtocol: "etp12.energistics.org");
                var buf = new byte[64 * 1024];

                // Consume RequestSession
                WebSocketReceiveResult r;
                do { r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None); }
                while (!r.EndOfMessage && r.MessageType != WebSocketMessageType.Close);

                // Send OpenSession
                var openFrame = BuildOpenSessionFrame();
                await ws.SendAsync(new ArraySegment<byte>(openFrame.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);

                // Consume ChannelStreamingStart (ignore content)
                do { r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None); }
                while (!r.EndOfMessage && r.MessageType != WebSocketMessageType.Close);

                await respond(ws, CancellationToken.None);
            });
        });

        return new StreamingTestServer(new TestServer(builder));
    }

    private static EtpClient BuildClient(StreamingTestServer server)
    {
        return new EtpClient(
            transportFactory: () => new TestServerTransport(server.TestServer),
            logger: NullLogger.Instance);
    }

    private sealed class StreamingTestServer : IAsyncDisposable
    {
        public TestServer TestServer { get; }
        public StreamingTestServer(TestServer s) => TestServer = s;
        public ValueTask DisposeAsync() { TestServer.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class TestServerTransport : IWebSocketTransport
    {
        private readonly TestServer _server;
        private WebSocket? _ws;

        public TestServerTransport(TestServer server) => _server = server;

        public WebSocketState State => _ws?.State ?? WebSocketState.None;
        public int? HttpStatusCode { get; private set; }

        public async Task ConnectAsync(
            Uri uri, string authorizationHeaderValue,
            TimeSpan keepAliveInterval, CancellationToken ct)
        {
            var client = _server.CreateWebSocketClient();
            client.ConfigureRequest = req =>
                req.Headers["Authorization"] = authorizationHeaderValue;

            try
            {
                var baseAddress = _server.BaseAddress;
                var wsUri = new UriBuilder(baseAddress)
                {
                    Scheme = baseAddress.Scheme.Replace("http", "ws"),
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
