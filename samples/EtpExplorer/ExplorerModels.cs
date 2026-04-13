namespace EtpExplorer;

// ── Session State ──────────────────────────────────────────────────────────────

/// <summary>High-level lifecycle state for one explorer session.</summary>
public enum ExplorerConnectionState
{
    Disconnected,
    Connected,
    Streaming,
    Closing,
}

/// <summary>Tracks user-visible interactive state for one explorer run.</summary>
public sealed class ExplorerSessionState
{
    public ExplorerConnectionState ConnectionState { get; set; } = ExplorerConnectionState.Disconnected;
    public IReadOnlyList<RootNodeOption> AvailableRootNodes { get; set; } = [];
    public RootNodeOption? SelectedRootNode { get; set; }
    public string CurrentUri { get; set; } = string.Empty;
    public List<string> NavigationStack { get; set; } = new();
    public IReadOnlyList<BrowseableResource> LastDiscoveryResult { get; set; } = [];
    public List<ExplorerBrowseColumn> BrowseColumns { get; set; } = new();
    public int FocusedBrowseColumnIndex { get; set; }
    public List<SelectedEndpoint> SelectionSet { get; set; } = new();
    public List<long> ActiveStreamChannels { get; set; } = new();
    public string? LastStatusMessage { get; set; }
}

// ── Root Node ─────────────────────────────────────────────────────────────────

/// <summary>One selectable top-level content branch discovered after connection.</summary>
public sealed class RootNodeOption
{
    public required string Name { get; init; }
    public required string Uri { get; init; }
    public string? Description { get; init; }
}

// ── Browse ────────────────────────────────────────────────────────────────────

/// <summary>One item in the interactive discovery browser.</summary>
public sealed class BrowseableResource
{
    public required string Uri { get; init; }
    public required string Name { get; init; }
    public required string ResourceType { get; init; }
    public required string ContentType { get; init; }
    public required int HasChildren { get; init; }
    public required bool ChannelSubscribable { get; init; }
    public int Depth { get; init; }
    public string? ParentUri { get; init; }
}

/// <summary>One visible browse pane containing child nodes for a given parent URI.</summary>
public sealed class ExplorerBrowseColumn
{
    public required string Title { get; init; }
    public required string ParentUri { get; init; }
    public required IReadOnlyList<BrowseableResource> Resources { get; init; }
    public int SelectedIndex { get; set; }
}

/// <summary>The action returned from the pane-style browse workspace.</summary>
public enum BrowseWorkspaceAction
{
    OpenFocusedResource,
    SelectFocusedResourceForStreaming,
    GoBack,
    ReturnToMain,
}

/// <summary>Result from one interactive pane-browser session turn.</summary>
public sealed class BrowseWorkspaceResult
{
    public required BrowseWorkspaceAction Action { get; init; }
    public required int FocusedColumnIndex { get; init; }
    public required IReadOnlyList<int> SelectedIndices { get; init; }
}

// ── Endpoint Resolution ───────────────────────────────────────────────────────

/// <summary>A channel definition that can be added to the selection set.</summary>
public sealed class ResolvedStreamableEndpoint
{
    public required string SourceResourceUri { get; init; }
    public required long ChannelId { get; init; }
    public required string ChannelUri { get; init; }
    public required string ChannelName { get; init; }
    public required string DataType { get; init; }
    public required string IndexType { get; init; }
    public required string Status { get; init; }
}

// ── Selection Set ─────────────────────────────────────────────────────────────

/// <summary>One user-selected stream target kept in the pending selection set.</summary>
public sealed class SelectedEndpoint
{
    public required string SelectionKey { get; init; }
    public required ResolvedStreamableEndpoint Endpoint { get; init; }
    public required DateTimeOffset SelectedAtUtc { get; init; }

    /// <summary>Builds a stable deduplication key from source + channel URIs.</summary>
    public static string BuildKey(string sourceResourceUri, string channelUri) =>
        $"{sourceResourceUri}|{channelUri}";
}

// ── Rendered Output ───────────────────────────────────────────────────────────

/// <summary>Categorises the kind of a live stream event for rendering purposes.</summary>
public enum StreamEventKind
{
    Data,
    DataChange,
    StatusChange,
    Remove,
}

/// <summary>One live row rendered while streaming is active.</summary>
public sealed class RenderedStreamEvent
{
    public required long ChannelId { get; init; }
    public required string ChannelName { get; init; }
    public required string SourceResourceUri { get; init; }
    public required string PrimaryIndexText { get; init; }
    public required string ValueText { get; init; }
    public required StreamEventKind EventKind { get; init; }
    public required DateTimeOffset ObservedAtUtc { get; init; }
}
