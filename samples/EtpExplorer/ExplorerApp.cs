using EtpClient.Models;
using Microsoft.Extensions.Logging;

namespace EtpExplorer;

/// <summary>
/// Central workflow controller for the ETP Explorer.
/// Manages session lifecycle, menu state, navigation stack, and cancellation.
/// </summary>
public sealed class ExplorerApp
{
    private readonly IExplorerClient _client;
    private readonly IExplorerUi _ui;
    private readonly ExplorerOptions _options;
    private readonly ExplorerBrowseService _browseService;
    private readonly ExplorerEndpointResolver _endpointResolver;
    private readonly SelectionSetService _selectionSet;
    private readonly ExplorerStreamingService _streamingService;
    private readonly ILogger<ExplorerApp> _logger;

    private readonly ExplorerSessionState _state = new();
    private IReadOnlyList<ResolvedStreamableEndpoint>? _lastResolvedEndpoints;

    public ExplorerApp(
        IExplorerClient client,
        IExplorerUi ui,
        ExplorerOptions options,
        ExplorerBrowseService browseService,
        ExplorerEndpointResolver endpointResolver,
        SelectionSetService selectionSet,
        ExplorerStreamingService streamingService,
        ILogger<ExplorerApp> logger)
    {
        _client = client;
        _ui = ui;
        _options = options;
        _browseService = browseService;
        _endpointResolver = endpointResolver;
        _selectionSet = selectionSet;
        _streamingService = streamingService;
        _logger = logger;
    }

    /// <summary>
    /// Runs the explorer application from startup through shutdown.
    /// Returns when the user exits or an unrecoverable error occurs.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        // 1. Validate configuration
        var validationError = _options.Validate();
        if (validationError is not null)
        {
            _ui.ShowConfigError(validationError);
            return 1;
        }

        // 2. Connect
        _ui.ShowStatus("Connecting to ETP server...");
        EtpConnectionResult connectionResult;
        try
        {
            var connectionOptions = new EtpConnectionOptions(
                new Uri(_options.EndpointUri!),
                _options.Username!,
                _options.Password!,
                messageEncoding: _options.MessageEncoding);

            connectionResult = await _client.ConnectAsync(connectionOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _ui.ShowError($"Connection failed: {ex.Message}");
            return 1;
        }

        _state.ConnectionState = ExplorerConnectionState.Connected;
        _ui.ShowStatus($"Connected. Server: {connectionResult.Session.ServerApplicationName}");

        // 3. Discover root nodes
        IReadOnlyList<RootNodeOption> rootNodes;
        try
        {
            using var rootCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            rootCts.CancelAfter(TimeSpan.FromSeconds(_options.ProtocolRequestTimeoutSeconds));

            var rootResult = await _client.DiscoverResourcesAsync("eml://", rootCts.Token).ConfigureAwait(false);
            rootNodes = _browseService.MapRootNodes(rootResult);
        }
        catch (Exception ex)
        {
            _ui.ShowError($"Root discovery failed: {ex.Message}");
            await ShutdownAsync(ct).ConfigureAwait(false);
            return 1;
        }

        if (rootNodes.Count == 0)
        {
            _ui.ShowError("No root nodes discovered. The server may not support Discovery.");
            await ShutdownAsync(ct).ConfigureAwait(false);
            return 1;
        }

        _state.AvailableRootNodes = rootNodes;

        // 4. Prompt root node selection
        await SelectRootNodeAsync(rootNodes, ct).ConfigureAwait(false);
        await BrowseLoopAsync(ct).ConfigureAwait(false);

        // 5. Main menu loop
        while (_state.ConnectionState == ExplorerConnectionState.Connected && !ct.IsCancellationRequested)
        {
            _state.SelectionSet = _selectionSet.CurrentSelection.ToList();
            var action = await _ui.PromptMainMenuAsync(_state, ct).ConfigureAwait(false);

            switch (action)
            {
                case MainMenuAction.Browse:
                    await BrowseLoopAsync(ct).ConfigureAwait(false);
                    break;

                case MainMenuAction.ChangeRootNode:
                    await SelectRootNodeAsync(_state.AvailableRootNodes, ct).ConfigureAwait(false);
                    await BrowseLoopAsync(ct).ConfigureAwait(false);
                    break;

                case MainMenuAction.ReviewSelection:
                    await ReviewSelectionAsync(ct).ConfigureAwait(false);
                    break;

                case MainMenuAction.StartStreaming:
                    await StartStreamingAsync(ct).ConfigureAwait(false);
                    break;

                case MainMenuAction.Exit:
                default:
                    goto exitLoop;
            }
        }

        exitLoop:
        await ShutdownAsync(ct).ConfigureAwait(false);
        return 0;
    }

