using System.Globalization;
using EtpClient.Models;

namespace EtpExplorer;

/// <summary>
/// Formats <see cref="ChannelEvent"/> instances for display,
/// attributing each event to its source endpoint.
/// </summary>
public sealed class StreamEventFormatter
{
    private readonly IReadOnlyDictionary<long, SelectedEndpoint> _endpointMap;

    public StreamEventFormatter(IReadOnlyList<SelectedEndpoint> selection)
    {
        _endpointMap = selection.ToDictionary(s => s.Endpoint.ChannelId);
    }

    /// <summary>
    /// Converts a <see cref="ChannelEvent"/> to zero or more <see cref="RenderedStreamEvent"/> records.
    /// Data events may produce one record per data item; other events produce one record for
    /// the primary channel ID on the event.
    /// </summary>
    public IReadOnlyList<RenderedStreamEvent> Format(EtpClient.Models.ChannelEvent evt, DateTimeOffset? now = null)
    {
        var observedAt = now ?? DateTimeOffset.UtcNow;

        switch (evt.Kind)
        {
            case EtpClient.Models.ChannelEventKind.Data:
                return evt.DataItems
                    .Select(item => FormatDataItem(item, observedAt))
                    .ToList();

            case EtpClient.Models.ChannelEventKind.DataChange:
                return [FormatSingleChannelEvent(
                    evt.ChannelId,
                    $"{evt.StartIndex}–{evt.EndIndex}",
                    "(changed)",
                    StreamEventKind.DataChange,
                    observedAt)];

            case EtpClient.Models.ChannelEventKind.StatusChange:
                return [FormatSingleChannelEvent(
                    evt.ChannelId,
                    string.Empty,
                    $"status: {evt.NewStatus}",
                    StreamEventKind.StatusChange,
                    observedAt)];

            case EtpClient.Models.ChannelEventKind.Remove:
                return [FormatSingleChannelEvent(
                    evt.ChannelId,
                    string.Empty,
                    evt.RemoveReason is { Length: > 0 } r ? $"(removed: {r})" : "(removed)",
                    StreamEventKind.Remove,
                    observedAt)];

            default:
                return [];
        }
    }

    private RenderedStreamEvent FormatDataItem(ChannelDataItem item, DateTimeOffset observedAt)
    {
        _endpointMap.TryGetValue(item.ChannelId, out var se);

        return new RenderedStreamEvent
        {
            ChannelId = item.ChannelId,
            ChannelName = se?.Endpoint.ChannelName ?? $"channel:{item.ChannelId}",
            SourceResourceUri = se?.Endpoint.SourceResourceUri ?? string.Empty,
            PrimaryIndexText = FormatIndexes(item.Indexes),
            ValueText = FormatValue(item.Value),
            EventKind = StreamEventKind.Data,
            ObservedAtUtc = observedAt,
        };
    }

    private RenderedStreamEvent FormatSingleChannelEvent(
        long channelId,
        string indexText,
        string valueText,
        StreamEventKind kind,
        DateTimeOffset observedAt)
    {
        _endpointMap.TryGetValue(channelId, out var se);

        return new RenderedStreamEvent
        {
            ChannelId = channelId,
            ChannelName = se?.Endpoint.ChannelName ?? $"channel:{channelId}",
            SourceResourceUri = se?.Endpoint.SourceResourceUri ?? string.Empty,
            PrimaryIndexText = indexText,
            ValueText = valueText,
            EventKind = kind,
            ObservedAtUtc = observedAt,
        };
    }

    private static string FormatIndexes(IReadOnlyList<long> indexes) => indexes.Count switch
    {
        0 => "-",
        1 => indexes[0].ToString(),
        _ => string.Join(", ", indexes),
    };

    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        double d => d.ToString("G", CultureInfo.InvariantCulture),
        float f => f.ToString("G", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        bool b => b.ToString(),
        byte[] bytes => $"bytes[{bytes.Length}]",
        _ => value.ToString() ?? string.Empty,
    };
}

