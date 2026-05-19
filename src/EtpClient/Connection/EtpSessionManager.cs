using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using EtpClient.Diagnostics;
using EtpClient.Instrumentation;
using EtpClient.Models;
using EtpClient.Protocol;
using Microsoft.Extensions.Logging;

namespace EtpClient.Connection;

/// <summary>
/// Manages a single ETP session lifecycle: connect → handshake → discovery → close.
/// Thread-safety: one concurrent call to <see cref="ConnectAsync"/> or
/// <see cref="CloseAsync"/> at a time.
/// </summary>
internal sealed class EtpSessionManager : IAsyncDisposable
{
    private const int ReceiveBufferSize = 64 * 1024; // 64 KiB — large enough for OpenSession
    private const int Protocol1StartMaxMessageRate = 1;
    private const int Protocol1StartMaxDataItems = int.MaxValue;

    private readonly IWebSocketTransport _transport;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _channelStreamingStartLock = new(1, 1);

    private volatile int _state = (int)EtpConnectionState.Closed;
    private int _disposed;

    // Set after a successful Protocol 0 handshake; used by post-session operations.
    private IEtpSessionCodec? _codec;
    private string _host = string.Empty;
    private int _port;
    private long _nextMessageId = 1; // starts at 1; increment atomically before sending
    private NegotiatedSessionInfo? _sessionInfo;
    private bool _channelStreamingProtocolStarted;

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
        var port = options.EndpointUri.Port;
        EtpClientLog.Connecting(_logger, host);

        // Select codec based on the caller's encoding choice
        IEtpSessionCodec codec = options.MessageEncoding switch
        {
            EtpMessageEncoding.Json => new JsonEtpSessionCodec(),
            _ => new BinaryEtpSessionCodec(),
        };

        EtpClientLog.EncodingSelected(_logger, host, options.MessageEncoding);

