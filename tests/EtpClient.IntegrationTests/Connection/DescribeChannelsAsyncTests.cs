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
/// Integration tests for <see cref="EtpClient.DescribeChannelsAsync"/>:
/// happy path, multipart metadata, ProtocolException rejection, and unsupported-URI handling.
/// T010 [US1]: describe channels over an in-process ETP 1.1 test server.
/// </summary>
public sealed class DescribeChannelsAsyncTests : IDisposable
{
    private const string ValidUser = "testuser";
    private const string ValidPass = "testpass";

    public void Dispose() { }

    // ── T010 [US1]: describe single URI returns channel definitions ───────────

    [Fact]
    public async Task DescribeChannelsAsync_ServerReturnsOneChannel_ResultContainsChannel()
    {
        var channel = CreateChannel(channelId: 1L, channelUri: "eml://witsml14/well(abc)/log(L1)/channel(RPM)", channelName: "RPM");

        await using var server = BuildChannelDescribeServer(
            respondWith: (uris, ws, ct) => SendChannelMetadataAsync(ws, [channel], correlationId: 2L, ct));

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var result = await client.DescribeChannelsAsync(["eml://witsml14/well(abc)/log(L1)"]);

        Assert.Equal(ChannelDescriptionState.Completed, result.State);
        Assert.Single(result.Channels);
        Assert.Equal(1L, result.Channels[0].ChannelId);
        Assert.Equal("RPM", result.Channels[0].ChannelName);
        Assert.Equal("eml://witsml14/well(abc)/log(L1)/channel(RPM)", result.Channels[0].ChannelUri);
    }

    [Fact]
    public async Task DescribeChannelsAsync_MultipartResponse_AggregatesAllChannels()
    {
        var ch1 = CreateChannel(channelId: 1L, channelUri: "eml://witsml14/well(abc)/log(L1)/channel(RPM)", channelName: "RPM");
        var ch2 = CreateChannel(channelId: 2L, channelUri: "eml://witsml14/well(abc)/log(L1)/channel(WOB)", channelName: "WOB");
        var ch3 = CreateChannel(channelId: 3L, channelUri: "eml://witsml14/well(abc)/log(L1)/channel(HKLA)", channelName: "HKLA");

        // Send in two multipart messages: [ch1, ch2] then [ch3]
        await using var server = BuildChannelDescribeServer(
            respondWith: async (uris, ws, ct) =>
            {
                await SendChannelMetadataAsync(ws, [ch1, ch2], correlationId: 2L, ct, finalPart: false);
                await SendChannelMetadataAsync(ws, [ch3], correlationId: 2L, ct, finalPart: true);
            });

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var result = await client.DescribeChannelsAsync(["eml://witsml14/well(abc)/log(L1)"]);

        Assert.Equal(ChannelDescriptionState.Completed, result.State);
        Assert.Equal(3, result.Channels.Count);
        Assert.Equal("RPM", result.Channels[0].ChannelName);
        Assert.Equal("WOB", result.Channels[1].ChannelName);
        Assert.Equal("HKLA", result.Channels[2].ChannelName);
    }

    [Fact]
    public async Task DescribeChannelsAsync_ServerReturnsProtocolException_ThrowsEtpChannelStreamingException()
    {
        await using var server = BuildChannelDescribeServer(
            respondWith: (uris, ws, ct) =>
                SendProtocolExceptionAsync(ws, correlationId: 2L, errorCode: 7, message: "URI not supported", ct));

        await using var client = BuildClient(server);
        await client.ConnectAsync(ValidOptions());

        var ex = await Assert.ThrowsAsync<EtpChannelStreamingException>(
            () => client.DescribeChannelsAsync(["eml://unsupported/uri"]));

        Assert.Equal(7, ex.EtpErrorCode);
    }

    [Fact]
    public async Task DescribeChannelsAsync_NotConnected_ThrowsInvalidOperationException()
    {
        await using var client = new global::EtpClient.EtpClient();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.DescribeChannelsAsync(["eml://witsml14/well(abc)"]));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ChannelDefinitionWire CreateChannel(long channelId, string channelUri, string channelName) =>
        new(channelId, channelUri, channelName);

    private static EtpConnectionOptions ValidOptions() =>
        new(new Uri("ws://localhost"), ValidUser, ValidPass);

