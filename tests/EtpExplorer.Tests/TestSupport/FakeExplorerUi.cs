namespace EtpExplorer.Tests.TestSupport;

/// <summary>
/// Deterministic fake of <see cref="IExplorerUi"/> for testing the explorer workflow.
/// Queues pre-configured responses for prompts and captures all output calls.
/// </summary>
public class FakeExplorerUi : IExplorerUi
{
    // ── Queued prompt responses ───────────────────────────────────────────────

    private readonly Queue<RootNodeOption> _rootNodeResponses = new();
    private readonly Queue<MainMenuAction> _mainMenuResponses = new();
    private readonly Queue<BrowseWorkspaceResult> _browseWorkspaceResponses = new();
    private readonly Queue<IReadOnlyList<ResolvedStreamableEndpoint>> _endpointSelectionResponses = new();
    private readonly Queue<SelectionReviewAction> _selectionReviewResponses = new();
    private readonly Queue<SelectedEndpoint?> _removeEndpointResponses = new();
    private readonly Queue<bool> _stopStreamingResponses = new();

    // ── Captured output ───────────────────────────────────────────────────────

    public List<string> StatusMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public List<string> ConfigErrorMessages { get; } = new();
    public List<RenderedStreamEvent> RenderedEvents { get; } = new();
    public List<ExplorerSessionState> BrowseSnapshots { get; } = new();

    // ── Queuing helpers ───────────────────────────────────────────────────────

    public void EnqueueRootNode(RootNodeOption node) => _rootNodeResponses.Enqueue(node);
    public void EnqueueMainMenu(MainMenuAction action) => _mainMenuResponses.Enqueue(action);
    public void EnqueueBrowseResult(BrowseWorkspaceResult result) => _browseWorkspaceResponses.Enqueue(result);
    public void EnqueueEndpointSelection(IReadOnlyList<ResolvedStreamableEndpoint> endpoints) =>
        _endpointSelectionResponses.Enqueue(endpoints);
    public void EnqueueSelectionReview(SelectionReviewAction action) => _selectionReviewResponses.Enqueue(action);
    public void EnqueueRemoveEndpoint(SelectedEndpoint? endpoint) => _removeEndpointResponses.Enqueue(endpoint);
    public void EnqueueStopStreaming(bool stop) => _stopStreamingResponses.Enqueue(stop);

    // ── IExplorerUi ───────────────────────────────────────────────────────────

    public void ShowStatus(string message) => StatusMessages.Add(message);

    public void ShowError(string message) => ErrorMessages.Add(message);

    public void ShowConfigError(string message) => ConfigErrorMessages.Add(message);

    public Task<RootNodeOption> PromptRootNodeSelectionAsync(
        IReadOnlyList<RootNodeOption> rootNodes,
        CancellationToken ct = default)
    {
        if (_rootNodeResponses.Count == 0)
            throw new InvalidOperationException(
                $"FakeExplorerUi: No queued response for {nameof(PromptRootNodeSelectionAsync)}. " +
                $"Call {nameof(EnqueueRootNode)} before running.");

        return Task.FromResult(_rootNodeResponses.Dequeue());
    }

    public virtual Task<MainMenuAction> PromptMainMenuAsync(
        ExplorerSessionState state,
        CancellationToken ct = default)
    {
        if (_mainMenuResponses.Count == 0)
            throw new InvalidOperationException(
                $"FakeExplorerUi: No queued response for {nameof(PromptMainMenuAsync)}. " +
                $"Call {nameof(EnqueueMainMenu)} before running.");

        return Task.FromResult(_mainMenuResponses.Dequeue());
    }

    public Task<BrowseWorkspaceResult> PromptBrowseWorkspaceAsync(
        ExplorerSessionState state,
        CancellationToken ct = default)
    {
        BrowseSnapshots.Add(CloneState(state));

        if (_browseWorkspaceResponses.Count == 0)
        {
            var selectedIndices = state.BrowseColumns
                .Select(column => column.SelectedIndex)
                .ToArray();

            return Task.FromResult(new BrowseWorkspaceResult
            {
                Action = BrowseWorkspaceAction.ReturnToMain,
                FocusedColumnIndex = state.FocusedBrowseColumnIndex,
                SelectedIndices = selectedIndices,
            });
        }

        return Task.FromResult(_browseWorkspaceResponses.Dequeue());
    }

    public Task<IReadOnlyList<ResolvedStreamableEndpoint>> PromptEndpointSelectionAsync(
        IReadOnlyList<ResolvedStreamableEndpoint> endpoints,
        CancellationToken ct = default)
    {
        if (_endpointSelectionResponses.Count == 0)
            return Task.FromResult<IReadOnlyList<ResolvedStreamableEndpoint>>([]);

        return Task.FromResult(_endpointSelectionResponses.Dequeue());
    }

    public Task<SelectionReviewAction> PromptSelectionReviewAsync(
        IReadOnlyList<SelectedEndpoint> selectedEndpoints,
        CancellationToken ct = default)
    {
        if (_selectionReviewResponses.Count == 0)
            return Task.FromResult(SelectionReviewAction.Done);

        return Task.FromResult(_selectionReviewResponses.Dequeue());
    }

    public Task<SelectedEndpoint?> PromptRemoveEndpointAsync(
        IReadOnlyList<SelectedEndpoint> selectedEndpoints,
        CancellationToken ct = default)
    {
        if (_removeEndpointResponses.Count == 0)
            return Task.FromResult<SelectedEndpoint?>(null);

        return Task.FromResult(_removeEndpointResponses.Dequeue());
    }

    public void RenderStreamEvent(RenderedStreamEvent evt) => RenderedEvents.Add(evt);

    public Task<bool> PromptStopStreamingAsync(CancellationToken ct = default)
    {
        if (_stopStreamingResponses.Count == 0)
            return Task.FromResult(false);

        return Task.FromResult(_stopStreamingResponses.Dequeue());
    }

    private static ExplorerSessionState CloneState(ExplorerSessionState state) => new()
    {
        ConnectionState = state.ConnectionState,
        AvailableRootNodes = state.AvailableRootNodes.ToList(),
        SelectedRootNode = state.SelectedRootNode,
        CurrentUri = state.CurrentUri,
        NavigationStack = state.NavigationStack.ToList(),
        LastDiscoveryResult = state.LastDiscoveryResult.ToList(),
        BrowseColumns = state.BrowseColumns
            .Select(column => new ExplorerBrowseColumn
            {
                Title = column.Title,
                ParentUri = column.ParentUri,
                Resources = column.Resources.ToList(),
                SelectedIndex = column.SelectedIndex,
                SearchTerm = column.SearchTerm,
            })
            .ToList(),
        FocusedBrowseColumnIndex = state.FocusedBrowseColumnIndex,
        SelectionSet = state.SelectionSet.ToList(),
        ActiveStreamChannels = state.ActiveStreamChannels.ToList(),
        LastStatusMessage = state.LastStatusMessage,
    };
}
