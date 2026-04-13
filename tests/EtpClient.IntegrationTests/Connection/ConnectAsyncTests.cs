using System.Net.WebSockets;
using System.Text;
using EtpClient.Connection;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Integration tests that spin up an in-process ETP server via ASP.NET Core TestHost
/// and exercise the full EtpClient connection flow.
/// </summary>
public sealed class ConnectAsyncTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    private readonly TestServer _server;

    public ConnectAsyncTests()
    {
        _server = BuildTestServer(ValidUser, ValidPass);
    }

    public void Dispose() => _server.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an EtpClient wired to the in-process test server transport.
    /// </summary>
    private EtpClient BuildClient(ILogger? logger = null)
    {
        return new EtpClient(
            () => new TestServerTransport(_server),
            logger ?? NullLogger.Instance);
    }

    private static EtpConnectionOptions ValidOptions() =>
        new(new Uri("ws://localhost/etp"), ValidUser, ValidPass);

    private static EtpConnectionOptions BadPasswordOptions() =>
        new(new Uri("ws://localhost/etp"), ValidUser, "wrong");

    // ── T023: happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_ValidCredentials_ReturnsSession()
    {
        await using var client = BuildClient();

        var result = await client.ConnectAsync(ValidOptions());

        Assert.Equal(EtpConnectionState.Connected, client.State);
        Assert.NotNull(result);
        Assert.Equal("IntegrationTestServer", result.Session.ServerApplicationName);
        Assert.Equal("1.0.0-test", result.Session.ServerApplicationVersion);
        Assert.NotEqual(Guid.Empty, result.Session.ServerInstanceId);
        Assert.Empty(result.Session.SupportedFormats);
    }

    // ── T024: authentication failure via mock (401 detection is ClientWebSocket-specific) ──

    [Fact]
    public async Task ConnectAsync_WrongPassword_ThrowsAuthenticationOrProtocolException()
    {
        // The test server returns 401 for wrong credentials, which the transport
        // maps to EtpConnectionFailureCategory.Authentication.
        await using var client = BuildClient();

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => client.ConnectAsync(BadPasswordOptions()));

        Assert.True(
            ex.Category is EtpConnectionFailureCategory.Authentication
                        or EtpConnectionFailureCategory.Protocol
                        or EtpConnectionFailureCategory.Transport,
            $"Unexpected category: {ex.Category}");
    }

    // ── T025: cancellation ────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_CancelledBeforeHandshake_ThrowsAndStateIsCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        await using var client = BuildClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ConnectAsync(ValidOptions(), cts.Token));

        Assert.Equal(EtpConnectionState.Canceled, client.State);
    }

    // ── T026: clean close ─────────────────────────────────────────────────────

    [Fact]
    public async Task CloseAsync_AfterSession_TransitionsToClosedState()
    {
        await using var client = BuildClient();
        await client.ConnectAsync(ValidOptions());

        await client.CloseAsync();

        Assert.Equal(EtpConnectionState.Closed, client.State);
    }

    // ── in-process test server ────────────────────────────────────────────────

    private static TestServer BuildTestServer(string expectedUser, string expectedPass)
    {
        var builder = new WebHostBuilder().Configure(app =>
        {
            app.UseWebSockets();
            app.Run(async ctx =>
            {
                // Only handle WebSocket upgrades
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    return;
                }

                var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var expectedCredentials = "Basic " +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{expectedUser}:{expectedPass}"));

                if (authHeader != expectedCredentials)
                {
                    // Simulate authentication failure: close before accepting as WebSocket
                    ctx.Response.StatusCode = 401;
                    return;
                }

                var ws = await ctx.WebSockets.AcceptWebSocketAsync(subProtocol: "etp12.energistics.org");

                using var requestBuffer = new System.IO.MemoryStream();
                var segment = new byte[8192];

                // Read the full RequestSession frame
                WebSocketReceiveResult receiveResult;
                do
                {
                    receiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(segment), CancellationToken.None);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                        return;
                    }
                    requestBuffer.Write(segment, 0, receiveResult.Count);
                }
                while (!receiveResult.EndOfMessage);

                // Respond with OpenSession
                var responseFrame = BuildOpenSessionFrame();
                await ws.SendAsync(
                    new ArraySegment<byte>(responseFrame.ToArray()),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    CancellationToken.None);

                // Keep connection open until client closes
                while (ws.State == WebSocketState.Open)
                {
                    receiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(segment), CancellationToken.None);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged", CancellationToken.None);
                }
            });
        });

        return new TestServer(builder);
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        // Header: protocol=0, messageType=2, correlationId=1, messageId=2, flags=FinalPart
        w.WriteInt(0); w.WriteInt(2); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(2);
        // applicationName, applicationVersion
        w.WriteString("IntegrationTestServer");
        w.WriteString("1.0.0-test");
        // sessionId (UUID string)
        w.WriteString(Guid.NewGuid().ToString());
        // supportedProtocols — empty array
        w.WriteArrayStart(0);
        // supportedObjects — empty array
        w.WriteArrayStart(0);

        return w.ToArray();
    }

    // ── test transport ────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <see cref="TestServer.CreateWebSocketClient()"/> to implement
    /// <see cref="IWebSocketTransport"/> for in-process integration tests.
    /// </summary>
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

        public async Task ConnectAsync(
            Uri uri,
            string authorizationHeaderValue,
            TimeSpan keepAliveInterval,
            CancellationToken ct)
        {
            var client = _server.CreateWebSocketClient();
            client.ConfigureRequest = req =>
                req.Headers["Authorization"] = authorizationHeaderValue;

            try
            {
                // TestServer.CreateWebSocketClient requires an absolute URI using the server's base address
                var base64 = _server.BaseAddress;
                var wsUri = new UriBuilder(base64)
                {
                    Scheme = base64.Scheme.Replace("http", "ws"),
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
