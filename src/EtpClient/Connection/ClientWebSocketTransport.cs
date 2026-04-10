using System.Net.WebSockets;

namespace EtpClient.Connection;

/// <summary>
/// Production implementation that wraps <see cref="ClientWebSocket"/>.
/// Sets the <c>Authorization</c> header and the ETP sub-protocol before connecting.
/// </summary>
internal sealed class ClientWebSocketTransport : IWebSocketTransport
{
    // private const string EtpSubProtocol = "etp12.energistics.org";
    private const string EtpSubProtocol = "energistics-tp";

    private readonly ClientWebSocket _ws = new();

    public WebSocketState State => _ws.State;

    public int? HttpStatusCode
    {
        get
        {
            // Available when CollectHttpResponseDetails = true and the upgrade failed
            var status = _ws.HttpStatusCode;
            return status == 0 ? null : (int)status;
        }
    }

    public Task ConnectAsync(
        Uri uri,
        string authorizationHeaderValue,
        TimeSpan keepAliveInterval,
        CancellationToken ct)
    {
        _ws.Options.SetRequestHeader("Authorization", authorizationHeaderValue);
        _ws.Options.AddSubProtocol(EtpSubProtocol);
        _ws.Options.KeepAliveInterval = keepAliveInterval;
        _ws.Options.CollectHttpResponseDetails = true;

        return _ws.ConnectAsync(uri, ct);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct)
        => _ws.SendAsync(buffer, messageType, endOfMessage, ct);

    public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct)
        => _ws.ReceiveAsync(buffer, ct);

    public Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken ct)
        => _ws.CloseOutputAsync(status, description, ct);

    public ValueTask DisposeAsync()
    {
        _ws.Dispose();
        return ValueTask.CompletedTask;
    }
}
