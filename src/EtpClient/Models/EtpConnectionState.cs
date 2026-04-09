namespace EtpClient.Models;

/// <summary>Externally observable states of an ETP client connection lifecycle.</summary>
public enum EtpConnectionState
{
    /// <summary>No connection has been started or the previous connection has been closed.</summary>
    Closed,

    /// <summary>WebSocket handshake and ETP session negotiation are in progress.</summary>
    Connecting,

    /// <summary>The ETP session was accepted by the server and the client is ready.</summary>
    Connected,

    /// <summary>The connection attempt or session failed due to an unrecoverable error.</summary>
    Failed,

    /// <summary>The connection attempt was canceled by the caller before completion.</summary>
    Canceled,
}
