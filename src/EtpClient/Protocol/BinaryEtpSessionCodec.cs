using System.Net.WebSockets;
using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// ETP session codec using Avro binary encoding over WebSocket binary frames.
/// This is the default codec and wraps the existing binary-only message helpers.
/// </summary>
internal sealed class BinaryEtpSessionCodec : IEtpSessionCodec
{
    public EtpMessageEncoding Encoding => EtpMessageEncoding.Binary;
    public WebSocketMessageType FrameType => WebSocketMessageType.Binary;

    // ── Protocol 0 (Core) ────────────────────────────────────────────────────

    public ReadOnlyMemory<byte> EncodeRequestSession(RequestSessionMessage message, long messageId)
        => message.EncodeFrame(messageId);

    public EtpMessageHeader DecodeHeader(ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        return EtpMessageHeader.ReadFrom(r);
    }

    public int PeekMessageType(ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        // Header fields: protocol (int), messageType (int)
        r.ReadInt(); // protocol — discard
        return r.ReadInt(); // messageType
    }

    public (EtpMessageHeader Header, NegotiatedSessionInfo Session) DecodeOpenSession(ReadOnlyMemory<byte> frame)
        => OpenSessionMessage.DecodeFrame(frame);

    public (EtpMessageHeader Header, int ErrorCode, string Message) DecodeProtocolException(ReadOnlyMemory<byte> frame)
        => ProtocolExceptionMessage.DecodeFrame(frame);

    // ── Protocol 3 (Discovery) ───────────────────────────────────────────────

    public ReadOnlyMemory<byte> EncodeGetResources(string uri, long messageId)
        => GetResourcesMessage.EncodeBinaryFrame(uri, messageId);

    public (EtpMessageHeader Header, DiscoveredResource Resource) DecodeGetResourcesResponse(ReadOnlyMemory<byte> frame)
        => GetResourcesResponseMessage.DecodeFrame(frame);
}