    private static async Task SendChannelMetadataAsync(
        WebSocket ws,
        IReadOnlyList<ChannelDefinitionWire> channels,
        long correlationId,
        CancellationToken ct,
        bool finalPart = true)
    {
        var w = new AvroWriter();
        var header = new EtpMessageHeader(
            Protocol: EtpProtocol.ChannelStreaming,
            MessageType: EtpChannelStreamingMessageType.ChannelMetadata,
            CorrelationId: correlationId,
            MessageId: correlationId + 1L,
            MessageFlags: finalPart ? EtpMessageFlags.FinalPart : 0);
        header.WriteTo(w);

        w.WriteArrayStart(channels.Count);
        foreach (var ch in channels)
        {
            w.WriteString(ch.ChannelUri);
            w.WriteLong(ch.ChannelId);
            // indexes: 1 primary index
            w.WriteArrayStart(1);
            w.WriteInt(0); // indexType=Time
            w.WriteString("ms");
            w.WriteLong(0L); // depthDatum=null
            w.WriteInt(0); // direction=Increasing
            w.WriteLong(0L); // mnemonic=null
            w.WriteLong(0L); // description=null
            w.WriteLong(0L); // uri=null
            w.WriteMapEnd(); // customData=empty
            w.WriteInt(3); // scale
            w.WriteLong(0L); // timeDatum=null
            w.WriteArrayEnd();
            w.WriteString(ch.ChannelName);
            w.WriteString("double"); // dataType
            w.WriteString("rpm"); // uom
            w.WriteLong(0L); // startIndex=null
            w.WriteLong(0L); // endIndex=null
            w.WriteString(""); // description
            w.WriteInt(0); // status=Active
            w.WriteLong(0L); // contentType=null
            w.WriteString(""); // source
            w.WriteString(""); // measureClass
            w.WriteLong(0L); // uuid=null
            w.WriteMapEnd(); // customData=empty
            w.WriteLong(0L); // domainObject=null
        }
        w.WriteArrayEnd();

        var frame = w.ToArray().ToArray();
        await ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, ct);
    }

    private static async Task SendProtocolExceptionAsync(
        WebSocket ws, long correlationId, int errorCode, string message, CancellationToken ct)
    {
        var w = new AvroWriter();
        w.WriteInt(0); // protocol = Core
        w.WriteInt(EtpMessageType.ProtocolException);
        w.WriteLong(correlationId);
        w.WriteLong(correlationId + 1L);
        w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteInt(errorCode);
        w.WriteString(message);
        var frame = w.ToArray().ToArray();
        await ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, ct);
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame()
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString("DescribeChannelsTestServer");
        w.WriteString("1.0.0-test");
        w.WriteString(Guid.NewGuid().ToString());
        // protocols: Protocol 1 (ChannelStreaming)
        w.WriteArrayStart(2);
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(ProtocolVersion.Etp11.Major);
        w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision);
        w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("producer");
        w.WriteMapStart(0);
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

    private delegate Task ChannelDescribeResponder(
        IReadOnlyList<string> requestedUris, WebSocket ws, CancellationToken ct);

    private static ChannelDescribeTestServer BuildChannelDescribeServer(ChannelDescribeResponder respondWith)
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
                using var ms = new System.IO.MemoryStream();

                // Consume RequestSession
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None); return; }
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);

                // Send OpenSession
                var openFrame = BuildOpenSessionFrame();
                await ws.SendAsync(new ArraySegment<byte>(openFrame.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);

                // Read ChannelDescribe
                ms.SetLength(0);
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);

                // Decode ChannelDescribe uris
                var frame = ms.ToArray().AsMemory();
                var reader = new AvroReader(frame);
                _ = EtpMessageHeader.ReadFrom(reader); // skip header
                var uris = new List<string>();
                long blockCount;
                while ((blockCount = reader.ReadBlockCount()) > 0)
                    for (var i = 0; i < blockCount; i++) uris.Add(reader.ReadString());

                await respondWith(uris, ws, CancellationToken.None);

                // Keep open
                while (ws.State == WebSocketState.Open)
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            });
        });

        return new ChannelDescribeTestServer(new TestServer(builder));
    }

    private static global::EtpClient.EtpClient BuildClient(ChannelDescribeTestServer server)
    {
        return new global::EtpClient.EtpClient(
            transportFactory: () => new TestServerTransport(server.TestServer),
            logger: NullLogger.Instance);
    }

    private sealed class ChannelDescribeTestServer : IAsyncDisposable
    {
        public TestServer TestServer { get; }
        public ChannelDescribeTestServer(TestServer server) => TestServer = server;
        public ValueTask DisposeAsync() { TestServer.Dispose(); return ValueTask.CompletedTask; }
    }

    // ── TestServerTransport (same pattern as DiscoverResourcesAsyncTests) ─────

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

    /// <summary>Wire representation of a channel for test frame building.</summary>
    internal sealed record ChannelDefinitionWire(long ChannelId, string ChannelUri, string ChannelName);
}
#pragma warning restore CS0618