    // ── Root node selection ───────────────────────────────────────────────────

    private async Task SelectRootNodeAsync(IReadOnlyList<RootNodeOption> rootNodes, CancellationToken ct)
    {
        var chosen = await _ui.PromptRootNodeSelectionAsync(rootNodes, ct).ConfigureAwait(false);
        _state.SelectedRootNode = chosen;
        _state.CurrentUri = chosen.Uri;
        _state.NavigationStack.Clear();
        _state.NavigationStack.Add(chosen.Name);
        _state.LastDiscoveryResult = [];
        _state.BrowseColumns.Clear();
        _state.FocusedBrowseColumnIndex = 0;
        _lastResolvedEndpoints = null;
        _ui.ShowStatus($"Root node selected: {chosen.Name}");
        await InitializeBrowseColumnsAsync(ct).ConfigureAwait(false);
    }

    // ── Browse loop ───────────────────────────────────────────────────────────

    private async Task BrowseLoopAsync(CancellationToken ct)
    {
        if (_state.SelectedRootNode is null)
            return;

        if (_state.BrowseColumns.Count == 0)
        {
            await InitializeBrowseColumnsAsync(ct).ConfigureAwait(false);
        }

        while (!ct.IsCancellationRequested)
        {
            _state.SelectionSet = _selectionSet.CurrentSelection.ToList();
            var result = await _ui.PromptBrowseWorkspaceAsync(_state, ct).ConfigureAwait(false);
            ApplyBrowseSelection(result);

            switch (result.Action)
            {
                case BrowseWorkspaceAction.OpenFocusedResource:
                    await OpenFocusedResourceAsync(ct).ConfigureAwait(false);
                    break;

                case BrowseWorkspaceAction.SelectFocusedResourceForStreaming:
                    await AddFocusedResourceToSelectionAsync(ct).ConfigureAwait(false);
                    break;

                case BrowseWorkspaceAction.GoBack:
                    CloseFocusedPane();
                    break;

                case BrowseWorkspaceAction.ReturnToMain:
                default:
                    return;
            }
        }
    }

    private async Task InitializeBrowseColumnsAsync(CancellationToken ct)
    {
        if (_state.SelectedRootNode is null)
            return;

        var resources = await DiscoverBrowseResourcesAsync(
            _state.SelectedRootNode.Uri,
            depth: 1,
            ct).ConfigureAwait(false);

        _state.BrowseColumns =
        [
            new ExplorerBrowseColumn
            {
                Title = _state.SelectedRootNode.Name,
                ParentUri = _state.SelectedRootNode.Uri,
                Resources = resources,
                SelectedIndex = resources.Count == 0 ? -1 : 0,
            },
        ];

        _state.FocusedBrowseColumnIndex = 0;
        _state.LastStatusMessage = resources.Count == 0
            ? $"No child resources found under {_state.SelectedRootNode.Name}."
            : $"Browse {_state.SelectedRootNode.Name} with arrow keys. Press Enter to open, Space to add for streaming.";
        SyncBrowseState();
    }

    private async Task<IReadOnlyList<BrowseableResource>> DiscoverBrowseResourcesAsync(
        string uri,
        int depth,
        CancellationToken ct)
    {
        try
        {
            using var browseCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            browseCts.CancelAfter(TimeSpan.FromSeconds(_options.ProtocolRequestTimeoutSeconds));

            var discoveryResult = await _client.DiscoverResourcesAsync(uri, browseCts.Token).ConfigureAwait(false);
            var resources = _browseService.MapResources(discoveryResult, uri, depth);
            _state.LastDiscoveryResult = resources;
            return resources;
        }
        catch (Exception ex)
        {
            _state.LastStatusMessage = $"Browse failed: {ex.Message}";
            return [];
        }
    }

