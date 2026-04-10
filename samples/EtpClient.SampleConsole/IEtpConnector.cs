using EtpClient.Models;

namespace EtpClient.SampleConsole;

/// <summary>
/// Abstracts the ETP connection lifecycle for the sample runner.
/// Enables unit testing without real network calls.
/// </summary>
public interface IEtpConnector : IAsyncDisposable
{
    /// <summary>Opens an authenticated ETP session.</summary>
    Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct);

    /// <summary>Discovers immediate child resources of the specified ETP URI.</summary>
    Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct);

    /// <summary>Requests channel metadata for one or more ETP URIs via Protocol 1 ChannelDescribe.</summary>
    Task<ChannelDescriptionResult> DescribeChannelsAsync(IReadOnlyList<string> uris, CancellationToken ct);

    /// <summary>
    /// Starts live Protocol 1 channel streaming for the specified subscriptions.
    /// Yields <see cref="ChannelEvent"/> instances until the stream ends or is cancelled.
    /// </summary>
    IAsyncEnumerable<ChannelEvent> StartChannelStreamingAsync(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions, CancellationToken ct);

    /// <summary>
    /// Sends a <c>ChannelStreamingStop</c> for the specified channel IDs.
    /// Does not close the session.
    /// </summary>
    Task StopChannelStreamingAsync(IReadOnlyList<long> channelIds, CancellationToken ct);

    /// <summary>
    /// Requests bounded historical data for one or more channels via Protocol 1 ChannelRangeRequest.
    /// Aggregates multipart responses into a single result.
    /// </summary>
    Task<ChannelRangeResult> RequestChannelRangeAsync(ChannelRangeRequestModel request, CancellationToken ct);

    /// <summary>Sends a WebSocket close frame and transitions to closed state.</summary>
    Task CloseAsync(CancellationToken ct);
}
