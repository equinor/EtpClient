using System.Net.WebSockets;
using System.Text;
using EtpClient.Models;
using EtpClient.Protocol;

namespace EtpClient.UnitTests.Protocol;

/// <summary>
/// Unit tests for <see cref="BinaryEtpSessionCodec"/>.
/// T012 [US1]: Verifies binary codec encoding, decoding, and frame-type properties.
/// </summary>
public sealed class BinarySessionCodecTests
{
    private static readonly EtpConnectionOptions DefaultOptions =
        new(new Uri("wss://example.com/etp"), "user", "pass");

    private static readonly RequestSessionMessage DefaultMessage = new(
        "EtpClient", "1.0.0", Guid.NewGuid(), []);

    // ── Codec properties ─────────────────────────────────────────────────────

    [Fact]
    public void Encoding_IsBinary()
    {
        var codec = new BinaryEtpSessionCodec();
        Assert.Equal(EtpMessageEncoding.Binary, codec.Encoding);
    }

    [Fact]
    public void FrameType_IsBinaryWebSocketFrame()
    {
        var codec = new BinaryEtpSessionCodec();
        Assert.Equal(WebSocketMessageType.Binary, codec.FrameType);
    }

    // ── EncodeRequestSession ─────────────────────────────────────────────────

    [Fact]
    public void EncodeRequestSession_ReturnsNonEmptyBytes()
    {
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeRequestSession(DefaultMessage, messageId: 1L);
        Assert.False(frame.IsEmpty);
    }

    [Fact]
    public void EncodeRequestSession_FrameIsDecodableByAvroReader()
    {
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeRequestSession(DefaultMessage, messageId: 1L);

        // Should be able to peek message type without exception
        var messageType = codec.PeekMessageType(frame);
        Assert.Equal(EtpMessageType.RequestSession, messageType);
    }

    // ── PeekMessageType ───────────────────────────────────────────────────────

    [Fact]
    public void PeekMessageType_OpenSessionFrame_ReturnsOpenSession()
    {
        var frame = BuildOpenSessionFrame();
        var codec = new BinaryEtpSessionCodec();
        Assert.Equal(EtpMessageType.OpenSession, codec.PeekMessageType(frame));
    }

    [Fact]
    public void PeekMessageType_ProtocolExceptionFrame_ReturnsProtocolException()
    {
        var frame = BuildProtocolExceptionFrame(1003, "test error");
        var codec = new BinaryEtpSessionCodec();
        Assert.Equal(EtpMessageType.ProtocolException, codec.PeekMessageType(frame));
    }

    // ── DecodeOpenSession ─────────────────────────────────────────────────────

    [Fact]
    public void DecodeOpenSession_ReturnsExpectedSessionInfo()
    {
        var serverId = Guid.NewGuid();
        var frame = BuildOpenSessionFrame(serverId, "TestServer", "1.0");
        var codec = new BinaryEtpSessionCodec();

        var (header, session) = codec.DecodeOpenSession(frame);

        Assert.Equal(EtpMessageType.OpenSession, header.MessageType);
        Assert.Equal(serverId, session.ServerInstanceId);
        Assert.Equal("TestServer", session.ServerApplicationName);
        Assert.Equal("1.0", session.ServerApplicationVersion);
    }

    [Fact]
    public void DecodeOpenSession_WithProtocolCapabilities_PreservesLaterProtocols()
    {
        var serverId = Guid.NewGuid();
        var frame = BuildOpenSessionFrameWithProtocolCapabilities(serverId, "TestServer", "1.0");
        var codec = new BinaryEtpSessionCodec();

        var (_, session) = codec.DecodeOpenSession(frame);

        Assert.Collection(
            session.SupportedProtocols,
            protocol =>
            {
                Assert.Equal(1, protocol.Protocol);
                Assert.Equal("producer", protocol.Role);
            },
            protocol =>
            {
                Assert.Equal(3, protocol.Protocol);
                Assert.Equal("store", protocol.Role);
            });
        Assert.True(session.SupportsDiscovery);
    }

    // ── DecodeProtocolException ───────────────────────────────────────────────

    [Fact]
    public void DecodeProtocolException_ReturnsExpectedErrorDetails()
    {
        var frame = BuildProtocolExceptionFrame(1003, "Permission denied");
        var codec = new BinaryEtpSessionCodec();

        var (header, errorCode, message) = codec.DecodeProtocolException(frame);

        Assert.Equal(EtpMessageType.ProtocolException, header.MessageType);
        Assert.Equal(1003, errorCode);
        Assert.Equal("Permission denied", message);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    internal static ReadOnlyMemory<byte> BuildOpenSessionFrame(
        Guid? serverId = null, string appName = "TestServer", string appVersion = "1.0")
    {
        var id = serverId ?? Guid.NewGuid();
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.OpenSession); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString(appName);
        w.WriteString(appVersion);
        w.WriteString(id.ToString());
        w.WriteArrayStart(0);   // supportedProtocols
        w.WriteArrayStart(0);   // supportedObjects
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildOpenSessionFrameWithProtocolCapabilities(
        Guid serverId, string appName, string appVersion)
    {
        var w = new AvroWriter();
        w.WriteInt(0);
        w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L);
        w.WriteLong(2L);
        w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteString(appName);
        w.WriteString(appVersion);
        w.WriteString(serverId.ToString());

        w.WriteArrayStart(2);

        w.WriteInt(1);
        w.WriteInt(ProtocolVersion.Etp11.Major);
        w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision);
        w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("producer");
        w.WriteMapStart(1);
        w.WriteString("SimpleStreamer");
        w.WriteLong(7L); // DataValue union index: boolean
        w.WriteBool(true);
        w.WriteMapEnd();

        w.WriteInt(3);
        w.WriteInt(ProtocolVersion.Etp11.Major);
        w.WriteInt(ProtocolVersion.Etp11.Minor);
        w.WriteInt(ProtocolVersion.Etp11.Revision);
        w.WriteInt(ProtocolVersion.Etp11.Patch);
        w.WriteString("store");
        w.WriteMapStart(1);
        w.WriteString("MaxResponseCount");
        w.WriteLong(3L); // DataValue union index: int
        w.WriteInt(1000);
        w.WriteMapEnd();

        w.WriteArrayEnd();
        w.WriteArrayStart(0);

        return w.ToArray();
    }

    internal static ReadOnlyMemory<byte> BuildProtocolExceptionFrame(int errorCode, string message)
    {
        var w = new AvroWriter();
        w.WriteInt(0); w.WriteInt(EtpMessageType.ProtocolException); w.WriteLong(1L); w.WriteLong(2L); w.WriteInt(EtpMessageFlags.FinalPart);
        w.WriteInt(errorCode);
        w.WriteString(message);
        return w.ToArray();
    }
}
