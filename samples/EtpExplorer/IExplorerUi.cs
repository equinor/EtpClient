namespace EtpExplorer;

/// <summary>
/// Abstraction over Spectre.Console (or fake) presentation layer for the explorer.
/// Enables deterministic testing of navigation decisions and rendered output
/// without a real terminal.
/// </summary>
public interface IExplorerUi
{
    /// <summary>Displays an informational status message to the user.</summary>
    void ShowStatus(string message);

    /// <summary>Displays an error or failure message.</summary>
    void ShowError(string message);

    /// <summary>Displays a secret-safe setup hint with missing config key names.</summary>
    void ShowConfigError(string message);

    /// <summary>
    /// Prompts the user to choose one root node from the available options.
    /// Returns the chosen <see cref="RootNodeOption"/>.
    /// </summary>
    Task<RootNodeOption> PromptRootNodeSelectionAsync(
        IReadOnlyList<RootNodeOption> rootNodes,
        CancellationToken ct = default);

    /// <summary>
    /// Shows the top-level connected main menu and returns the chosen action.
    /// </summary>
    Task<MainMenuAction> PromptMainMenuAsync(
        ExplorerSessionState state,
        CancellationToken ct = default);

    /// <summary>
    /// Shows the pane-style browse workspace and returns the chosen interaction.
    /// </summary>
    Task<BrowseWorkspaceResult> PromptBrowseWorkspaceAsync(
        ExplorerSessionState state,
        CancellationToken ct = default);

    /// <summary>
    /// Prompts the user to select one or more resolved endpoints to add to the selection set.
    /// Returns the endpoints that were chosen (may be empty).
    /// </summary>
    Task<IReadOnlyList<ResolvedStreamableEndpoint>> PromptEndpointSelectionAsync(
        IReadOnlyList<ResolvedStreamableEndpoint> endpoints,
        CancellationToken ct = default);

    /// <summary>
    /// Shows the current selection set and returns the chosen review action.
    /// </summary>
    Task<SelectionReviewAction> PromptSelectionReviewAsync(
        IReadOnlyList<SelectedEndpoint> selectedEndpoints,
        CancellationToken ct = default);

    /// <summary>
    /// Prompts the user to choose one endpoint to remove from the selection set.
    /// Returns <see langword="null"/> if the user cancels or the list is empty.
    /// </summary>
    Task<SelectedEndpoint?> PromptRemoveEndpointAsync(
        IReadOnlyList<SelectedEndpoint> selectedEndpoints,
        CancellationToken ct = default);

    /// <summary>
    /// Renders one live streaming event to the output.
    /// </summary>
    void RenderStreamEvent(RenderedStreamEvent evt);

    /// <summary>
    /// Renders an inline streaming stop/exit prompt while streaming is active.
    /// Returns <see langword="true"/> when the user requests a stop.
    /// </summary>
    Task<bool> PromptStopStreamingAsync(CancellationToken ct = default);
}

// ── Menu Action Enums ─────────────────────────────────────────────────────────

/// <summary>Actions available from the top-level connected main menu.</summary>
public enum MainMenuAction
{
    Browse,
    ChangeRootNode,
    ReviewSelection,
    StartStreaming,
    Exit,
}

/// <summary>Actions available from the selection review screen.</summary>
public enum SelectionReviewAction
{
    RemoveOne,
    ClearAll,
    Done,
}
