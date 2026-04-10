#pragma warning disable CS0618 // TestServer(IWebHostBuilder) is obsolete but required by test infrastructure
using System.Net.WebSockets;
using System.Text;
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
/// Integration tests that spin up an in-process ETP server to exercise the full
/// <c>ConnectAsync → DiscoverResourcesAsync</c> flow.
/// T017/T018 [US1, US2]: Discovery happy path, empty Acknowledge, and ProtocolException paths.
/// </summary>
public sealed class DiscoverResourcesAsyncTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    public void Dispose() { }

    // ── T017: happy path – resources returned ────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_ServerReturnsTwoResources_ResultContainsAll()
    {
        var resource1 = CreateResource("eml://witsml20", "witsml20", resourceType: "UriProtocol", hasChildren: 5);
        var resource2 = CreateResource("eml://eml21", "eml21", resourceType: "UriProtocol", hasChildren: 0);

        await using var server = BuildDiscoveryServer(
            respondWith: (uri, ws, ct) =>
                SendResourcesAsync(ws, [resource1, resource2], ct),
            expectedProtocol: "etp12.energistics.org");

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var result = await client.DiscoverResourcesAsync("eml://");

        Assert.Equal(DiscoveryResultState.CompletedWithResources, result.State);
        Assert.Equal(2, result.Resources.Count);
        Assert.Equal("eml://witsml20", result.Resources[0].Uri);
        Assert.Equal("witsml20", result.Resources[0].Name);
        Assert.Equal("UriProtocol", result.Resources[0].ResourceType);
        Assert.Equal("eml://eml21", result.Resources[1].Uri);
    }

    [Fact]
    public async Task DiscoverResourcesAsync_SingleResource_AllFieldsMappedCorrectly()
    {
        const string expectedUri = "eml://witsml20/well(abc-001)";
        const string expectedContentType = "application/x-witsml+xml;version=2.0";
        const string expectedName = "Test Well ABC";
        const string expectedUuid = "6ba7b810-9dad-11d1-80b4-00c04fd430c8";
        const long expectedLastChanged = 1_700_000_000L;

        var resource = new DiscoveredResource
        {
            Uri = expectedUri,
            ContentType = expectedContentType,
            Name = expectedName,
            ChannelSubscribable = true,
            CustomData = new Dictionary<string, string> { { "depth", "3000m" } },
            ResourceType = "DataObject",
            HasChildren = 0,
            Uuid = expectedUuid,
            LastChanged = expectedLastChanged,
            ObjectNotifiable = true,
        };

        await using var server = BuildDiscoveryServer(
            respondWith: (uri, ws, ct) => SendResourcesAsync(ws, [resource], ct),
            expectedProtocol: "etp12.energistics.org");

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var result = await client.DiscoverResourcesAsync("eml://witsml20");

        Assert.Single(result.Resources);
        var r = result.Resources[0];
        Assert.Equal(expectedUri, r.Uri);
        Assert.Equal(expectedContentType, r.ContentType);
        Assert.Equal(expectedName, r.Name);
        Assert.True(r.ChannelSubscribable);
        Assert.Equal("DataObject", r.ResourceType);
        Assert.Equal(0, r.HasChildren);
        Assert.Equal(expectedUuid, r.Uuid);
        Assert.Equal(expectedLastChanged, r.LastChanged);
        Assert.True(r.ObjectNotifiable);
        Assert.Equal("3000m", r.CustomData["depth"]);
    }

    // ── T018: empty Acknowledge path ─────────────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_ServerSendsAcknowledge_ResultIsEmpty()
    {
        await using var server = BuildDiscoveryServer(
            respondWith: (uri, ws, ct) => SendAcknowledgeAsync(ws, ct),
            expectedProtocol: "etp12.energistics.org");

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var result = await client.DiscoverResourcesAsync("eml://empty");

        Assert.Equal(DiscoveryResultState.CompletedEmpty, result.State);
        Assert.Empty(result.Resources);
        Assert.True(result.WasEmptyAcknowledged);
    }

    // ── ProtocolException path ────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_ServerSendsProtocolException_Throws()
    {
        await using var server = BuildDiscoveryServer(
            respondWith: (uri, ws, ct) => SendProtocolExceptionAsync(ws, 2L, 4, "No such URI", ct),
            expectedProtocol: "etp12.energistics.org");

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var ex = await Assert.ThrowsAsync<EtpDiscoveryException>(
            () => client.DiscoverResourcesAsync("eml://bad/uri"));

        Assert.Equal("eml://bad/uri", ex.RequestedUri);
        Assert.Equal(4, ex.EtpErrorCode);
    }

    // ── multipart: FinalPart on last message ─────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_Multipart_AccumulatesAllResourcesUntilFinalPart()
    {
        var resources = Enumerable.Range(1, 3)
            .Select(i => CreateResource($"eml://witsml20/well({i})", $"Well {i}"))
            .ToList();

        await using var server = BuildDiscoveryServer(
            respondWith: (uri, ws, ct) => SendResourcesAsync(ws, resources, ct),
            expectedProtocol: "etp12.energistics.org");

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var result = await client.DiscoverResourcesAsync("eml://witsml20");

        Assert.Equal(3, result.Resources.Count);
        Assert.Equal(DiscoveryResultState.CompletedWithResources, result.State);
    }

    // ── encoding is propagated ────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverResourcesAsync_BinaryEncoding_ResultMessageEncodingIsBinary()
    {
        var resource = CreateResource("eml://witsml20", "witsml20");

        await using var server = BuildDiscoveryServer(
            respondWith: (uri, ws, ct) => SendResourcesAsync(ws, [resource], ct),
            expectedProtocol: "etp12.energistics.org");

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var result = await client.DiscoverResourcesAsync("eml://");

        Assert.Equal(EtpMessageEncoding.Binary, result.MessageEncoding);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static EtpConnectionOptions ValidOptions() =>
        new(new Uri("ws://localhost/etp"), ValidUser, ValidPass);

    private static global::EtpClient.EtpClient BuildClient(
        DiscoveryTestServer server) =>
        new global::EtpClient.EtpClient(
            () => new TestServerTransport(server.TestServer),
            NullLogger.Instance);

    private static DiscoveredResource CreateResource(
        string uri, string name,
        string resourceType = "UriProtocol", int hasChildren = 1) =>
        new()
        {
            Uri = uri,
            ContentType = "",
            Name = name,
            ChannelSubscribable = false,
            CustomData = new Dictionary<string, string>(),
            ResourceType = resourceType,
            HasChildren = hasChildren,
            Uuid = null,
            LastChanged = 0L,
            ObjectNotifiable = false,
        };

    // ── server response builders ──────────────────────────────────────────────

    private static async Task SendResourcesAsync(
        WebSocket ws,
        IReadOnlyList<DiscoveredResource> resources,
        CancellationToken ct)
    {
        for (var i = 0; i < resources.Count; i++)
        {
            var finalPart = i == resources.Count - 1;
            var frame = BuildResponseFrame(resources[i], correlationId: 2L, messageId: (long)(i + 2), finalPart);
            await ws.SendAsync(new ArraySegment<byte>(frame.ToArray()),
                WebSocketMessageType.Binary, true, ct);
        }
    }

    private static async Task SendAcknowledgeAsync(WebSocket ws, CancellationToken ct)
    {
        var w = new AvroWriter();
        // Acknowledge: protocol=3, messageType=1001, correlationId=2 (matches GetResources messageId)
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(EtpMessageType.Acknowledge);
        w.WriteLong(2L); // correlationId = request messageId
        w.WriteLong(3L); // messageId
        w.WriteInt(EtpMessageFlags.FinalPart);
        var frame = w.ToArray().ToArray();
        await ws.SendAsync(new ArraySegment<byte>(frame),
            WebSocketMessageType.Binary, true, ct);
    }

    private static async Task SendProtocolExceptionAsync(
        WebSocket ws, long correlationId, int errorCode, string message, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(0); // protocol = Core (Protocol 0)
        w.WriteInt(EtpMessageType.ProtocolException);
        w.WriteLong(correlationId);
        w.WriteLong(3L);
        w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteInt(errorCode);
        w.WriteString(message);
        var frame = w.ToArray().ToArray();
        await ws.SendAsync(new ArraySegment<byte>(frame),
            WebSocketMessageType.Binary, true, ct);
    }

    private static ReadOnlyMemory<byte> BuildResponseFrame(
        DiscoveredResource resource, long correlationId, long messageId, bool finalPart)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(EtpDiscoveryMessageType.GetResourcesResponse);
        w.WriteLong(correlationId);
        w.WriteLong(messageId);
        w.WriteInt(finalPart ? EtpMessageFlags.FinalPart : 0);

        w.WriteString(resource.Uri);
        w.WriteString(resource.ContentType);
        w.WriteString(resource.Name);
        w.WriteBool(resource.ChannelSubscribable);

        if (resource.CustomData.Count == 0)
        {
            w.WriteMapStart(0);
        }
        else
        {
            w.WriteMapStart(resource.CustomData.Count);
            foreach (var kv in resource.CustomData) { w.WriteString(kv.Key); w.WriteString(kv.Value); }
            w.WriteMapEnd();
        }

        w.WriteString(resource.ResourceType);
        w.WriteInt(resource.HasChildren);

        if (resource.Uuid is null) { w.WriteLong(0L); }
        else { w.WriteLong(1L); w.WriteString(resource.Uuid); }

        w.WriteLong(resource.LastChanged);
        w.WriteBool(resource.ObjectNotifiable);
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("IntegrationTestServer");
        w.WriteString("1.0.0-test");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(1);
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(ProtocolVersion.Etp11.Major);
        w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision);
        w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("store");
        w.WriteMapStart(0);
        w.WriteArrayEnd();
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    // ── in-process test server ────────────────────────────────────────────────

    private delegate Task DiscoveryResponder(
        string requestedUri, WebSocket ws, CancellationToken ct);

    private static DiscoveryTestServer BuildDiscoveryServer(
        DiscoveryResponder respondWith,
        string expectedProtocol)
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

                var ws = await ctx.WebSockets.AcceptWebSocketAsync(subProtocol: expectedProtocol);

                var buf = new byte[8192];
                using var ms = new System.IO.MemoryStream();

                // Phase 1: consume RequestSession
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
                        return;
                    }
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);

                // Phase 2: send OpenSession
                var openFrame = BuildOpenSessionFrame();
                await ws.SendAsync(
                    new ArraySegment<byte>(openFrame.ToArray()),
                    WebSocketMessageType.Binary, true, CancellationToken.None);

                // Phase 3: read GetResources then respond
                ms.SetLength(0);
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);

                var frame = ms.ToArray().AsMemory();
                var avroReader = new AvroReader(frame);
                _ = EtpMessageHeader.ReadFrom(avroReader); // skip header
                var requestedUri = avroReader.ReadString();

                await respondWith(requestedUri, ws, CancellationToken.None);

                // Keep open until client closes
                while (ws.State == WebSocketState.Open)
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            });
        });

        return new DiscoveryTestServer(new TestServer(builder));
    }

    // ── DiscoveryTestServer wrapper ───────────────────────────────────────────

    private sealed class DiscoveryTestServer : IAsyncDisposable
    {
        public TestServer TestServer { get; }

        public DiscoveryTestServer(TestServer server)
        {
            TestServer = server;
        }

        public ValueTask DisposeAsync()
        {
            TestServer.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    // ── TestServerTransport (copy from ConnectAsyncTests) ─────────────────────

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
