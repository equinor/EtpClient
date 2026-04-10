using System.Net.WebSockets;
using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Abstracts the encoding and decoding of ETP session messages,
/// enabling the session manager to support both binary and JSON encodings
/// without duplicating session-level logic.
/// Covers Protocol 0 (Core) session establishment and Protocol 3 (Discovery).
/// </summary>
internal interface IEtpSessionCodec
{
    /// <summary>The ETP message encoding this codec implements.</summary>
    EtpMessageEncoding Encoding { get; }

    /// <summary>
    /// The WebSocket frame type that corresponds to this codec's encoding.
    /// Binary encoding uses <see cref="WebSocketMessageType.Binary"/>;
    /// JSON encoding uses <see cref="WebSocketMessageType.Text"/>.
    /// </summary>
    WebSocketMessageType FrameType { get; }

    // ── Protocol 0 (Core) ────────────────────────────────────────────────────

    /// <summary>Encodes a complete RequestSession frame ready to send over WebSocket.</summary>
    ReadOnlyMemory<byte> EncodeRequestSession(RequestSessionMessage message, long messageId);

    /// <summary>
    /// Decodes only the message header from a received frame (does not advance past the header).
    /// Used to dispatch on both <c>protocol</c> and <c>messageType</c> before parsing the body.
    /// </summary>
    EtpMessageHeader DecodeHeader(ReadOnlyMemory<byte> frame);

    /// <summary>
    /// Peeks the message type from a received frame without fully decoding it.
    /// Returns the <c>messageType</c> value from the ETP message header.
    /// </summary>
    int PeekMessageType(ReadOnlyMemory<byte> frame);

    /// <summary>Decodes a full OpenSession frame (header + body).</summary>
    (EtpMessageHeader Header, NegotiatedSessionInfo Session) DecodeOpenSession(ReadOnlyMemory<byte> frame);

    /// <summary>Decodes a full ProtocolException frame (header + body).</summary>
    (EtpMessageHeader Header, int ErrorCode, string Message) DecodeProtocolException(ReadOnlyMemory<byte> frame);

    // ── Protocol 3 (Discovery) ───────────────────────────────────────────────

    /// <summary>
    /// Encodes a complete GetResources frame for the given URI ready to send over WebSocket.
    /// </summary>
    ReadOnlyMemory<byte> EncodeGetResources(string uri, long messageId);

    /// <summary>
    /// Decodes a full GetResourcesResponse frame (header + single resource body).
    /// Each multipart response message contains exactly one resource.
    /// </summary>
    (EtpMessageHeader Header, DiscoveredResource Resource) DecodeGetResourcesResponse(ReadOnlyMemory<byte> frame);
}
