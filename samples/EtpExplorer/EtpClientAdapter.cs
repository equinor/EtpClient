using EtpClient;
using EtpClient.Models;

namespace EtpExplorer;

/// <summary>
/// Production adapter that wraps <see cref="EtpClient.EtpClient"/> to implement <see cref="IExplorerClient"/>.
/// </summary>
public sealed class EtpClientAdapter : IExplorerClient
{
    private readonly EtpClient.EtpClient _client;

    public EtpClientAdapter(EtpClient.EtpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public EtpConnectionState State => _client.State;

    public Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct = default)
        => _client.ConnectAsync(options, ct);

    public Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct = default)
        => _client.DiscoverResourcesAsync(uri, ct);

    public Task<ChannelDescriptionResult> DescribeChannelsAsync(IReadOnlyList<string> uris, CancellationToken ct = default)
        => _client.DescribeChannelsAsync(uris, ct);

    public IAsyncEnumerable<ChannelEvent> StartChannelStreamingAsync(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions,
        CancellationToken ct = default)
        => _client.StartChannelStreamingAsync(subscriptions, ct);

    public Task StopChannelStreamingAsync(IReadOnlyList<long> channelIds, CancellationToken ct = default)
        => _client.StopChannelStreamingAsync(channelIds, ct);

    public Task CloseAsync(CancellationToken ct = default)
        => _client.CloseAsync(ct);

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