        using var activity = EtpInstrumentation.StartConnectActivity(host, port);
        var sw = Stopwatch.StartNew();
        Exception? caughtEx = null;
        int? caughtEtpErrorCode = null;
        try
        {
            // Build Basic auth header — credentials used transiently, not stored
            var authHeader = BuildAuthorizationHeader(options.Username, options.Password);

            // Assign _host/_port before the first send so metric tags are correct
            // even for frames sent during the handshake itself.
            _host = host;
            _port = port;

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

            await SendFrameAsync(requestFrame, codec.FrameType, ct).ConfigureAwait(false);

            // Await response
            var responseFrame = await ReceiveFullFrameAsync(codec.FrameType, ct).ConfigureAwait(false);

            var result = ProcessResponse(responseFrame, host, options, codec);
            _codec = codec;
            _sessionInfo = result.Session;

            activity?.SetTag("etp.encoding", options.MessageEncoding == EtpMessageEncoding.Json ? "json" : "binary");
            activity?.SetStatus(ActivityStatusCode.Ok);
            EtpInstrumentation.ActiveConnections.Add(1,
                new KeyValuePair<string, object?>("server.address", host));
            return result;
        }
        catch (OperationCanceledException oce)
        {
            caughtEx = oce;
            activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
            activity?.SetTag("error.type", oce.GetType().FullName);
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Canceled);
            throw;
        }
        catch (WebSocketException wsEx)
        {
            caughtEx = wsEx;
            var httpStatus = _transport.HttpStatusCode;
            activity?.SetStatus(ActivityStatusCode.Error, wsEx.Message);
            activity?.SetTag("error.type", wsEx.GetType().FullName);
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
            caughtEx = ex;
            caughtEtpErrorCode = ex.EtpErrorCode;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);
            if (ex.EtpErrorCode.HasValue)
                activity?.SetTag("etp.error_code", ex.EtpErrorCode.Value);
            EtpClientLog.SessionError(_logger, host, ex.Category, ex.EtpErrorCode);
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
            throw;
        }
        catch (Exception ex)
        {
            caughtEx = ex;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Failed);
            EtpClientLog.SessionError(_logger, host, EtpConnectionFailureCategory.Transport, null);
            throw new EtpConnectionException(
                EtpConnectionFailureCategory.Transport,
                "Unexpected error during ETP connection.",
                innerException: ex);
        }
        finally
        {
            sw.Stop();
            EtpInstrumentation.OperationDuration.Record(
                sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("etp.operation", "connect"),
                new KeyValuePair<string, object?>("server.address", host));
            if (caughtEx is not null)
                EtpInstrumentation.RecordOperationError("connect", host, caughtEtpErrorCode);
        }
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        if (State != EtpConnectionState.Connected)
        {
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Closed);
            return;
        }

        var host = _host;
        var port = _port;
        using var activity = EtpInstrumentation.StartOperationActivity("etp.disconnect", host, port);
        var sw = Stopwatch.StartNew();
        try
        {
            await _transport.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client closing session",
                ct).ConfigureAwait(false);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch
        {
            // Best-effort close; suppress errors during shutdown
        }
        finally
        {
            sw.Stop();
            Interlocked.Exchange(ref _state, (int)EtpConnectionState.Closed);
            EtpInstrumentation.ActiveConnections.Add(-1,
                new KeyValuePair<string, object?>("server.address", host));
            EtpInstrumentation.OperationDuration.Record(
                sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("etp.operation", "disconnect"),
                new KeyValuePair<string, object?>("server.address", host));
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

        using var activity = EtpInstrumentation.StartOperationActivity("etp.discovery", host, _port);
        activity?.SetTag("etp.uri", EtpInstrumentation.TruncateAttribute(uri));
        var sw = Stopwatch.StartNew();
        Exception? caughtEx = null;
        int? caughtEtpErrorCode = null;

        try
        {
            var requestFrame = codec.EncodeGetResources(uri, messageId);
            await SendFrameAsync(requestFrame, codec.FrameType, ct).ConfigureAwait(false);

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

            activity?.SetTag("etp.resource_count", resources.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new DiscoveryResult
            {
                RequestedUri = uri,
                Resources = resources,
                WasEmptyAcknowledged = wasEmptyAcknowledged,
                MessageEncoding = codec.Encoding,
            };
        }
        catch (EtpDiscoveryException ex)
        {
            caughtEx = ex;
            caughtEtpErrorCode = ex.EtpErrorCode;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);
            if (ex.EtpErrorCode.HasValue)
                activity?.SetTag("etp.error_code", ex.EtpErrorCode.Value);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            caughtEx = ex;
            activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
            activity?.SetTag("error.type", "System.OperationCanceledException");
            throw;
        }
        finally
        {
            sw.Stop();
            EtpInstrumentation.OperationDuration.Record(
                sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("etp.operation", "discover"),
                new KeyValuePair<string, object?>("server.address", host));
            if (caughtEx is not null)
                EtpInstrumentation.RecordOperationError("discover", host, caughtEtpErrorCode);
        }
    }

    public async Task<ChannelDescriptionResult> DescribeChannelsAsync(
        IReadOnlyList<string> uris,
        CancellationToken ct)
    {
        if (State != EtpConnectionState.Connected || _codec is null)
            throw new InvalidOperationException("DescribeChannels requires an active Connected session.");

        var codec = _codec;
        var host = _host;
        var messageId = Interlocked.Increment(ref _nextMessageId);
        var target = string.Join(", ", uris);
        EtpClientLog.DescribeChannelsStarted(_logger, host, target);

        using var activity = EtpInstrumentation.StartOperationActivity("etp.channel.describe", host, _port);
        activity?.SetTag("etp.channel_target", EtpInstrumentation.TruncateAttribute(uris.Count > 0 ? uris[0] : target));
        var sw = Stopwatch.StartNew();
        Exception? caughtEx = null;
        int? caughtEtpErrorCode = null;

        try
        {
            await EnsureChannelStreamingProtocolStartedAsync(codec, ct).ConfigureAwait(false);

            var requestFrame = codec.EncodeChannelDescribe(uris, messageId);
            await SendFrameAsync(requestFrame, codec.FrameType, ct).ConfigureAwait(false);

            var channels = new List<ChannelDefinition>();
            var wasMultipart = false;
            var initialChannelCount = 0;

            while (true)
            {
                var responseFrame = await ReceiveFullFrameAsync(codec.FrameType, ct).ConfigureAwait(false);
                var header = codec.DecodeHeader(responseFrame);

                // Ignore messages correlated to other outstanding requests
                if (header.CorrelationId != messageId)
                    continue;

                if (header.Protocol == EtpProtocol.ChannelStreaming &&
                    header.MessageType == EtpChannelStreamingMessageType.ChannelMetadata)
                {
                    var (_, channelsBatch) = codec.DecodeChannelMetadata(responseFrame);
                    channels.AddRange(channelsBatch);
                    if (channels.Count > initialChannelCount)
                        wasMultipart = true;
                    if ((header.MessageFlags & EtpMessageFlags.FinalPart) != 0)
                        break;
                    initialChannelCount = channels.Count;
                }
                else if (header.MessageType == EtpMessageType.ProtocolException)
                {
                    var (_, errorCode, message) = codec.DecodeProtocolException(responseFrame);
                    var detail = string.IsNullOrWhiteSpace(message)
                        ? $"ETP error code {errorCode}"
                        : $"{message} (ETP error code {errorCode})";
                    EtpClientLog.DescribeChannelsFailed(_logger, host, target, errorCode);
                    throw new EtpChannelStreamingException(
                        $"ChannelDescribe failed for '{target}': {detail}", target, errorCode);
                }
                else
                {
                    throw new EtpChannelStreamingException(
                        $"Unexpected message (protocol={header.Protocol}, type={header.MessageType}) during ChannelDescribe for '{target}'.",
                        target,
                        etpErrorCode: null);
                }
            }

            EtpClientLog.DescribeChannelsCompleted(_logger, host, target, channels.Count);

            activity?.SetTag("etp.channel_count", channels.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new ChannelDescriptionResult
            {
                RequestedUris = uris,
                Channels = channels,
                MessageEncoding = codec.Encoding,
                WasMultipart = wasMultipart,
                State = ChannelDescriptionState.Completed,
            };
        }
        catch (EtpChannelStreamingException ex)
        {
            caughtEx = ex;
            caughtEtpErrorCode = ex.EtpErrorCode;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);
            if (ex.EtpErrorCode.HasValue)
                activity?.SetTag("etp.error_code", ex.EtpErrorCode.Value);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            caughtEx = ex;
            activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
            activity?.SetTag("error.type", "System.OperationCanceledException");
            throw;
        }
        finally
        {
            sw.Stop();
            EtpInstrumentation.OperationDuration.Record(
                sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("etp.operation", "channel.describe"),
                new KeyValuePair<string, object?>("server.address", host));
            if (caughtEx is not null)
                EtpInstrumentation.RecordOperationError("channel.describe", host, caughtEtpErrorCode);
        }
    }

    /// <summary>
    /// Starts live Protocol 1 channel streaming and yields <see cref="ChannelEvent"/> instances
    /// as the producer sends data, change, status, or remove messages.
    /// The enumeration completes when all subscribed channels have been individually removed
    /// by the server, or when the cancellation token is fired.
    /// A <c>ProtocolException</c> causes an <see cref="EtpChannelStreamingException"/>.
    /// </summary>
    public async IAsyncEnumerable<ChannelEvent> StartChannelStreamingAsync(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (State != EtpConnectionState.Connected || _codec is null)
            throw new InvalidOperationException("StartChannelStreaming requires an active Connected session.");

        var codec = _codec;
        var host = _host;
        var messageId = Interlocked.Increment(ref _nextMessageId);
        EtpClientLog.StreamingStarted(_logger, host, subscriptions.Count);
        // Deduplicated set of channel IDs we expect the server to remove before completing.
        var remainingChannelIds = new HashSet<long>(subscriptions.Select(s => s.ChannelId));

        await EnsureChannelStreamingProtocolStartedAsync(codec, ct).ConfigureAwait(false);

        var requestFrame = codec.EncodeChannelStreamingStart(subscriptions, messageId);
        await SendFrameAsync(requestFrame, codec.FrameType, ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            ReadOnlyMemory<byte> responseFrame;
            try
            {
                responseFrame = await ReceiveFullFrameAsync(codec.FrameType, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            var header = codec.DecodeHeader(responseFrame);

            if (header.Protocol == EtpProtocol.ChannelStreaming &&
                header.MessageType == EtpChannelStreamingMessageType.ChannelData)
            {
                IReadOnlyList<ChannelDataItem> items;
                try
                {
                    (_, items) = codec.DecodeChannelData(responseFrame);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
                {
                    throw new EtpChannelStreamingException(
                        $"Failed to decode ChannelData frame: {ex.Message}",
                        "(live stream)",
                        etpErrorCode: null,
                        innerException: ex);
                }
                yield return new ChannelEvent
                {
                    Kind = ChannelEventKind.Data,
                    DataItems = items,
                };
            }
            else if (header.Protocol == EtpProtocol.ChannelStreaming &&
                     header.MessageType == EtpChannelStreamingMessageType.ChannelDataChange)
            {
                var (_, chanId, startIdx, endIdx) = codec.DecodeChannelDataChange(responseFrame);
                yield return new ChannelEvent
                {
                    Kind = ChannelEventKind.DataChange,
                    ChannelId = chanId,
                    StartIndex = startIdx,
                    EndIndex = endIdx,
                };
            }
            else if (header.Protocol == EtpProtocol.ChannelStreaming &&
                     header.MessageType == EtpChannelStreamingMessageType.ChannelStatusChange)
            {
                var (_, chanId, newStatus) = codec.DecodeChannelStatusChange(responseFrame);
                yield return new ChannelEvent
                {
                    Kind = ChannelEventKind.StatusChange,
                    ChannelId = chanId,
                    NewStatus = newStatus,
                };
            }
            else if (header.Protocol == EtpProtocol.ChannelStreaming &&
                     header.MessageType == EtpChannelStreamingMessageType.ChannelRemove)
            {
                var (_, chanId, reason) = codec.DecodeChannelRemove(responseFrame);
                EtpClientLog.StreamingChannelRemoved(_logger, host, chanId);
                remainingChannelIds.Remove(chanId); // no-op for unsubscribed or duplicate removes
                yield return new ChannelEvent
                {
                    Kind = ChannelEventKind.Remove,
                    ChannelId = chanId,
                    RemoveReason = reason,
                };
                if (remainingChannelIds.Count == 0)
                    yield break; // all subscribed channels removed by server
            }
            else if (header.MessageType == EtpMessageType.ProtocolException)
            {
                var (_, errorCode, message) = codec.DecodeProtocolException(responseFrame);
                var detail = string.IsNullOrWhiteSpace(message)
                    ? $"ETP error code {errorCode}"
                    : $"{message} (ETP error code {errorCode})";
                EtpClientLog.StreamingFailed(_logger, host, errorCode);
                throw new EtpChannelStreamingException(
                    $"ChannelStreaming failed: {detail}", "(live stream)", errorCode);
            }
            // Other frame types (e.g. unrelated protocol messages) are ignored
        }
    }

    /// <summary>
    /// Sends <c>ChannelStreamingStop</c> for the specified channel IDs.
    /// Does not close the session; the session remains <see cref="EtpConnectionState.Connected"/>.
    /// </summary>
    public async Task StopChannelStreamingAsync(
        IReadOnlyList<long> channelIds,
        CancellationToken ct)
    {
        if (State != EtpConnectionState.Connected || _codec is null)
            throw new InvalidOperationException("StopChannelStreaming requires an active Connected session.");

        var codec = _codec;
        var messageId = Interlocked.Increment(ref _nextMessageId);
        var frame = codec.EncodeChannelStreamingStop(channelIds, messageId);
        await SendFrameAsync(frame, codec.FrameType, ct).ConfigureAwait(false);
        EtpClientLog.StreamingStopped(_logger, _host, channelIds.Count);
    }

    /// <summary>
    /// Sends a <c>ChannelRangeRequest</c> and streams each <see cref="ChannelDataItem"/>
    /// as its <c>ChannelData</c> frame arrives. Enumeration ends on the final-part frame.
    /// </summary>
    public async IAsyncEnumerable<ChannelDataItem> RequestChannelRangeAsync(
        ChannelRangeRequestModel request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (State != EtpConnectionState.Connected || _codec is null)
            throw new InvalidOperationException("RequestChannelRange requires an active Connected session.");

        var codec = _codec;
        var host = _host;
        var messageId = Interlocked.Increment(ref _nextMessageId);
        EtpClientLog.RangeRequestStarted(_logger, host, request.ChannelIds.Count,
            DateTimeOffset.FromUnixTimeMilliseconds(request.FromIndex).ToString("u"),
            DateTimeOffset.FromUnixTimeMilliseconds(request.ToIndex).ToString("u"));

        using var activity = EtpInstrumentation.StartOperationActivity("etp.channel.range_request", host, _port);
        activity?.SetTag("etp.channel_count", request.ChannelIds.Count);
        var sw = Stopwatch.StartNew();
        bool protocolError = false;
        int? etpErrorCode = null;

        try
        {
            await EnsureChannelStreamingProtocolStartedAsync(codec, ct).ConfigureAwait(false);

            var ranges = new[]
            {
                new ChannelRangeInfoWire(request.ChannelIds, request.FromIndex, request.ToIndex),
            };
            var requestFrame = codec.EncodeChannelRangeRequest(ranges, messageId);
            await SendFrameAsync(requestFrame, codec.FrameType, ct).ConfigureAwait(false);

            var sampleCount = 0;

            while (true)
            {
                ReadOnlyMemory<byte> responseFrame;
                try
                {
                    responseFrame = await ReceiveFullFrameAsync(codec.FrameType, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                var header = codec.DecodeHeader(responseFrame);

                // Ignore messages not correlated to this request
                if (header.CorrelationId != messageId)
                    continue;

                if (header.Protocol == EtpProtocol.ChannelStreaming &&
                    header.MessageType == EtpChannelStreamingMessageType.ChannelData)
                {
                    IReadOnlyList<ChannelDataItem> items;
                    try
                    {
                        (_, items) = codec.DecodeChannelData(responseFrame);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
                    {
                        protocolError = true;
                        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        activity?.SetTag("error.type", ex.GetType().FullName);
                        throw new EtpChannelStreamingException(
                            $"Failed to decode ChannelData frame: {ex.Message}",
                            "(range request)",
                            etpErrorCode: null,
                            innerException: ex);
                    }

                    foreach (var item in items)
                    {
                        yield return item;
                        sampleCount++;
                    }

                    if ((header.MessageFlags & EtpMessageFlags.FinalPart) != 0)
                    {
                        EtpClientLog.RangeRequestCompleted(_logger, host, request.ChannelIds.Count, sampleCount);
                        yield break;
                    }
                }
                else if (header.MessageType == EtpMessageType.ProtocolException)
                {
                    var (_, errorCode, message) = codec.DecodeProtocolException(responseFrame);
                    var detail = string.IsNullOrWhiteSpace(message)
                        ? $"ETP error code {errorCode}"
                        : $"{message} (ETP error code {errorCode})";
                    EtpClientLog.RangeRequestFailed(_logger, host, errorCode);
                    protocolError = true;
                    etpErrorCode = errorCode;
                    activity?.SetStatus(ActivityStatusCode.Error, detail);
                    activity?.SetTag("error.type", typeof(EtpChannelStreamingException).FullName);
                    activity?.SetTag("etp.error_code", errorCode);
                    throw new EtpChannelStreamingException(
                        $"ChannelRangeRequest failed: {detail}", "(range request)", errorCode);
                }
                else
                {
                    var errMsg = $"Unexpected message (protocol={header.Protocol}, type={header.MessageType}) during ChannelRangeRequest.";
                    protocolError = true;
                    activity?.SetStatus(ActivityStatusCode.Error, errMsg);
                    activity?.SetTag("error.type", typeof(EtpChannelStreamingException).FullName);
                    throw new EtpChannelStreamingException(errMsg, "(range request)", etpErrorCode: null);
                }
            }
        }
        finally
        {
            sw.Stop();
            EtpInstrumentation.OperationDuration.Record(
                sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("etp.operation", "channel.range_request"),
                new KeyValuePair<string, object?>("server.address", host));
            if (protocolError)
                EtpInstrumentation.RecordOperationError("channel.range_request", host, etpErrorCode);
            else
                activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }

    private async Task EnsureChannelStreamingProtocolStartedAsync(IEtpSessionCodec codec, CancellationToken ct)
    {
        if (_channelStreamingProtocolStarted)
            return;

        await _channelStreamingStartLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_channelStreamingProtocolStarted)
                return;

            var messageId = Interlocked.Increment(ref _nextMessageId);
            var frame = codec.EncodeChannelStreamingProtocolStart(
                Protocol1StartMaxMessageRate,
                Protocol1StartMaxDataItems,
                messageId);

            await SendFrameAsync(frame, codec.FrameType, ct).ConfigureAwait(false);
            _channelStreamingProtocolStarted = true;
        }
        finally
        {
            _channelStreamingStartLock.Release();
        }
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
        {
            EtpInstrumentation.MessagesReceived.Add(1, new KeyValuePair<string, object?>("server.address", _host));
            return buffer.AsMemory(0, result.Count);
        }

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

        EtpInstrumentation.MessagesReceived.Add(1, new KeyValuePair<string, object?>("server.address", _host));
        return ms.ToArray();
    }

    private ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, WebSocketMessageType frameType, CancellationToken ct)
    {
        EtpInstrumentation.MessagesSent.Add(1, new KeyValuePair<string, object?>("server.address", _host));
        return _transport.SendAsync(frame, frameType, endOfMessage: true, ct);
    }

    private static string BuildAuthorizationHeader(string username, string password)
    {
        var credentials = $"{username}:{password}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        return $"Basic {encoded}";
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _channelStreamingStartLock.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}
