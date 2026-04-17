using EtpClient.Connection;
using EtpClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpClient;

/// <summary>
/// Minimal ETP client that can open an authenticated session with an ETP server
/// using WebSocket + Basic authentication and complete the Protocol 0 handshake.
/// </summary>
/// <example>
/// <code lang="csharp">
/// await using var client = new EtpClient(logger);
/// var options = new EtpConnectionOptions(new Uri("wss://server/etp"), "user", "pass");
/// var result = await client.ConnectAsync(options);
/// Console.WriteLine($"Connected: {result.Session.ServerApplicationName}");
/// await client.CloseAsync();
/// </code>
/// </example>
public sealed class EtpClient : IEtpClient
{
    private readonly Func<IWebSocketTransport> _transportFactory;
    private readonly ILogger _logger;

    private EtpSessionManager? _manager;

    /// <summary>Gets the current connection state.</summary>
    public EtpConnectionState State => _manager?.State ?? EtpConnectionState.Closed;

    /// <summary>
    /// Creates a new <see cref="EtpClient"/> using the production WebSocket transport.
    /// </summary>
    /// <param name="logger">Optional logger; uses <see cref="NullLogger"/> when omitted.</param>
    public EtpClient(ILogger<EtpClient>? logger = null)
        : this(() => new ClientWebSocketTransport(), (ILogger?)logger)
    {
    }

    /// <summary>Internal ctor for unit/integration testing with a custom transport factory.</summary>
    internal EtpClient(Func<IWebSocketTransport> transportFactory, ILogger? logger = null)
    {
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Opens a WebSocket connection to the ETP server, attaches Basic authentication,
    /// and completes the ETP Protocol 0 RequestSession/OpenSession handshake.
    /// </summary>
    /// <param name="options">Connection configuration. Must not be <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="EtpConnectionResult"/> containing the negotiated session details.
    /// </returns>
    /// <exception cref="EtpConnectionException">
    /// Thrown when authentication fails, the server rejects the session, or a transport error occurs.
    /// </exception>
    public async Task<EtpConnectionResult> ConnectAsync(
        EtpConnectionOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Create a fresh transport + manager for each connection attempt
        var transport = _transportFactory();
        _manager = new EtpSessionManager(transport, _logger);

        return await _manager.ConnectAsync(options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Discovers immediate child resources of the specified ETP URI using Protocol 3 (Discovery).
    /// </summary>
    /// <param name="uri">
    /// The ETP URI whose children are to be discovered (e.g. <c>"eml://"</c> for the root).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="DiscoveryResult"/> containing the list of discovered resources plus
    /// metadata about whether the server acknowledged an empty result.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not in the <see cref="EtpConnectionState.Connected"/> state.
    /// Call <see cref="ConnectAsync"/> first.
    /// </exception>
    /// <exception cref="EtpDiscoveryException">
    /// Thrown when the server returns a <c>ProtocolException</c> or an unexpected message
    /// during the discovery exchange.
    /// </exception>
    public Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct = default)
    {
        if (_manager is null || State != EtpConnectionState.Connected)
            throw new InvalidOperationException("Discovery requires an active Connected session.");

        return _manager.DiscoverResourcesAsync(uri, ct);
    }

    /// <summary>
    /// Sends a WebSocket close frame and transitions to <see cref="EtpConnectionState.Closed"/>.
    /// Safe to call when already closed.
    /// </summary>
    public Task CloseAsync(CancellationToken ct = default)
    {
        if (_manager is null)
            return Task.CompletedTask;

        return _manager.CloseAsync(ct);
    }

    /// <summary>
    /// Requests channel metadata from the producer for one or more ETP URIs
    /// using Protocol 1 (ChannelStreaming) ChannelDescribe.
    /// </summary>
    /// <param name="uris">One or more ETP channel URIs to describe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ChannelDescriptionResult"/> containing the channel definitions
    /// returned by the server plus request metadata.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not in the <see cref="EtpConnectionState.Connected"/> state.
    /// Call <see cref="ConnectAsync"/> first.
    /// </exception>
    /// <exception cref="EtpChannelStreamingException">
    /// Thrown when the server returns a <c>ProtocolException</c> or an unexpected message
    /// during the channel describe exchange.
    /// </exception>
    public Task<ChannelDescriptionResult> DescribeChannelsAsync(
        IReadOnlyList<string> uris,
        CancellationToken ct = default)
    {
        if (_manager is null || State != EtpConnectionState.Connected)
            throw new InvalidOperationException(
                "DescribeChannels requires an active Connected session.");

        return _manager.DescribeChannelsAsync(uris, ct);
    }

    /// <summary>
    /// Starts live Protocol 1 channel streaming for the specified subscriptions.
    /// Yields <see cref="ChannelEvent"/> instances as the producer sends data, change,
    /// status, or remove messages. The enumeration completes only after the server has
    /// sent a <c>ChannelRemove</c> for every channel ID in <paramref name="subscriptions"/>,
    /// or when the cancellation token fires. Individual removals are yielded as
    /// <see cref="ChannelEventKind.Remove"/> events so callers can react to each one;
    /// the stream continues until the last subscribed channel is removed.
    /// </summary>
    /// <param name="subscriptions">Channels to subscribe to with their streaming parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not <see cref="EtpConnectionState.Connected"/>.
    /// </exception>
    /// <exception cref="EtpChannelStreamingException">
    /// Thrown when the server returns a <c>ProtocolException</c> during streaming.
    /// </exception>
    public IAsyncEnumerable<ChannelEvent> StartChannelStreamingAsync(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions,
        CancellationToken ct = default)
    {
        if (_manager is null || State != EtpConnectionState.Connected)
            throw new InvalidOperationException(
                "StartChannelStreaming requires an active Connected session.");

        return _manager.StartChannelStreamingAsync(subscriptions, ct);
    }

    /// <summary>
    /// Sends a <c>ChannelStreamingStop</c> for the specified channel IDs.
    /// Stopping channels does not close the underlying ETP session.
    /// </summary>
    /// <param name="channelIds">Channel IDs to stop.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not <see cref="EtpConnectionState.Connected"/>.
    /// </exception>
    public Task StopChannelStreamingAsync(
        IReadOnlyList<long> channelIds,
        CancellationToken ct = default)
    {
        if (_manager is null || State != EtpConnectionState.Connected)
            throw new InvalidOperationException(
                "StopChannelStreaming requires an active Connected session.");

        return _manager.StopChannelStreamingAsync(channelIds, ct);
    }

    /// <summary>
    /// Requests historical channel data for a bounded primary-index range using Protocol 1.
    /// Aggregates multipart <c>ChannelData</c> responses into a single result.
    /// </summary>
    /// <param name="request">Range request identifying channels, start index, and end index.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ChannelRangeResult"/> containing all data for the requested range.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not <see cref="EtpConnectionState.Connected"/>.
    /// </exception>
    /// <exception cref="EtpChannelStreamingException">
    /// Thrown when the server returns a <c>ProtocolException</c>.
    /// </exception>
    public Task<ChannelRangeResult> RequestChannelRangeAsync(
        ChannelRangeRequestModel request,
        CancellationToken ct = default)
    {
        if (_manager is null || State != EtpConnectionState.Connected)
            throw new InvalidOperationException(
                "RequestChannelRange requires an active Connected session.");

        return _manager.RequestChannelRangeAsync(request, ct);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        var manager = Interlocked.Exchange(ref _manager, null);
        if (manager is null)
            return;

        await manager.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        await manager.DisposeAsync().ConfigureAwait(false);
    }
}
