using EtpClient.Models;

namespace EtpExplorer;

/// <summary>
/// App-local seam over <c>EtpClient</c> operations used by the explorer.
/// Enables testing without a live WebSocket server.
/// </summary>
public interface IExplorerClient : IAsyncDisposable
{
    /// <summary>Current connection state.</summary>
    EtpConnectionState State { get; }

    /// <inheritdoc cref="EtpClient.EtpClient.ConnectAsync"/>
    Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct = default);

    /// <inheritdoc cref="EtpClient.EtpClient.DiscoverResourcesAsync"/>
    Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct = default);

    /// <inheritdoc cref="EtpClient.EtpClient.DescribeChannelsAsync"/>
    Task<ChannelDescriptionResult> DescribeChannelsAsync(IReadOnlyList<string> uris, CancellationToken ct = default);

    /// <inheritdoc cref="EtpClient.EtpClient.StartChannelStreamingAsync"/>
    IAsyncEnumerable<ChannelEvent> StartChannelStreamingAsync(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions,
        CancellationToken ct = default);

    /// <inheritdoc cref="EtpClient.EtpClient.StopChannelStreamingAsync"/>
    Task StopChannelStreamingAsync(IReadOnlyList<long> channelIds, CancellationToken ct = default);

    /// <inheritdoc cref="EtpClient.EtpClient.CloseAsync"/>
    Task CloseAsync(CancellationToken ct = default);
}
