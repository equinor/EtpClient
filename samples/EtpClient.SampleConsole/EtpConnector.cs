using EtpClient.Models;

namespace EtpClient.SampleConsole;

/// <summary>
/// Production <see cref="IEtpConnector"/> implementation wrapping the library <see cref="EtpClient"/>.
/// </summary>
public sealed class EtpConnector : IEtpConnector
{
    private readonly EtpClient _client = new();

    /// <inheritdoc/>
    public Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct) =>
        _client.ConnectAsync(options, ct);

    /// <inheritdoc/>
    public Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct) =>
        _client.DiscoverResourcesAsync(uri, ct);

    /// <inheritdoc/>
    public Task<ChannelDescriptionResult> DescribeChannelsAsync(
        IReadOnlyList<string> uris, CancellationToken ct) =>
        _client.DescribeChannelsAsync(uris, ct);

    /// <inheritdoc/>
    public IAsyncEnumerable<ChannelEvent> StartChannelStreamingAsync(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions, CancellationToken ct) =>
        _client.StartChannelStreamingAsync(subscriptions, ct);

    /// <inheritdoc/>
    public Task StopChannelStreamingAsync(IReadOnlyList<long> channelIds, CancellationToken ct) =>
        _client.StopChannelStreamingAsync(channelIds, ct);

    /// <inheritdoc/>
    public IAsyncEnumerable<ChannelDataItem> RequestChannelRangeAsync(
        ChannelRangeRequestModel request, CancellationToken ct) =>
        _client.RequestChannelRangeAsync(request, ct);

    /// <inheritdoc/>
    public Task CloseAsync(CancellationToken ct) => _client.CloseAsync(ct);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