    private void ApplyBrowseSelection(BrowseWorkspaceResult result)
    {
        if (_state.BrowseColumns.Count == 0)
            return;

        _state.FocusedBrowseColumnIndex = Math.Clamp(
            result.FocusedColumnIndex,
            0,
            _state.BrowseColumns.Count - 1);

        for (var i = 0; i < _state.BrowseColumns.Count; i++)
        {
            var column = _state.BrowseColumns[i];
            if (column.Resources.Count == 0)
            {
                column.SelectedIndex = -1;
                continue;
            }

            var requestedIndex = i < result.SelectedIndices.Count
                ? result.SelectedIndices[i]
                : column.SelectedIndex;

            column.SelectedIndex = Math.Clamp(requestedIndex, 0, column.Resources.Count - 1);
        }

        SyncBrowseState();
    }

    private async Task OpenFocusedResourceAsync(CancellationToken ct)
    {
        var focused = GetFocusedResource();
        if (focused is null)
        {
            _state.LastStatusMessage = "No node selected in the active column.";
            return;
        }

        var childResources = await DiscoverBrowseResourcesAsync(
            focused.Uri,
            focused.Depth + 1,
            ct).ConfigureAwait(false);

        if (_state.FocusedBrowseColumnIndex < _state.BrowseColumns.Count - 1)
        {
            _state.BrowseColumns.RemoveRange(
                _state.FocusedBrowseColumnIndex + 1,
                _state.BrowseColumns.Count - (_state.FocusedBrowseColumnIndex + 1));
        }

        _state.BrowseColumns.Add(new ExplorerBrowseColumn
        {
            Title = focused.Name,
            ParentUri = focused.Uri,
            Resources = childResources,
            SelectedIndex = childResources.Count == 0 ? -1 : 0,
        });

        _state.FocusedBrowseColumnIndex = _state.BrowseColumns.Count - 1;
        _state.LastStatusMessage = childResources.Count == 0
            ? $"{focused.Name} has no child nodes."
            : $"Opened {focused.Name}.";
        SyncBrowseState();
    }

    private void CloseFocusedPane()
    {
        if (_state.BrowseColumns.Count <= 1)
        {
            _state.LastStatusMessage = "Already at the root column.";
            SyncBrowseState();
            return;
        }

        var removeFrom = Math.Max(1, _state.FocusedBrowseColumnIndex);
        _state.BrowseColumns.RemoveRange(removeFrom, _state.BrowseColumns.Count - removeFrom);
        _state.FocusedBrowseColumnIndex = removeFrom - 1;
        _state.LastStatusMessage = "Moved back one level.";
        SyncBrowseState();
    }

    private async Task AddFocusedResourceToSelectionAsync(CancellationToken ct)
    {
        var focused = GetFocusedResource();
        if (focused is null)
        {
            _state.LastStatusMessage = "No node selected in the active column.";
            return;
        }

        IReadOnlyList<ResolvedStreamableEndpoint> available;
        try
        {
            using var describeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            describeCts.CancelAfter(TimeSpan.FromSeconds(_options.ProtocolRequestTimeoutSeconds));

            var describeResult = await _client.DescribeChannelsAsync(
                [focused.Uri],
                describeCts.Token).ConfigureAwait(false);

            available = _endpointResolver.ResolveEndpoints(describeResult, focused.Uri);
            _lastResolvedEndpoints = available;
        }
        catch (Exception ex)
        {
            _state.LastStatusMessage = $"Endpoint resolution failed: {ex.Message}";
            _lastResolvedEndpoints = [];
            return;
        }

        if (available.Count == 0)
        {
            _state.LastStatusMessage = $"{focused.Name} has no streamable endpoints.";
            return;
        }

        IReadOnlyList<ResolvedStreamableEndpoint> chosen;
        if (available.Count == 1)
        {
            chosen = available;
        }
        else
        {
            chosen = await _ui.PromptEndpointSelectionAsync(available, ct).ConfigureAwait(false);
            if (chosen.Count == 0)
            {
                _state.LastStatusMessage = "No endpoints selected.";
                return;
            }
        }

        var added = _selectionSet.AddEndpoints(chosen);
        _state.SelectionSet = _selectionSet.CurrentSelection.ToList();
        _state.LastStatusMessage = $"Added {added} endpoint(s). Total selected: {_selectionSet.CurrentSelection.Count}.";
    }

