using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Integration tests for encoding selection during session establishment.
/// T014 [US1]: Binary and JSON encoding are both tested with an in-process server
/// that responds in the appropriate frame type for each mode.
/// </summary>
public sealed class ConnectAsyncEncodingTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    private readonly TestServer _binaryServer;
    private readonly TestServer _jsonServer;

    public ConnectAsyncEncodingTests()
    {
        _binaryServer = BuildTestServer(ValidUser, ValidPass, respondWithJson: false);
        _jsonServer = BuildTestServer(ValidUser, ValidPass, respondWithJson: true);
    }

    public void Dispose()
    {
        _binaryServer.Dispose();
        _jsonServer.Dispose();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private global::EtpClient.EtpClient BuildClient(TestServer server) =>
        new global::EtpClient.EtpClient(
            () => new TestServerTransport(server),
            NullLogger.Instance);

    private static EtpConnectionOptions BinaryOptions() =>
        new(new Uri("ws://localhost/etp"), ValidUser, ValidPass,
            messageEncoding: EtpMessageEncoding.Binary);

    private static EtpConnectionOptions JsonOptions() =>
        new(new Uri("ws://localhost/etp"), ValidUser, ValidPass,
            messageEncoding: EtpMessageEncoding.Json);

    // ── Binary encoding (US1, acceptance scenario 1) ─────────────────────────

    [Fact]
    public async Task ConnectAsync_BinaryEncoding_EstablishesSession()
    {
        await using var client = BuildClient(_binaryServer);

        var result = await client.ConnectAsync(BinaryOptions());

        Assert.Equal(EtpConnectionState.Connected, client.State);
        Assert.Equal(EtpMessageEncoding.Binary, result.MessageEncoding);
        Assert.Equal("BinaryTestServer", result.Session.ServerApplicationName);
    }

    // ── JSON encoding (US1, acceptance scenario 2) ────────────────────────────

    [Fact]
    public async Task ConnectAsync_JsonEncoding_EstablishesSession()
    {
        await using var client = BuildClient(_jsonServer);

        var result = await client.ConnectAsync(JsonOptions());

        Assert.Equal(EtpConnectionState.Connected, client.State);
        Assert.Equal(EtpMessageEncoding.Json, result.MessageEncoding);
        Assert.Equal("JsonTestServer", result.Session.ServerApplicationName);
    }

    // ── Default encoding is binary (spec: backward compat) ───────────────────

    [Fact]
    public async Task ConnectAsync_DefaultOptions_UsesBinaryEncoding()
    {
        await using var client = BuildClient(_binaryServer);

        var defaultOptions = new EtpConnectionOptions(
            new Uri("ws://localhost/etp"), ValidUser, ValidPass);
        var result = await client.ConnectAsync(defaultOptions);

        Assert.Equal(EtpMessageEncoding.Binary, result.MessageEncoding);
    }

    // ── In-process test server that responds in binary or JSON ────────────────

#pragma warning disable CS0618
    private static TestServer BuildTestServer(string expectedUser, string expectedPass, bool respondWithJson)
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
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{expectedUser}:{expectedPass}"));

                if (authHeader != expected)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }

                var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                var segment = new byte[8192];
                using var ms = new System.IO.MemoryStream();

                // Read client's request frame (ignore content — just consume it)
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(segment), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                        return;
                    }
                    ms.Write(segment, 0, r.Count);
                } while (!r.EndOfMessage);

                // Respond with OpenSession using the requested encoding
                if (respondWithJson)
                {
                    var responseJson = BuildJsonOpenSessionFrame("JsonTestServer");
                    await ws.SendAsync(
                        new ArraySegment<byte>(responseJson),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                else
                {
                    var responseBytes = BuildBinaryOpenSessionFrame("BinaryTestServer");
                    await ws.SendAsync(
                        new ArraySegment<byte>(responseBytes.ToArray()),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        CancellationToken.None);
                }

                // Keep open until close
                while (ws.State == WebSocketState.Open)
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(segment), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged", CancellationToken.None);
                }
            });
        });

        return new TestServer(builder);
    }
#pragma warning restore CS0618

    private static ReadOnlyMemory<byte> BuildBinaryOpenSessionFrame(string appName)
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString(appName);
        w.WriteString("1.0-test");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(0);  // supportedProtocols
        w.WriteArrayStart(0);  // supportedObjects
        return w.ToArray();
    }

    private static byte[] BuildJsonOpenSessionFrame(string appName)
    {
        var msg = new JsonArray
        {
            new JsonObject
            {
                ["protocol"] = 0,
                ["messageType"] = EtpMessageType.OpenSession,
                ["correlationId"] = 1,
                ["messageId"] = 2,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            new JsonObject
            {
                ["applicationName"] = appName,
                ["applicationVersion"] = "1.0-test",
                ["sessionId"] = Guid.NewGuid().ToString(),
                ["supportedProtocols"] = new JsonArray(),
                ["supportedObjects"] = new JsonArray(),
            },
        };

        return Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    // ── Test transport (same as ConnectAsyncTests.TestServerTransport) ────────

    private sealed class TestServerTransport : IWebSocketTransport
    {
        private readonly TestServer _server;
        private WebSocket? _ws;

        internal TestServerTransport(TestServer server) => _server = server;

        public WebSocketState State => _ws?.State ?? WebSocketState.None;
        public int? HttpStatusCode { get; private set; }

        public async Task ConnectAsync(
            Uri uri, string authorizationHeaderValue, TimeSpan keepAliveInterval, CancellationToken ct)
        {
            var client = _server.CreateWebSocketClient();
            client.ConfigureRequest = req =>
                req.Headers["Authorization"] = authorizationHeaderValue;

            try
            {
                var baseUri = _server.BaseAddress;
                var wsUri = new UriBuilder(baseUri) { Scheme = baseUri.Scheme.Replace("http", "ws"), Path = "/etp" }.Uri;
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

        public ValueTask DisposeAsync() { _ws?.Dispose(); return ValueTask.CompletedTask; }
    }
}
