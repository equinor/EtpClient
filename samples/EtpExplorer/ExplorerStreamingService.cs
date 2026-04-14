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
    /// Creates the initial fixed-row snapshot from the current selection set.
    /// Rows are sorted alphabetically by channel name and set to waiting state.
    /// </summary>
    public StreamViewSnapshot BuildInitialSnapshot(IReadOnlyList<SelectedEndpoint> selection)
    {
        var rows = selection
            .Select(s => new StreamRowSnapshot
            {
                ChannelId = s.Endpoint.ChannelId,
                ChannelName = s.Endpoint.ChannelName,
                SourceResourceUri = s.Endpoint.SourceResourceUri,
            })
            .OrderBy(r => r.ChannelName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StreamViewSnapshot
        {
            Rows = rows,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Applies a formatted stream event to the matching row in the snapshot.
    /// Updates index, value, and status fields in place.
    /// No-op if no row matches the event's channel ID.
    /// </summary>
    public void ApplyEvent(StreamViewSnapshot snapshot, RenderedStreamEvent update)
    {
        var row = snapshot.Rows.FirstOrDefault(r => r.ChannelId == update.ChannelId);
        if (row is null)
            return;

        row.LastEventKind = update.EventKind;
        row.LastUpdatedAtUtc = update.ObservedAtUtc;

        switch (update.EventKind)
        {
            case StreamEventKind.Data:
                row.PrimaryIndexText = update.PrimaryIndexText;
                row.ValueText = update.ValueText;
                row.RowStatus = RowStatusField.Live;
                row.StatusText = "Live";
                break;

            case StreamEventKind.DataChange:
                row.RowStatus = RowStatusField.Changed;
                row.StatusText = "Changed";
                break;

            case StreamEventKind.StatusChange:
                row.RowStatus = RowStatusField.StatusChanged;
                row.StatusText = update.ValueText;
                break;

            case StreamEventKind.Remove:
                row.RowStatus = RowStatusField.Ended;
                row.StatusText = "Ended";
                break;
        }
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
