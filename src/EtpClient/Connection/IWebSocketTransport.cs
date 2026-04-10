using System.Net.WebSockets;

namespace EtpClient.Connection;

/// <summary>
/// Abstracts the underlying WebSocket transport to enable unit testing.
/// </summary>
internal interface IWebSocketTransport : IAsyncDisposable
{
    /// <summary>Gets the current state of the underlying WebSocket.</summary>
    WebSocketState State { get; }

    /// <summary>
    /// For a failed HTTP upgrade, the HTTP status code returned by the server.
    /// <c>null</c> when the connection succeeded or before a connection attempt.
    /// </summary>
    int? HttpStatusCode { get; }

    /// <summary>
    /// Connects to the specified URI, attaching the given authorization header
    /// and ETP sub-protocol before the handshake.
    /// </summary>
    Task ConnectAsync(Uri uri, string authorizationHeaderValue, TimeSpan keepAliveInterval, CancellationToken ct);

    /// <summary>Sends a frame to the server using the specified WebSocket message type.</summary>
    ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct);

    /// <summary>Receives the next incoming frame into <paramref name="buffer"/>.</summary>
    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct);

    /// <summary>Sends a WebSocket close frame.</summary>
    Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken ct);
}
