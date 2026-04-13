namespace EtpExplorer;

/// <summary>
/// Manages the user's pending stream endpoint selection set.
/// Enforces deduplication and provides add/remove/clear operations.
/// </summary>
public sealed class SelectionSetService
{
    private readonly List<SelectedEndpoint> _selected = new();

    /// <summary>Returns the current selection set as a read-only view.</summary>
    public IReadOnlyList<SelectedEndpoint> CurrentSelection => _selected;

    /// <summary>
    /// Adds one or more endpoints to the selection set.
    /// Duplicate endpoints (same <see cref="SelectedEndpoint.SelectionKey"/>) are silently skipped.
    /// </summary>
    /// <returns>Number of endpoints actually added.</returns>
    public int AddEndpoints(IEnumerable<ResolvedStreamableEndpoint> endpoints, DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var added = 0;
        foreach (var endpoint in endpoints)
        {
            var key = SelectedEndpoint.BuildKey(endpoint.SourceResourceUri, endpoint.ChannelUri);
            if (_selected.Any(s => s.SelectionKey == key))
                continue;

            _selected.Add(new SelectedEndpoint
            {
                SelectionKey = key,
                Endpoint = endpoint,
                SelectedAtUtc = timestamp,
            });
            added++;
        }
        return added;
    }

    /// <summary>Removes the endpoint with the given <paramref name="selectionKey"/> from the set.</summary>
    /// <returns><see langword="true"/> if an endpoint was found and removed.</returns>
    public bool RemoveEndpoint(string selectionKey)
    {
        var idx = _selected.FindIndex(s => s.SelectionKey == selectionKey);
        if (idx < 0) return false;
        _selected.RemoveAt(idx);
        return true;
    }

    /// <summary>Removes all endpoints from the selection set.</summary>
    public void ClearAll() => _selected.Clear();
}