    private BrowseableResource? GetFocusedResource()
    {
        if (_state.BrowseColumns.Count == 0)
            return null;

        var focusedColumn = _state.BrowseColumns[Math.Clamp(
            _state.FocusedBrowseColumnIndex,
            0,
            _state.BrowseColumns.Count - 1)];

        if (focusedColumn.Resources.Count == 0 || focusedColumn.SelectedIndex < 0)
            return null;

        return focusedColumn.Resources[focusedColumn.SelectedIndex];
    }

    private void SyncBrowseState()
    {
        if (_state.SelectedRootNode is null)
            return;

        _state.NavigationStack.Clear();
        _state.NavigationStack.Add(_state.SelectedRootNode.Name);
        _state.CurrentUri = _state.SelectedRootNode.Uri;

        for (var i = 0; i < _state.BrowseColumns.Count; i++)
        {
            var column = _state.BrowseColumns[i];
            if (column.Resources.Count == 0 || column.SelectedIndex < 0)
                break;

            var resource = column.Resources[column.SelectedIndex];
            _state.NavigationStack.Add(resource.Name);
            _state.CurrentUri = resource.Uri;

            if (i == _state.FocusedBrowseColumnIndex)
                break;
        }
    }

    // ── Selection review ──────────────────────────────────────────────────────

    private async Task ReviewSelectionAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var current = _selectionSet.CurrentSelection;
            var action = await _ui.PromptSelectionReviewAsync(current, ct).ConfigureAwait(false);

            switch (action)
            {
                case SelectionReviewAction.RemoveOne:
                    var toRemove = await _ui.PromptRemoveEndpointAsync(current, ct).ConfigureAwait(false);
                    if (toRemove is not null)
                    {
                        _selectionSet.RemoveEndpoint(toRemove.SelectionKey);
                        _state.SelectionSet = _selectionSet.CurrentSelection.ToList();
                        _ui.ShowStatus($"Removed: {toRemove.Endpoint.ChannelName}");
                    }
                    break;

                case SelectionReviewAction.ClearAll:
                    _selectionSet.ClearAll();
                    _state.SelectionSet = [];
                    _ui.ShowStatus("Selection cleared.");
                    return;

                case SelectionReviewAction.Done:
                default:
                    return;
            }
        }
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    private async Task StartStreamingAsync(CancellationToken ct)
    {
        var selection = _selectionSet.CurrentSelection;
        if (selection.Count == 0)
        {
            _ui.ShowStatus("No endpoints selected. Add endpoints to the selection set before streaming.");
            return;
        }

        var subscriptions = _streamingService.BuildSubscriptions(selection);
        _state.ConnectionState = ExplorerConnectionState.Streaming;
        _state.ActiveStreamChannels = selection.Select(s => s.Endpoint.ChannelId).ToList();

        _ui.ShowStatus($"Starting live stream for {selection.Count} endpoint(s)...");

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await foreach (var rendered in _streamingService.StreamAsync(_client, subscriptions, selection, streamCts.Token))
            {
                _ui.RenderStreamEvent(rendered);

                // Non-blocking stop check
                var shouldStop = await _ui.PromptStopStreamingAsync(streamCts.Token).ConfigureAwait(false);
                if (shouldStop)
                {
                    streamCts.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // User-initiated stop
        }
        finally
        {
            // Best-effort stop
            await _streamingService.StopAsync(_client, _state.ActiveStreamChannels, ct).ConfigureAwait(false);
            _state.ActiveStreamChannels.Clear();
            _state.ConnectionState = ExplorerConnectionState.Connected;
            _ui.ShowStatus("Streaming stopped.");
        }
    }

    // ── Shutdown ──────────────────────────────────────────────────────────────

    private async Task ShutdownAsync(CancellationToken ct)
    {
        if (_state.ConnectionState == ExplorerConnectionState.Streaming)
        {
            await _streamingService.StopAsync(_client, _state.ActiveStreamChannels, ct).ConfigureAwait(false);
        }

        _state.ConnectionState = ExplorerConnectionState.Closing;
        try
        {
            await _client.CloseAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Close encountered an error during shutdown.");
        }

        _state.ConnectionState = ExplorerConnectionState.Disconnected;
    }
}
