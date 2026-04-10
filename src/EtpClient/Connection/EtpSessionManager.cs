using System.Net.WebSockets;
using System.Text;
using EtpClient.Diagnostics;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging;

namespace EtpClient.Connection;

/// <summary>
/// Manages a single ETP session lifecycle: connect → handshake → discovery → close.
/// Thread-safety: one concurrent call to <see cref="ConnectAsync"/> or
/// <see cref="CloseAsync"/> at a time.
/// </summary>
internal sealed class EtpSessionManager
{
    private const int ReceiveBufferSize = 64 * 1024; // 64 KiB — large enough for OpenSession

    private readonly IWebSocketTransport _transport;
    private readonly ILogger _logger;

    private volatile int _state = (int)EtpConnectionState.Closed;

    // Set after a successful Protocol 0 handshake; used by post-session operations.
    private IEtpSessionCodec? _codec;
    private string _host = string.Empty;
    private long _nextMessageId = 1; // starts at 1; increment atomically before sending
    private NegotiatedSessionInfo? _sessionInfo;

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

        // Select codec based on the caller's encoding choice
        IEtpSessionCodec codec = options.MessageEncoding switch
        {
            EtpMessageEncoding.Json => new JsonEtpSessionCodec(),
            _ => new BinaryEtpSessionCodec(),
        };

        EtpClientLog.EncodingSelected(_logger, host, options.MessageEncoding);

