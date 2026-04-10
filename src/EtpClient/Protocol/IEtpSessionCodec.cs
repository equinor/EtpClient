using System.Net.WebSockets;
using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Abstracts the encoding and decoding of ETP Protocol 0 session messages,
/// enabling the session manager to support both binary and JSON encodings
/// without duplicating session-level logic.
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

    /// <summary>Encodes a complete RequestSession frame ready to send over WebSocket.</summary>
    ReadOnlyMemory<byte> EncodeRequestSession(RequestSessionMessage message, long messageId);

    /// <summary>
    /// Peeks the message type from a received frame without fully decoding it.
    /// Returns the <c>messageType</c> value from the ETP message header.
    /// </summary>
    int PeekMessageType(ReadOnlyMemory<byte> frame);

    /// <summary>
    /// Decodes a full OpenSession frame (header + body).
    /// </summary>
    (EtpMessageHeader Header, NegotiatedSessionInfo Session) DecodeOpenSession(ReadOnlyMemory<byte> frame);

    /// <summary>
    /// Decodes a full ProtocolException frame (header + body).
    /// </summary>
    (EtpMessageHeader Header, int ErrorCode, string Message) DecodeProtocolException(ReadOnlyMemory<byte> frame);
}
