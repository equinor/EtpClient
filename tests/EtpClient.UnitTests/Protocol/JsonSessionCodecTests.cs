using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EtpClient.Models;
using EtpClient.Protocol;

namespace EtpClient.UnitTests.Protocol;

/// <summary>
/// Unit tests for <see cref="JsonEtpSessionCodec"/>.
/// T013 [US1]: Verifies JSON codec encoding, decoding, and frame-type properties.
/// </summary>
public sealed class JsonSessionCodecTests
{
    private static readonly RequestSessionMessage DefaultMessage = new(
        "EtpClient", "1.0.0", Guid.NewGuid(), []);

    // ── Codec properties ─────────────────────────────────────────────────────

    [Fact]
    public void Encoding_IsJson()
    {
        var codec = new JsonEtpSessionCodec();
        Assert.Equal(EtpMessageEncoding.Json, codec.Encoding);
    }

    [Fact]
    public void FrameType_IsTextWebSocketFrame()
    {
        var codec = new JsonEtpSessionCodec();
        Assert.Equal(WebSocketMessageType.Text, codec.FrameType);
    }

    // ── EncodeRequestSession ─────────────────────────────────────────────────

    [Fact]
    public void EncodeRequestSession_ProducesValidJsonArray()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeRequestSession(DefaultMessage, messageId: 1L);
        var json = Encoding.UTF8.GetString(frame.Span);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void EncodeRequestSession_HeaderContainsCorrectMessageType()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeRequestSession(DefaultMessage, messageId: 1L);

        using var doc = JsonDocument.Parse(frame);
        var headerEl = doc.RootElement[0];

        Assert.Equal(0, headerEl.GetProperty("protocol").GetInt32());
        Assert.Equal(EtpMessageType.RequestSession, headerEl.GetProperty("messageType").GetInt32());
        Assert.Equal(0L, headerEl.GetProperty("correlationId").GetInt64());
        Assert.Equal(1L, headerEl.GetProperty("messageId").GetInt64());
    }

    [Fact]
    public void EncodeRequestSession_BodyContainsClientInstanceId()
    {
        var instanceId = Guid.NewGuid();
        var message = new RequestSessionMessage("App", "1.0", instanceId, []);
        var codec = new JsonEtpSessionCodec();

        var frame = codec.EncodeRequestSession(message, messageId: 1L);
        using var doc = JsonDocument.Parse(frame);
        var bodyEl = doc.RootElement[1];

        Assert.True(bodyEl.TryGetProperty("supportedObjects", out var supportedObjectsEl));
        Assert.Equal(JsonValueKind.Array, supportedObjectsEl.ValueKind);
    }

    [Fact]
    public void EncodeRequestSession_BodyContainsApplicationName()
    {
        var message = new RequestSessionMessage("MyApp", "2.0", Guid.NewGuid(), []);
        var codec = new JsonEtpSessionCodec();

        var frame = codec.EncodeRequestSession(message, messageId: 1L);
        using var doc = JsonDocument.Parse(frame);
        var bodyEl = doc.RootElement[1];

        Assert.Equal("MyApp", bodyEl.GetProperty("applicationName").GetString());
        Assert.Equal("2.0", bodyEl.GetProperty("applicationVersion").GetString());
    }

    // ── PeekMessageType ───────────────────────────────────────────────────────

    [Fact]
    public void PeekMessageType_OpenSessionFrame_ReturnsOpenSession()
    {
        var frame = BuildOpenSessionFrame();
        var codec = new JsonEtpSessionCodec();
        Assert.Equal(EtpMessageType.OpenSession, codec.PeekMessageType(frame));
    }

    [Fact]
    public void PeekMessageType_ProtocolExceptionFrame_ReturnsProtocolException()
    {
        var frame = BuildProtocolExceptionFrame(1003, "test error");
        var codec = new JsonEtpSessionCodec();
        Assert.Equal(EtpMessageType.ProtocolException, codec.PeekMessageType(frame));
    }

    // ── DecodeOpenSession ─────────────────────────────────────────────────────

    [Fact]
    public void DecodeOpenSession_ReturnsExpectedSessionInfo()
    {
        var serverId = Guid.NewGuid();
        var frame = BuildOpenSessionFrame(serverId, "JsonServer", "2.0");
        var codec = new JsonEtpSessionCodec();

        var (header, session) = codec.DecodeOpenSession(frame);

        Assert.Equal(EtpMessageType.OpenSession, header.MessageType);
        Assert.Equal(serverId, session.ServerInstanceId);
        Assert.Equal("JsonServer", session.ServerApplicationName);
        Assert.Equal("2.0", session.ServerApplicationVersion);
    }

    [Fact]
    public void DecodeOpenSession_EmptySupportedProtocols_ReturnsEmptyList()
    {
        var frame = BuildOpenSessionFrame();
        var codec = new JsonEtpSessionCodec();

        var (_, session) = codec.DecodeOpenSession(frame);

        Assert.Empty(session.SupportedProtocols);
    }

    // ── DecodeProtocolException ───────────────────────────────────────────────

    [Fact]
    public void DecodeProtocolException_ReturnsExpectedErrorDetails()
    {
        var frame = BuildProtocolExceptionFrame(1003, "Permission denied");
        var codec = new JsonEtpSessionCodec();

        var (header, errorCode, message) = codec.DecodeProtocolException(frame);

        Assert.Equal(EtpMessageType.ProtocolException, header.MessageType);
        Assert.Equal(1003, errorCode);
        Assert.Equal("Permission denied", message);
    }

    // ── round-trip: encode RequestSession, ensure PeekMessageType matches ────

    [Fact]
    public void RoundTrip_EncodedRequestSession_PeekGivesCorrectType()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeRequestSession(DefaultMessage, messageId: 1L);
        Assert.Equal(EtpMessageType.RequestSession, codec.PeekMessageType(frame));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    internal static ReadOnlyMemory<byte> BuildOpenSessionFrame(
        Guid? serverId = null, string appName = "TestServer", string appVersion = "1.0")
    {
        var id = serverId ?? Guid.NewGuid();
        var msg = new JsonArray
        {
            new JsonObject
            {
                ["protocol"] = 0,
                ["messageType"] = EtpMessageType.OpenSession,
                ["correlationId"] = 1L,
                ["messageId"] = 2L,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            new JsonObject
            {
                ["applicationName"] = appName,
                ["applicationVersion"] = appVersion,
                ["sessionId"] = id.ToString(),
                ["supportedProtocols"] = new JsonArray(),
                ["supportedObjects"] = new JsonArray(),
            },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    internal static ReadOnlyMemory<byte> BuildProtocolExceptionFrame(int errorCode, string message)
    {
        var json = $"[{{\"protocol\":0,\"messageType\":{EtpMessageType.ProtocolException},\"correlationId\":1,\"messageId\":2,\"messageFlags\":{EtpMessageFlags.FinalPart}}},{{\"errorCode\":{errorCode},\"errorMessage\":\"{message}\"}}]";
        return Encoding.UTF8.GetBytes(json);
    }
}
