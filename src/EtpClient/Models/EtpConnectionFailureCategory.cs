namespace EtpClient.Models;

/// <summary>Distinguishes the root cause category of an ETP connection failure.</summary>
public enum EtpConnectionFailureCategory
{
    /// <summary>One or more required connection options were missing or malformed.</summary>
    Validation,

    /// <summary>The server rejected the credentials (e.g. HTTP 401 during WebSocket upgrade).</summary>
    Authentication,

    /// <summary>A network or transport error prevented the WebSocket connection from opening.</summary>
    Transport,

    /// <summary>The ETP session negotiation did not complete successfully (e.g. ProtocolException received).</summary>
    Protocol,

    /// <summary>The connection attempt was canceled by the caller via a <see cref="System.Threading.CancellationToken"/>.</summary>
    Cancellation,
}
