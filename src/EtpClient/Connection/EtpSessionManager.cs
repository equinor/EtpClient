using System.Net.WebSockets;
using System.Text;
using EtpClient.Diagnostics;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging;

namespace EtpClient.Connection;

/// <summary>
/// Manages a single ETP session lifecycle: connect → handshake → close.
/// Thread-safety: one concurrent call to <see cref="ConnectAsync"/> or
/// <see cref="CloseAsync"/> at a time.
/// </summary>
internal sealed class EtpSessionManager
{
    private const int ReceiveBufferSize = 64 * 1024; // 64 KiB — large enough for OpenSession

    private readonly IWebSocketTransport _transport;
    private readonly ILogger _logger;

    private volatile int _state = (int)EtpConnectionState.Closed;

    public EtpConnectionState State => (EtpConnectionState)_state;

    public EtpSessionManager(IWebSocketTransport transport, ILogger logger)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EtpConnectionResult> ConnectAsync(
        EtpConnectionOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);

        Interlocked.Exchange(ref _state, (int)EtpConnectionState.Connecting);

        var host = options.EndpointUri.Host;
        EtpClientLog.Connecting(_logger, host);

        try
        {
            // Build Basic auth header — credentials used transiently, not stored
            var authHeader = BuildAuthorizationHeader(options.Username, options.Password);

            await _transport.ConnectAsync(
                options.EndpointUri,
                authHeader,
                options.KeepAliveInterval,
                ct).ConfigureAwait(false);

            // Send RequestSession
            var requestFrame = new RequestSessionMessage(
                "EtpClient",
                "1.0.0",
                options.ClientInstanceId,
                options.RequestedProtocols)
                .EncodeFrame(messageId: 1L);

            await _transport.SendAsync(requestFrame, endOfMessage: true, ct).ConfigureAwait(false);

            // Await response
            var responseFrame = await ReceiveFullFrameAsync(ct).ConfigureAwait(false);

            return ProcessResponse(responseFrame, host, options);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Canceled);
            throw;
        }
        catch (WebSocketException wsEx)
        {
            var httpStatus = _transport.HttpStatusCode;
            if (httpStatus == 401)
            {
                Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
                EtpClientLog.AuthenticationFailed(_logger, host, 401);
                throw new EtpConnectionException(
                    EtpConnectionFailureCategory.Authentication,
                    "Authentication rejected by server (HTTP 401).",
                    innerException: wsEx,
                    httpStatusCode: 401);
            }

            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
            EtpClientLog.SessionError(_logger, host, EtpConnectionFailureCategory.Transport, null);
            throw new EtpConnectionException(
                EtpConnectionFailureCategory.Transport,
                "WebSocket transport error during connection.",
                innerException: wsEx);
        }
        catch (EtpConnectionException ex)
        {
            EtpClientLog.SessionError(_logger, host, ex.Category, ex.EtpErrorCode);
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
            throw;
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
            EtpClientLog.SessionError(_logger, host, EtpConnectionFailureCategory.Transport, null);
            throw new EtpConnectionException(
                EtpConnectionFailureCategory.Transport,
                "Unexpected error during ETP connection.",
                innerException: ex);
        }
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        if (State != EtpConnectionState.Connected)
        {
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Closed);
            return;
        }

        try
        {
            await _transport.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client closing session",
                ct).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort close; suppress errors during shutdown
        }
        finally
        {
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Closed);
        }
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private EtpConnectionResult ProcessResponse(
        ReadOnlyMemory<byte> frame,
        string host,
        EtpConnectionOptions options)
    {
        var reader = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(reader);

        switch (header.MessageType)
        {
            case EtpMessageType.OpenSession:
            {
                var (_, sessionInfo) = OpenSessionMessage.DecodeFrame(frame);
                Interlocked.Exchange(ref _state, (int)EtpConnectionState.Connected);
                EtpClientLog.SessionEstablished(_logger, sessionInfo.ServerApplicationName, host);
                return new EtpConnectionResult { Session = sessionInfo, ConnectedAtUtc = DateTimeOffset.UtcNow, EndpointHost = host };
            }
            case EtpMessageType.ProtocolException:
            {
                var (_, errorCode, message) = ProtocolExceptionMessage.DecodeFrame(frame);
                var ex = new EtpConnectionException(
                    EtpConnectionFailureCategory.Protocol,
                    $"Server rejected session: {message}",
                    etpErrorCode: errorCode);
                Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
                throw ex;
            }
            default:
                Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
                throw new EtpConnectionException(
                    EtpConnectionFailureCategory.Protocol,
                    $"Unexpected message type {header.MessageType} during handshake.");
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReceiveFullFrameAsync(CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        var result = await _transport.ReceiveAsync(buffer, ct).ConfigureAwait(false);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            throw new EtpConnectionException(
                EtpConnectionFailureCategory.Transport,
                "Server closed the WebSocket during handshake.");
        }

        // For typical ETP messages the full frame fits in one receive
        if (result.EndOfMessage)
            return buffer.AsMemory(0, result.Count);

        // Multi-fragment fallback (uncommon for handshake messages)
        using var ms = new System.IO.MemoryStream();
        ms.Write(buffer, 0, result.Count);

        while (!result.EndOfMessage)
        {
            result = await _transport.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new EtpConnectionException(
                    EtpConnectionFailureCategory.Transport,
                    "Server closed the WebSocket mid-message.");
            ms.Write(buffer, 0, result.Count);
        }

        return ms.ToArray();
    }

    private static string BuildAuthorizationHeader(string username, string password)
    {
        var credentials = $"{username}:{password}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        return $"Basic {encoded}";
    }
}