        try
        {
            // Build Basic auth header — credentials used transiently, not stored
            var authHeader = BuildAuthorizationHeader(options.Username, options.Password);

            await _transport.ConnectAsync(
                options.EndpointUri,
                authHeader,
                options.KeepAliveInterval,
                ct).ConfigureAwait(false);

            // Send RequestSession using selected codec
            var requestMessage = new RequestSessionMessage(
                "EtpClient",
                "1.0.0",
                options.ClientInstanceId,
                options.RequestedProtocols);
            var requestFrame = codec.EncodeRequestSession(requestMessage, messageId: 1L);

            await _transport.SendAsync(requestFrame, codec.FrameType, endOfMessage: true, ct).ConfigureAwait(false);

            // Await response
            var responseFrame = await ReceiveFullFrameAsync(codec.FrameType, ct).ConfigureAwait(false);

            var result = ProcessResponse(responseFrame, host, options, codec);
            _codec = codec;
            _host = host;
            _sessionInfo = result.Session;
            return result;
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

    public async Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct)
    {
        if (State != EtpConnectionState.Connected || _codec is null)
            throw new InvalidOperationException("Discovery requires an active Connected session.");

        if (_sessionInfo is null || !_sessionInfo.SupportsDiscovery)
        {
            throw new EtpDiscoveryException(
                "Discovery protocol (3) was not negotiated by the server.",
                uri,
                etpErrorCode: null);
        }

        var codec = _codec;
        var host = _host;
        var messageId = Interlocked.Increment(ref _nextMessageId);
        EtpClientLog.DiscoveryStarted(_logger, host, uri);

        var requestFrame = codec.EncodeGetResources(uri, messageId);
        await _transport.SendAsync(requestFrame, codec.FrameType, endOfMessage: true, ct).ConfigureAwait(false);

        var resources = new List<DiscoveredResource>();
        var wasEmptyAcknowledged = false;

        while (true)
        {
            var responseFrame = await ReceiveFullFrameAsync(codec.FrameType, ct).ConfigureAwait(false);
            var header = codec.DecodeHeader(responseFrame);

            if (header.CorrelationId != messageId)
                continue;

            if (header.Protocol == EtpProtocol.Discovery &&
                header.MessageType == EtpDiscoveryMessageType.GetResourcesResponse)
            {
                var (_, resource) = codec.DecodeGetResourcesResponse(responseFrame);
                resources.Add(resource);
                if ((header.MessageFlags & EtpMessageFlags.FinalPart) != 0)
                    break;
            }
            else if (header.Protocol == EtpProtocol.Discovery &&
                     header.MessageType == EtpMessageType.Acknowledge)
            {
                wasEmptyAcknowledged = true;
                break;
            }
            else if (header.MessageType == EtpMessageType.ProtocolException)
            {
                var (_, errorCode, message) = codec.DecodeProtocolException(responseFrame);
                var detail = string.IsNullOrWhiteSpace(message)
                    ? $"ETP error code {errorCode}"
                    : $"{message} (ETP error code {errorCode})";
                EtpClientLog.DiscoveryFailed(_logger, host, uri, errorCode);
                throw new EtpDiscoveryException(
                    $"Discovery failed for URI '{uri}': {detail}", uri, errorCode);
            }
            else
            {
                throw new EtpDiscoveryException(
                    $"Unexpected message (protocol={header.Protocol}, type={header.MessageType}) during discovery for URI '{uri}'.",
                    uri,
                    etpErrorCode: null);
            }
        }

        if (wasEmptyAcknowledged)
            EtpClientLog.DiscoveryEmpty(_logger, host, uri);
        else
            EtpClientLog.DiscoveryCompleted(_logger, host, uri, resources.Count);

        return new DiscoveryResult
        {
            RequestedUri = uri,
            Resources = resources,
            WasEmptyAcknowledged = wasEmptyAcknowledged,
            MessageEncoding = codec.Encoding,
        };
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private EtpConnectionResult ProcessResponse(
        ReadOnlyMemory<byte> frame,
        string host,
        EtpConnectionOptions options,
        IEtpSessionCodec codec)
    {
        int messageType;
        try
        {
            messageType = codec.PeekMessageType(frame);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
            EtpClientLog.SessionError(_logger, host, EtpConnectionFailureCategory.Protocol, null);
            throw new EtpConnectionException(
                EtpConnectionFailureCategory.Protocol,
                $"Could not decode server response using {options.MessageEncoding} encoding. " +
                "The server may use a different encoding.",
                innerException: ex);
        }

        switch (messageType)
        {
            case EtpMessageType.OpenSession:
            {
                var (_, sessionInfo) = codec.DecodeOpenSession(frame);
                Interlocked.Exchange(ref _state, (int)EtpConnectionState.Connected);
                EtpClientLog.SessionEstablished(_logger, sessionInfo.ServerApplicationName, host);
                return new EtpConnectionResult
                {
                    Session = sessionInfo,
                    ConnectedAtUtc = DateTimeOffset.UtcNow,
                    EndpointHost = host,
                    MessageEncoding = options.MessageEncoding,
                };
            }
            case EtpMessageType.ProtocolException:
            {
                var (_, errorCode, message) = codec.DecodeProtocolException(frame);
                    var detail = string.IsNullOrWhiteSpace(message)
                        ? $"ETP error code {errorCode}"
                        : $"{message} (ETP error code {errorCode})";
                    var ex = new EtpConnectionException(
                    EtpConnectionFailureCategory.Protocol,
                    $"Server rejected session: {detail}",
                    etpErrorCode: errorCode);
                Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
                throw ex;
            }
            default:
                Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
                throw new EtpConnectionException(
                    EtpConnectionFailureCategory.Protocol,
                    $"Unexpected message type {messageType} during handshake.");
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReceiveFullFrameAsync(
        System.Net.WebSockets.WebSocketMessageType expectedFrameType,
        CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        var result = await _transport.ReceiveAsync(buffer, ct).ConfigureAwait(false);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            throw new EtpConnectionException(
                EtpConnectionFailureCategory.Transport,
                "Server closed the WebSocket during handshake.");
        }

        // Detect encoding mismatch: server responded with a different frame type than selected
        if (result.MessageType != expectedFrameType)
        {
            throw new EtpConnectionException(
                EtpConnectionFailureCategory.Protocol,
                $"Server responded with {result.MessageType} frame but client selected " +
                $"{(expectedFrameType == WebSocketMessageType.Binary ? EtpMessageEncoding.Binary : EtpMessageEncoding.Json)} encoding. " +
                "The server may not support the selected encoding.");
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
