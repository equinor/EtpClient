using EtpClient.Models;

namespace EtpClient;

/// <summary>
/// Abstraction over an ETP client session. Supports authenticated connection,
/// Protocol 3 Discovery, and Protocol 1 ChannelStreaming workflows.
/// </summary>
public interface IEtpClient : IAsyncDisposable
{
    /// <summary>Gets the current connection state.</summary>
    EtpConnectionState State { get; }

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
    Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct = default);

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
    /// </exception>
    /// <exception cref="EtpDiscoveryException">
    /// Thrown when the server returns a <c>ProtocolException</c> or an unexpected message
    /// during the discovery exchange.
    /// </exception>
    Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct = default);

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
    /// </exception>
    /// <exception cref="EtpChannelStreamingException">
    /// Thrown when the server returns a <c>ProtocolException</c> or an unexpected message
    /// during the channel describe exchange.
    /// </exception>
    Task<ChannelDescriptionResult> DescribeChannelsAsync(IReadOnlyList<string> uris, CancellationToken ct = default);

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
    IAsyncEnumerable<ChannelEvent> StartChannelStreamingAsync(IReadOnlyList<ChannelSubscriptionInfo> subscriptions, CancellationToken ct = default);

    /// <summary>
    /// Sends a <c>ChannelStreamingStop</c> for the specified channel IDs.
    /// Stopping channels does not close the underlying ETP session.
    /// </summary>
    /// <param name="channelIds">Channel IDs to stop.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not <see cref="EtpConnectionState.Connected"/>.
    /// </exception>
    Task StopChannelStreamingAsync(IReadOnlyList<long> channelIds, CancellationToken ct = default);

    /// <summary>
    /// Requests historical channel data for a bounded primary-index range using Protocol 1.
    /// Yields each <see cref="ChannelDataItem"/> as it is received from the server.
    /// Enumeration completes when the server sends the final-part <c>ChannelData</c> message.
    /// </summary>
    /// <param name="request">Range request identifying channels, start index, and end index.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async sequence of <see cref="ChannelDataItem"/> values streamed from the server.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not <see cref="EtpConnectionState.Connected"/>.
    /// </exception>
    /// <exception cref="EtpChannelStreamingException">
    /// Thrown when the server returns a <c>ProtocolException</c> or an unexpected message type
    /// during the range exchange.
    /// </exception>
    IAsyncEnumerable<ChannelDataItem> RequestChannelRangeAsync(ChannelRangeRequestModel request, CancellationToken ct = default);

    /// <summary>
    /// Sends a WebSocket close frame and transitions to <see cref="EtpConnectionState.Closed"/>.
    /// Safe to call when already closed.
    /// </summary>
    Task CloseAsync(CancellationToken ct = default);
}
