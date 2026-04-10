namespace EtpClient.Models;

/// <summary>
/// Specifies the ETP message encoding used for a session.
/// Reference: ETP v1.1 specification — the Avro specification supports both binary and
/// JSON encoding of data; ETP supports both.
/// </summary>
public enum EtpMessageEncoding
{
    /// <summary>
    /// Avro binary encoding over WebSocket binary frames.
    /// This is the default and preserves the behavior of existing callers.
    /// </summary>
    Binary = 0,

    /// <summary>
    /// Avro JSON encoding over WebSocket text frames.
    /// Use when the target endpoint requires or prefers JSON-encoded ETP messages.
    /// </summary>
    Json = 1,
}
