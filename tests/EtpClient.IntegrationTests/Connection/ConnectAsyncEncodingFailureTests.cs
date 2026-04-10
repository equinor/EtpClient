using System.Net.WebSockets;
using System.Text;
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
/// Integration tests for encoding-related failure behavior.
/// T022 [US2]: Verifies that encoding mismatches and server rejection produce
/// observable, distinguishable, and secret-safe failures.
/// </summary>
public sealed class ConnectAsyncEncodingFailureTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    // A server that always responds with binary frames
    private readonly TestServer _binaryOnlyServer;

    public ConnectAsyncEncodingFailureTests()
    {
        _binaryOnlyServer = BuildBinaryOnlyServer(ValidUser, ValidPass);
    }

    public void Dispose() => _binaryOnlyServer.Dispose();

    // ── Mismatch: client selects JSON but server responds binary ──────────────

    [Fact]
    public async Task ConnectAsync_JsonSelected_BinaryOnlyServer_ThrowsProtocolException()
    {
        await using var client = BuildClient(_binaryOnlyServer);
        var options = new EtpConnectionOptions(
            new Uri("ws://localhost/etp"), ValidUser, ValidPass,
            messageEncoding: EtpMessageEncoding.Json);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => client.ConnectAsync(options));

        Assert.Equal(EtpConnectionFailureCategory.Protocol, ex.Category);
    }

    [Fact]
    public async Task ConnectAsync_EncodingMismatch_ExceptionMessageIsSecretSafe()
    {
        await using var client = BuildClient(_binaryOnlyServer);
        var options = new EtpConnectionOptions(
            new Uri("ws://localhost/etp"), ValidUser, ValidPass,
            messageEncoding: EtpMessageEncoding.Json);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => client.ConnectAsync(options));

        Assert.DoesNotContain(ValidPass, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ValidUser, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── ProtocolException from server: distinguishable from transport error ────

    [Fact]
    public async Task ConnectAsync_ServerSendsProtocolException_ThrowsProtocolCategory()
    {
        using var protocolExceptionServer = BuildProtocolExceptionServer(ValidUser, ValidPass);
        await using var client = BuildClient(protocolExceptionServer);
        var options = new EtpConnectionOptions(
            new Uri("ws://localhost/etp"), ValidUser, ValidPass,
            messageEncoding: EtpMessageEncoding.Binary);

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => client.ConnectAsync(options));

        Assert.Equal(EtpConnectionFailureCategory.Protocol, ex.Category);
        Assert.NotNull(ex.EtpErrorCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private global::EtpClient.EtpClient BuildClient(TestServer server) =>
        new global::EtpClient.EtpClient(
            () => new TestServerTransport(server),
            NullLogger.Instance);

#pragma warning disable CS0618
    private static TestServer BuildBinaryOnlyServer(string user, string pass)
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

                var auth = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
                if (auth != expected)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }

                var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                var seg = new byte[8192];

                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(seg), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                        return;
                    }
                } while (!r.EndOfMessage);

                // Always respond with a binary-encoded OpenSession, regardless of client's encoding
                var w = new AvroWriter();
                w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
                w.WriteString("BinaryOnlyServer");
                w.WriteString("1.0");
                w.WriteString(Guid.NewGuid().ToString());
                w.WriteArrayStart(0); w.WriteArrayStart(0);

                var response = w.ToArray();
                await ws.SendAsync(
                    new ArraySegment<byte>(response.ToArray()),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    CancellationToken.None);

                while (ws.State == WebSocketState.Open)
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(seg), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                }
            });
        });

        return new TestServer(builder);
    }

    private static TestServer BuildProtocolExceptionServer(string user, string pass)
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

                var auth = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
                if (auth != expected)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }

                var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                var seg = new byte[8192];

                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(seg), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                        return;
                    }
                } while (!r.EndOfMessage);

                // Respond with a binary ProtocolException (errorCode=1003, "Unsupported encoding")
                var w = new AvroWriter();
                w.WriteInt(0); w.WriteInt(EtpMessageType.ProtocolException); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
                w.WriteInt(1003);
                w.WriteString("Unsupported encoding");

                var response = w.ToArray();
                await ws.SendAsync(
                    new ArraySegment<byte>(response.ToArray()),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    CancellationToken.None);

                while (ws.State == WebSocketState.Open)
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(seg), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                }
            });
        });
        return new TestServer(builder);
    }
#pragma warning restore CS0618

    private sealed class TestServerTransport : IWebSocketTransport
    {
        private readonly TestServer _server;
        private WebSocket? _ws;

        internal TestServerTransport(TestServer server) => _server = server;

        public WebSocketState State => _ws?.State ?? WebSocketState.None;
        public int? HttpStatusCode { get; private set; }

        public async Task ConnectAsync(Uri uri, string auth, TimeSpan keepAlive, CancellationToken ct)
        {
            var client = _server.CreateWebSocketClient();
            client.ConfigureRequest = req => req.Headers["Authorization"] = auth;
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

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool eom, CancellationToken ct)
            => _ws!.SendAsync(buffer, messageType, eom, ct);

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct)
            => _ws!.ReceiveAsync(buffer, ct);

        public Task CloseOutputAsync(WebSocketCloseStatus status, string? desc, CancellationToken ct)
            => _ws!.CloseOutputAsync(status, desc, ct);

        public ValueTask DisposeAsync() { _ws?.Dispose(); return ValueTask.CompletedTask; }
    }
}
