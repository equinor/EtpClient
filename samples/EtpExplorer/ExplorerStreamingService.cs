using EtpClient.Models;
using Microsoft.Extensions.Logging;

namespace EtpExplorer;

/// <summary>
/// Manages live streaming subscription lifecycle for the explorer.
/// Converts selected endpoints to subscriptions, streams events, and handles cleanup.
/// </summary>
public sealed class ExplorerStreamingService
{
    private readonly ILogger<ExplorerStreamingService> _logger;

    public ExplorerStreamingService(ILogger<ExplorerStreamingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds the list of channel subscriptions from the current selection set.
    /// All subscriptions use start-latest streaming.
    /// </summary>
    public IReadOnlyList<ChannelSubscriptionInfo> BuildSubscriptions(IReadOnlyList<SelectedEndpoint> selection)
    {
        return selection
            .Select(s => new ChannelSubscriptionInfo(s.Endpoint.ChannelId, startLatest: true, receiveChangeNotifications: false))
            .ToList();
    }

    /// <summary>
    /// Streams channel events for the given subscriptions and yields rendered events.
    /// Stops when the cancellation token fires or streaming ends naturally.
    /// </summary>
    public async IAsyncEnumerable<RenderedStreamEvent> StreamAsync(
        IExplorerClient client,
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions,
        IReadOnlyList<SelectedEndpoint> selection,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var formatter = new StreamEventFormatter(selection);

        await foreach (var evt in client.StartChannelStreamingAsync(subscriptions, ct).WithCancellation(ct))
        {
            foreach (var rendered in formatter.Format(evt))
                yield return rendered;

            if (evt.Kind == EtpClient.Models.ChannelEventKind.Remove)
                break;
        }
    }

    /// <summary>
    /// Sends a stop request for all active channel IDs. Best-effort; does not throw.
    /// </summary>
    public async Task StopAsync(
        IExplorerClient client,
        IReadOnlyList<long> channelIds,
        CancellationToken ct = default)
    {
        if (channelIds.Count == 0) return;
        try
        {
            await client.StopChannelStreamingAsync(channelIds, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stop channel streaming encountered an error; continuing with shutdown.");
        }
    }
}
