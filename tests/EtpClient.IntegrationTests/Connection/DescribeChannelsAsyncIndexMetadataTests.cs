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

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Integration tests verifying that <see cref="EtpClient.DescribeChannelsAsync"/> preserves
/// primary-index metadata (scale, timeDatum, depthDatum) from the wire into the returned
/// <see cref="ChannelDefinition"/> instances.
/// T008 [Foundational], T011 [US1], T018 [US2], T025 [US3].
/// </summary>
public sealed class DescribeChannelsAsyncIndexMetadataTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    public void Dispose() { }

    // ── T008 [Foundational]: scale is preserved ───────────────────────────────

    [Fact]
    public async Task DescribeChannelsAsync_BinaryEncoding_PreservesIndexScale()
    {
        await using var server = BuildServer(scale: 5, timeDatum: null, depthDatum: null);
        await using var client = BuildClient(server);
        await client.ConnectAsync(new EtpConnectionOptions(new Uri("ws://localhost"), ValidUser, ValidPass));

        var result = await client.DescribeChannelsAsync(["eml://test"]);

        Assert.Single(result.Channels);
        Assert.Equal(5, result.Channels[0].IndexScale);
    }

    // ── T011 [US1]: timeDatum preserved from binary wire format ──────────────

    [Fact]
    public async Task DescribeChannelsAsync_BinaryEncoding_PreservesTimeDatum()
    {
        await using var server = BuildServer(scale: 0, timeDatum: "1970-01-01T00:00:00Z", depthDatum: null);
        await using var client = BuildClient(server);
        await client.ConnectAsync(new EtpConnectionOptions(new Uri("ws://localhost"), ValidUser, ValidPass));

        var result = await client.DescribeChannelsAsync(["eml://test"]);

        Assert.Equal("1970-01-01T00:00:00Z", result.Channels[0].IndexTimeDatum);
    }

    [Fact]
    public async Task DescribeChannelsAsync_BinaryEncoding_TimeDatumIsNull_WhenNotProvided()
    {
        await using var server = BuildServer(scale: 0, timeDatum: null, depthDatum: null);
        await using var client = BuildClient(server);
        await client.ConnectAsync(new EtpConnectionOptions(new Uri("ws://localhost"), ValidUser, ValidPass));

        var result = await client.DescribeChannelsAsync(["eml://test"]);

        Assert.Null(result.Channels[0].IndexTimeDatum);
    }

    // ── T018 [US2]: depthDatum and scale preserved ───────────────────────────

    [Fact]
    public async Task DescribeChannelsAsync_BinaryEncoding_PreservesDepthDatumAndScale()
    {
        await using var server = BuildServer(
            indexType: 1, scale: 5, timeDatum: null, depthDatum: "MSL");
        await using var client = BuildClient(server);
        await client.ConnectAsync(new EtpConnectionOptions(new Uri("ws://localhost"), ValidUser, ValidPass));

        var result = await client.DescribeChannelsAsync(["eml://test"]);

        var channel = result.Channels[0];
        Assert.Equal("Depth", channel.IndexType);
        Assert.Equal(5, channel.IndexScale);
        Assert.Equal("MSL", channel.IndexDepthDatum);
    }

    // ── T025 [US3]: channels with no/partial index metadata don't crash ───────

    [Fact]
    public async Task DescribeChannelsAsync_BinaryEncoding_NoIndexMetadata_ChannelStillDecoded()
    {
        // Send a channel with zero-length indexes array
        await using var server = BuildServerWithEmptyIndexes();
        await using var client = BuildClient(server);
        await client.ConnectAsync(new EtpConnectionOptions(new Uri("ws://localhost"), ValidUser, ValidPass));

        var result = await client.DescribeChannelsAsync(["eml://test"]);

        Assert.Single(result.Channels);
        Assert.Equal("Time", result.Channels[0].IndexType); // default
        Assert.Equal(0, result.Channels[0].IndexScale);     // default
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IndexMetadataTestServer BuildServer(
        int indexType = 0, int scale = 0, string? timeDatum = null, string? depthDatum = null)
    {
        var builder = CreateBuilder(
            respondWith: (ws, ct) => SendChannelMetadataAsync(
                ws, indexType, scale, timeDatum, depthDatum, ct));
        return new IndexMetadataTestServer(new TestServer(builder));
    }

    private static IndexMetadataTestServer BuildServerWithEmptyIndexes()
    {
        var builder = CreateBuilder(
            respondWith: (ws, ct) => SendChannelMetadataWithEmptyIndexesAsync(ws, ct));
        return new IndexMetadataTestServer(new TestServer(builder));
    }

    private static IWebHostBuilder CreateBuilder(
        Func<WebSocket, CancellationToken, Task> respondWith)
    {
        return new WebHostBuilder().Configure(app =>
        {
            app.UseWebSockets();
            app.Run(async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }

                var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var expected = "Basic " +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidUser}:{ValidPass}"));
                if (authHeader != expected) { ctx.Response.StatusCode = 401; return; }

                var ws = await ctx.WebSockets.AcceptWebSocketAsync(subProtocol: "etp12.energistics.org");
                var buf = new byte[64 * 1024];

                // Consume RequestSession, send OpenSession
                using var ms = new System.IO.MemoryStream();
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);

                var openFrame = BuildOpenSessionFrame();
                await ws.SendAsync(new ArraySegment<byte>(openFrame.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);

                // Consume Protocol 1 Start (client sends this before ChannelDescribe)
                ms.SetLength(0);
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);

                // Consume ChannelDescribe
                ms.SetLength(0);
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);

                await respondWith(ws, CancellationToken.None);

                while (ws.State == WebSocketState.Open)
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            });
        });
    }

    private static async Task SendChannelMetadataAsync(
        WebSocket ws, int indexType, int scale, string? timeDatum, string? depthDatum, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelMetadata);
        w.WriteLong(2L); w.WriteLong(3L);
        w.WriteInt(EtpMessageFlags.FinalPart);

        w.WriteArrayStart(1);
        w.WriteString("eml://test/channel(C)");
        w.WriteLong(1L);

        // indexes: 1 entry
        w.WriteArrayStart(1);
        w.WriteInt(indexType);
        w.WriteString("us");
        if (depthDatum is null) { w.WriteLong(0L); } else { w.WriteLong(1L); w.WriteString(depthDatum); }
        w.WriteInt(0); // direction
        w.WriteLong(0L); w.WriteLong(0L); w.WriteLong(0L); // mnemonic, description, uri = null
        w.WriteMapEnd(); // customData
        w.WriteInt(scale);
        if (timeDatum is null) { w.WriteLong(0L); } else { w.WriteLong(1L); w.WriteString(timeDatum); }
        w.WriteArrayEnd();

        w.WriteString("C"); w.WriteString("double"); w.WriteString("rpm");
        w.WriteLong(0L); w.WriteLong(0L); // startIndex, endIndex = null
        w.WriteString(""); w.WriteInt(0); // description, status=Active
        w.WriteLong(0L); // contentType=null
        w.WriteString(""); w.WriteString(""); // source, measureClass
        w.WriteLong(0L); // uuid=null
        w.WriteMapEnd(); // customData
        w.WriteLong(0L); // domainObject=null
        w.WriteArrayEnd();

        var frame = w.ToArray().ToArray();
        await ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, ct);
    }

    private static async Task SendChannelMetadataWithEmptyIndexesAsync(WebSocket ws, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelMetadata);
        w.WriteLong(2L); w.WriteLong(3L);
        w.WriteInt(EtpMessageFlags.FinalPart);

        w.WriteArrayStart(1);
        w.WriteString("eml://test/channel(C)");
        w.WriteLong(1L);

        // indexes: empty array — WriteArrayStart(0) already writes the block terminator
        w.WriteArrayStart(0);

        w.WriteString("C"); w.WriteString("double"); w.WriteString("rpm");
        w.WriteLong(0L); w.WriteLong(0L);
        w.WriteString(""); w.WriteInt(0);
        w.WriteLong(0L);
        w.WriteString(""); w.WriteString("");
        w.WriteLong(0L);
        w.WriteMapEnd();
        w.WriteLong(0L);
        w.WriteArrayEnd();

        var frame = w.ToArray().ToArray();
        await ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, ct);
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("TestServer"); w.WriteString("1.0.0-test");
        w.WriteString(Guid.NewGuid().ToString());
        w.WriteArrayStart(1);
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(1); w.WriteInt(1); w.WriteInt(0); w.WriteInt(0); // version 1.1.0.0
        w.WriteString("producer");
        w.WriteMapStart(0);
        w.WriteArrayEnd();
        w.WriteArrayStart(0);
        return w.ToArray();
    }

    private static EtpClient BuildClient(IndexMetadataTestServer server) =>
        new(transportFactory: () => new TestServerTransport(server.TestServer),
            logger: NullLogger.Instance);

    private sealed class IndexMetadataTestServer : IAsyncDisposable
    {
        public TestServer TestServer { get; }
        public IndexMetadataTestServer(TestServer s) => TestServer = s;
        public ValueTask DisposeAsync() { TestServer.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class TestServerTransport : IWebSocketTransport
    {
        private readonly TestServer _server;
        private WebSocket? _ws;

        public TestServerTransport(TestServer server) => _server = server;

        public WebSocketState State => _ws?.State ?? WebSocketState.None;
        public int? HttpStatusCode { get; private set; }

        public async Task ConnectAsync(Uri uri, string authorizationHeaderValue,
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

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken ct) =>
            _ws!.SendAsync(buffer, messageType, endOfMessage, ct);

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct) =>
            _ws!.ReceiveAsync(buffer, ct);

        public Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken ct) =>
            _ws!.CloseOutputAsync(status, description, ct);

        public ValueTask DisposeAsync()
        {
            _ws?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
