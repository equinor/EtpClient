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
/// Integration tests for bounded historical Protocol 1 data retrieval via
/// <see cref="EtpClient.RequestChannelRangeAsync"/>.
/// T026 [US3]: happy path, multipart range response, protocol exception mapping.
/// </summary>
public sealed class RequestChannelRangeAsyncTests : IDisposable
{
    private const string ValidUser = "rangeuser";
    private const string ValidPass = "rangepass";

    public void Dispose() { }

    // ── T026 [US3]: happy path — single-part response ─────────────────────────

    [Fact]
    public async Task RequestChannelRangeAsync_SinglePartResponse_ReturnsSamplesAndNotMultipart()
    {
        await using var server = BuildRangeServer(async (ws, correlationId, ct) =>
        {
            await SendChannelDataAsync(ws, channelId: 1L, indexValue: 1500L, doubleValue: 42.0,
                correlationId: correlationId, messageId: 10L, finalPart: true, ct);
        });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 1000L,
            ToIndex = 2000L,
        };
        var result = await client.RequestChannelRangeAsync(request);

        Assert.Equal(ChannelRangeResultState.Completed, result.State);
        Assert.Single(result.Samples);
        Assert.Equal(1L, result.Samples[0].ChannelId);
        Assert.Equal(1500L, result.Samples[0].Indexes[0]);
        Assert.Equal(42.0, result.Samples[0].Value);
        Assert.False(result.WasMultipart);
    }

    // ── T026 [US3]: multipart response aggregation ────────────────────────────

    [Fact]
    public async Task RequestChannelRangeAsync_MultipartResponse_AggregatesAllSamples()
    {
        await using var server = BuildRangeServer(async (ws, correlationId, ct) =>
        {
            await SendChannelDataAsync(ws, channelId: 1L, indexValue: 1000L, doubleValue: 10.0,
                correlationId: correlationId, messageId: 10L, finalPart: false, ct);
            await SendChannelDataAsync(ws, channelId: 1L, indexValue: 1500L, doubleValue: 15.0,
                correlationId: correlationId, messageId: 11L, finalPart: false, ct);
            await SendChannelDataAsync(ws, channelId: 1L, indexValue: 2000L, doubleValue: 20.0,
                correlationId: correlationId, messageId: 12L, finalPart: true, ct);
        });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 1000L,
            ToIndex = 2000L,
        };
        var result = await client.RequestChannelRangeAsync(request);

        Assert.Equal(ChannelRangeResultState.Completed, result.State);
        Assert.Equal(3, result.Samples.Count);
        Assert.True(result.WasMultipart);
        Assert.Equal(1000L, result.Samples[0].Indexes[0]);
        Assert.Equal(2000L, result.Samples[2].Indexes[0]);
    }

    // ── T026 [US3]: ProtocolException maps to EtpChannelStreamingException ────

    [Fact]
    public async Task RequestChannelRangeAsync_ServerSendsProtocolException_ThrowsEtpChannelStreamingException()
    {
        await using var server = BuildRangeServer(async (ws, correlationId, ct) =>
        {
            await SendProtocolExceptionAsync(ws, errorCode: 4, correlationId: correlationId, messageId: 10L, ct);
        });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 0L,
            ToIndex = 1000L,
        };
        await Assert.ThrowsAsync<EtpChannelStreamingException>(() =>
            client.RequestChannelRangeAsync(request));
    }

    // ── T026 [US3]: not connected throws ──────────────────────────────────────

    [Fact]
    public async Task RequestChannelRangeAsync_NotConnected_ThrowsInvalidOperationException()
    {
        await using var client = new EtpClient();
        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [1L],
            FromIndex = 0L,
            ToIndex = 1000L,
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.RequestChannelRangeAsync(request));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static EtpConnectionOptions ValidOptions() =>
        new(new Uri("ws://localhost"), ValidUser, ValidPass);

    private static async Task SendChannelDataAsync(
        WebSocket ws, long channelId, long indexValue, double doubleValue,
        long correlationId, long messageId, bool finalPart, CancellationToken ct)
    {
        var flags = finalPart ? EtpMessageFlags.FinalPart : 0;
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelData);
        w.WriteLong(correlationId); w.WriteLong(messageId); w.WriteInt(flags);

        w.WriteArrayStart(1);
        w.WriteArrayStart(1); w.WriteLong(indexValue); w.WriteArrayEnd(); // indexes
        w.WriteLong(channelId);
        w.WriteLong(1L); w.WriteDouble(doubleValue); // DataValue union: 1 = double
        w.WriteArrayEnd(); // valueAttributes: empty
        w.WriteArrayEnd();

        await ws.SendAsync(w.ToArray().ToArray(), WebSocketMessageType.Binary, true, ct);
    }

    private static async Task SendProtocolExceptionAsync(
        WebSocket ws, int errorCode, long correlationId, long messageId, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(0); // protocol 0 (core)
        w.WriteInt(EtpMessageType.ProtocolException);
        w.WriteLong(correlationId); w.WriteLong(messageId); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteInt(errorCode);
        w.WriteLong(1L); w.WriteString("Test error"); // error message: Some("Test error")
        await ws.SendAsync(w.ToArray().ToArray(), WebSocketMessageType.Binary, true, ct);
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("RangeTestServer");
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

    /// <summary>
    /// Reads the <c>ChannelRangeRequest</c> frame and returns the <c>messageId</c>
    /// from its header so the server can use it as correlation ID in responses.
    /// </summary>
    private static long ReadRangeRequestMessageId(byte[] buf, int count)
    {
        var reader = new AvroReader(buf.AsMemory(0, count));
        var header = EtpMessageHeader.ReadFrom(reader);
        return header.MessageId;
    }

    private delegate Task RangeResponder(WebSocket ws, long correlationId, CancellationToken ct);

    private static RangeTestServer BuildRangeServer(RangeResponder respond)
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

                // Consume ChannelRangeRequest and extract its messageId
                do { r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None); }
                while (!r.EndOfMessage && r.MessageType != WebSocketMessageType.Close);
                var correlationId = ReadRangeRequestMessageId(buf, r.Count);

                // Respond with ChannelData frame(s)
                await respond(ws, correlationId, CancellationToken.None);
            });
        });

        return new RangeTestServer(new TestServer(builder));
    }

    private static EtpClient BuildClient(RangeTestServer server)
    {
        return new EtpClient(
            transportFactory: () => new TestServerTransport(server.TestServer),
            logger: NullLogger.Instance);
    }

    private sealed class RangeTestServer : IAsyncDisposable
    {
        public TestServer TestServer { get; }
        public RangeTestServer(TestServer s) => TestServer = s;
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
