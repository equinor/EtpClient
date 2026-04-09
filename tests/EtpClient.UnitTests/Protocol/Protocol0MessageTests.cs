using EtpClient.Models;
using EtpClient.Protocol;
using Xunit;

namespace EtpClient.UnitTests.Protocol;

public sealed class Protocol0MessageTests
{
    // ── EtpMessageHeader round-trip ───────────────────────────────────────────

    [Fact]
    public void MessageHeader_RoundTrip()
    {
        var original = new EtpMessageHeader(
            Protocol: 0,
            MessageType: EtpMessageType.RequestSession,
            MessageId: 1L,
            MessageFlags: EtpMessageFlags.FinalPart);

        var writer = new AvroWriter();
        original.WriteTo(writer);
        var encoded = writer.ToArray();

        var reader = new AvroReader(encoded);
        var decoded = EtpMessageHeader.ReadFrom(reader);

        Assert.Equal(original.Protocol, decoded.Protocol);
        Assert.Equal(original.MessageType, decoded.MessageType);
        Assert.Equal(original.MessageId, decoded.MessageId);
        Assert.Equal(original.MessageFlags, decoded.MessageFlags);
    }

    // ── RequestSession encode ────────────────────────────────────────────────

    [Fact]
    public void RequestSession_Encode_ProducesValidAvroBinaryFrame()
    {
        var clientId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var msg = new RequestSessionMessage(
            applicationName: "TestApp",
            applicationVersion: "1.0",
            clientInstanceId: clientId,
            requestedProtocols: []);

        var bytes = msg.EncodeFrame(messageId: 1L);

        // Must be non-empty
        Assert.NotEmpty(bytes.ToArray());

        // Header must decode as Protocol 0, messageType 1 (RequestSession)
        var reader = new AvroReader(bytes);
        var header = EtpMessageHeader.ReadFrom(reader);
        Assert.Equal(0, header.Protocol);
        Assert.Equal(EtpMessageType.RequestSession, header.MessageType);
        Assert.Equal(1L, header.MessageId);
    }

    [Fact]
    public void RequestSession_DoesNotContainCredentialBytes()
    {
        // Ensure username:password bytes cannot possibly appear in the frame
        var msg = new RequestSessionMessage(
            applicationName: "EtpClient",
            applicationVersion: "1.0",
            clientInstanceId: Guid.NewGuid(),
            requestedProtocols: []);

        var bytes = msg.EncodeFrame(messageId: 1L).ToArray();
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        // The message should not contain anything that looks like a credential
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", text, StringComparison.OrdinalIgnoreCase);
    }

    // ── OpenSession decode ───────────────────────────────────────────────────

    [Fact]
    public void OpenSession_Decode_ExtractsExpectedFields()
    {
        var serverId = Guid.Parse("AAAABBBB-CCCC-DDDD-EEEE-FFFFFFFFFFFF");
        var frame = BuildOpenSessionFrame(serverId, "ServerApp", "2.0", "xml");

        var (header, session) = OpenSessionMessage.DecodeFrame(frame);

        Assert.Equal(EtpMessageType.OpenSession, header.MessageType);
        Assert.Equal(serverId, session.ServerInstanceId);
        Assert.Equal("ServerApp", session.ServerApplicationName);
        Assert.Equal("2.0", session.ServerApplicationVersion);
        Assert.Equal("xml", session.SupportedFormats[0]);
    }

    // ── ProtocolException decode ─────────────────────────────────────────────

    [Fact]
    public void ProtocolException_Decode_ExtractsErrorCode()
    {
        var frame = BuildProtocolExceptionFrame(errorCode: 5, message: "Unauthorized");

        var (header, code, msg) = ProtocolExceptionMessage.DecodeFrame(frame);

        Assert.Equal(EtpMessageType.ProtocolException, header.MessageType);
        Assert.Equal(5, code);
        Assert.Equal("Unauthorized", msg);
    }

    // ── helpers that build well-formed Avro frames ────────────────────────────

    private static ReadOnlyMemory<byte> BuildOpenSessionFrame(
        Guid serverId, string appName, string appVersion, string format)
    {
        var w = new AvroWriter();
        // Header: protocol=0, messageType=2, messageId=1, messageFlags=2
        w.WriteInt(0);
        w.WriteInt(EtpMessageType.OpenSession);
        w.WriteLong(1L);
        w.WriteInt(EtpMessageFlags.FinalPart);

        // Body: applicationName, applicationVersion
        w.WriteString(appName);
        w.WriteString(appVersion);
        // serverInstanceId: 16-byte fixed (UUID)
        w.WriteFixed(serverId.ToByteArray());
        // supportedProtocols: empty array
        w.WriteArrayStart(0);
        // supportedDataObjects: empty array
        w.WriteArrayStart(0);
        // supportedCompression: ""
        w.WriteString("");
        // supportedFormats: [format]
        w.WriteArrayStart(1); w.WriteString(format); w.WriteArrayEnd();
        // currentDateTime, earliestReliableTime
        w.WriteLong(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L);
        w.WriteLong(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L);
        // endpointCapabilities: empty map
        w.WriteMapStart(0);

        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildProtocolExceptionFrame(int errorCode, string message)
    {
        var w = new AvroWriter();
        w.WriteInt(0);
        w.WriteInt(EtpMessageType.ProtocolException);
        w.WriteLong(1L);
        w.WriteInt(EtpMessageFlags.FinalPart);
        // ProtocolException body: errorCode (int), message (string)
        w.WriteInt(errorCode);
        w.WriteString(message);
        return w.ToArray();
    }
}
