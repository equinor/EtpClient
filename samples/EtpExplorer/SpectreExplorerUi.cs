using Spectre.Console.Rendering;
using Spectre.Console;

namespace EtpExplorer;

/// <summary>
/// Production implementation of <see cref="IExplorerUi"/> using Spectre.Console.
/// </summary>
public sealed class SpectreExplorerUi : IExplorerUi
{
    public void ShowStatus(string message)
        => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");

    public void ShowError(string message)
        => AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");

    public void ShowConfigError(string message)
    {
        AnsiConsole.MarkupLine("[red bold]Configuration error:[/]");
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    public Task<RootNodeOption> PromptRootNodeSelectionAsync(
        IReadOnlyList<RootNodeOption> rootNodes,
        CancellationToken ct = default)
    {
        var choices = rootNodes.Select(n => n.Name).ToList();
        var chosen = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Choose a root node to browse:[/]")
                .PageSize(10)
                .AddChoices(choices));

        var node = rootNodes.First(n => n.Name == chosen);
        return Task.FromResult(node);
    }

    public Task<MainMenuAction> PromptMainMenuAsync(
        ExplorerSessionState state,
        CancellationToken ct = default)
    {
        var root = state.SelectedRootNode?.Name ?? "(none)";
        var selCount = state.SelectionSet.Count;
        var prompt = new SelectionPrompt<string>()
            .Title($"[green bold]ETP Explorer[/] — Root: [cyan]{Markup.Escape(root)}[/] | Selected: [yellow]{selCount}[/] endpoint(s)")
            .PageSize(10)
            .AddChoices(
                "Return to browser",
                "Change root node",
                "Review selected endpoints",
                $"Start streaming ({selCount} endpoint(s) selected)",
                "Exit");

        var choice = AnsiConsole.Prompt(prompt);

        var action = choice switch
        {
            "Return to browser" => MainMenuAction.Browse,
            "Change root node" => MainMenuAction.ChangeRootNode,
            "Review selected endpoints" => MainMenuAction.ReviewSelection,
            _ when choice.StartsWith("Start streaming") => MainMenuAction.StartStreaming,
            _ => MainMenuAction.Exit,
        };

        return Task.FromResult(action);
    }

    public Task<BrowseWorkspaceResult> PromptBrowseWorkspaceAsync(
        ExplorerSessionState state,
        CancellationToken ct = default)
    {
        var focusedColumnIndex = state.BrowseColumns.Count == 0
            ? 0
            : Math.Clamp(state.FocusedBrowseColumnIndex, 0, state.BrowseColumns.Count - 1);
        var selectedIndices = state.BrowseColumns
            .Select(column => column.VisibleResources.Count == 0
                ? -1
                : Math.Clamp(column.SelectedIndex, 0, column.VisibleResources.Count - 1))
            .ToArray();

        // Clear once to establish a fixed origin, then reuse cursor-home on every
        // subsequent frame so we overwrite in-place instead of blank-and-redraw.
        AnsiConsole.Clear();
        Console.CursorVisible = false;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                RenderBrowseWorkspace(state, focusedColumnIndex, selectedIndices);

                var keyInfo = Console.ReadKey(intercept: true);
                var key = keyInfo.Key;

                // '/' opens the search/filter input (cross-platform via KeyChar)
                if (keyInfo.KeyChar == '/')
                {
                    Console.CursorVisible = true;
                    Console.SetCursorPosition(0, Console.WindowHeight - 1);
                    Console.Write("\x1b[2K"); // clear line
                    Console.Write("Search (empty to clear): ");
                    var rawTerm = Console.ReadLine() ?? string.Empty;
                    Console.CursorVisible = false;
                    return Task.FromResult(new BrowseWorkspaceResult
                    {
                        Action = BrowseWorkspaceAction.UpdateSearchTerm,
                        FocusedColumnIndex = focusedColumnIndex,
                        SelectedIndices = selectedIndices,
                        UpdatedSearchTerm = rawTerm,
                    });
                }

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        MoveSelection(state.BrowseColumns, selectedIndices, focusedColumnIndex, -1);
                        break;

                    case ConsoleKey.DownArrow:
                        MoveSelection(state.BrowseColumns, selectedIndices, focusedColumnIndex, 1);
                        break;

                    case ConsoleKey.LeftArrow:
                        if (focusedColumnIndex > 0)
                            focusedColumnIndex--;
                        break;

                    case ConsoleKey.RightArrow:
                        if (focusedColumnIndex < state.BrowseColumns.Count - 1)
                            focusedColumnIndex++;
                        break;

                    case ConsoleKey.Enter:
                        return Task.FromResult(BuildBrowseResult(
                            BrowseWorkspaceAction.OpenFocusedResource,
                            focusedColumnIndex,
                            selectedIndices));

                    case ConsoleKey.Spacebar:
                        return Task.FromResult(BuildBrowseResult(
                            BrowseWorkspaceAction.SelectFocusedResourceForStreaming,
                            focusedColumnIndex,
                            selectedIndices));

                    case ConsoleKey.Backspace:
                        return Task.FromResult(BuildBrowseResult(
                            BrowseWorkspaceAction.GoBack,
                            focusedColumnIndex,
                            selectedIndices));

                    case ConsoleKey.Escape:
                        return Task.FromResult(BuildBrowseResult(
                            BrowseWorkspaceAction.ReturnToMain,
                            focusedColumnIndex,
                            selectedIndices));
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    public Task<IReadOnlyList<ResolvedStreamableEndpoint>> PromptEndpointSelectionAsync(
        IReadOnlyList<ResolvedStreamableEndpoint> endpoints,
        CancellationToken ct = default)
    {
        if (endpoints.Count == 0)
            return Task.FromResult<IReadOnlyList<ResolvedStreamableEndpoint>>([]);

        // Build display strings with all user-supplied values escaped so Spectre
        // does not interpret e.g. "[double]" as a markup tag.
        var choiceLabels = endpoints
            .Select(e =>
                $"{Markup.Escape(e.ChannelName)} " +
                $"[{Markup.Escape(e.DataType)}] " +
                $"({Markup.Escape(e.IndexType)})")
            .ToList();

        var labelToEndpoint = endpoints
            .Zip(choiceLabels, (endpoint, label) => (endpoint, label))
            .ToDictionary(x => x.label, x => x.endpoint);

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[green]Select endpoints to add:[/]")
                .PageSize(15)
                .NotRequired()
                .AddChoices(choiceLabels));

        var result = selected
            .Where(labelToEndpoint.ContainsKey)
            .Select(label => labelToEndpoint[label])
            .ToList();

        return Task.FromResult<IReadOnlyList<ResolvedStreamableEndpoint>>(result);
    }

    public Task<SelectionReviewAction> PromptSelectionReviewAsync(
        IReadOnlyList<SelectedEndpoint> selectedEndpoints,
        CancellationToken ct = default)
    {
        RenderSelectionTable(selectedEndpoints);

        if (selectedEndpoints.Count == 0)
            return Task.FromResult(SelectionReviewAction.Done);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Selection actions:[/]")
                .AddChoices("Remove one endpoint", "Clear all", "Done"));

        var action = choice switch
        {
            "Remove one endpoint" => SelectionReviewAction.RemoveOne,
            "Clear all" => SelectionReviewAction.ClearAll,
            _ => SelectionReviewAction.Done,
        };

        return Task.FromResult(action);
    }

    public Task<SelectedEndpoint?> PromptRemoveEndpointAsync(
        IReadOnlyList<SelectedEndpoint> selectedEndpoints,
        CancellationToken ct = default)
    {
        if (selectedEndpoints.Count == 0)
            return Task.FromResult<SelectedEndpoint?>(null);

        var choices = selectedEndpoints
            .Select(e => e.Endpoint.ChannelName)
            .ToList();

        var chosen = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Choose endpoint to remove:[/]")
                .AddChoices(choices));

        var result = selectedEndpoints.FirstOrDefault(e => e.Endpoint.ChannelName == chosen);
        return Task.FromResult(result);
    }

    private int _lastStreamRenderLines;

    public void ResetStreamView() => _lastStreamRenderLines = 0;

    public void RenderStreamSnapshot(StreamViewSnapshot snapshot)
    {
        // On subsequent renders move cursor back to the start of the previous output
        // and erase to end of screen. Using relative movement (\x1b[NA) keeps this
        // correct even when the first render caused the terminal to scroll.
        // Only emit raw ANSI when the terminal is interactive and supports escape sequences;
        // redirected or non-ANSI output would otherwise show the codes as garbage.
        if (_lastStreamRenderLines > 0 && !Console.IsOutputRedirected && AnsiConsole.Profile.Capabilities.Ansi)
            Console.Write($"\x1b[{_lastStreamRenderLines}A\x1b[J");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]Channel[/]").LeftAligned())
            .AddColumn(new TableColumn("[grey]Index[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Value[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Status[/]").LeftAligned());

        foreach (var row in snapshot.Rows)
        {
            var statusColor = row.RowStatus switch
            {
                RowStatusField.Live => "green",
                RowStatusField.Changed => "yellow",
                RowStatusField.StatusChanged => "blue",
                RowStatusField.Ended => "red",
                _ => "grey",
            };

            table.AddRow(
                Markup.Escape(row.ChannelName),
                Markup.Escape(row.PrimaryIndexText),
                Markup.Escape(row.ValueText),
                $"[{statusColor}]{Markup.Escape(row.StatusText)}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]Press [bold]S[/] or [bold]Escape[/] to stop streaming.[/]");

        // top-border(1) + header(1) + separator(1) + rows(N) + bottom-border(1) + hint(1)
        _lastStreamRenderLines = snapshot.Rows.Count + 5;
    }

    public Task<bool> PromptStopStreamingAsync(CancellationToken ct = default)
    {
        if (Console.IsInputRedirected)
            return Task.FromResult(false);

        try
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.S || key.Key == ConsoleKey.Escape)
                    return Task.FromResult(true);
            }
        }
        catch (InvalidOperationException)
        {
            // No interactive console available; treat as not stopped.
        }

        return Task.FromResult(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static BrowseWorkspaceResult BuildBrowseResult(
        BrowseWorkspaceAction action,
        int focusedColumnIndex,
        IReadOnlyList<int> selectedIndices) => new()
        {
            Action = action,
            FocusedColumnIndex = focusedColumnIndex,
            SelectedIndices = selectedIndices.ToArray(),
        };

    private static void MoveSelection(
        IReadOnlyList<ExplorerBrowseColumn> columns,
        int[] selectedIndices,
        int focusedColumnIndex,
        int delta)
    {
        if (columns.Count == 0 || focusedColumnIndex < 0 || focusedColumnIndex >= columns.Count)
            return;

        var column = columns[focusedColumnIndex];
        var visible = column.VisibleResources;
        if (visible.Count == 0)
        {
            selectedIndices[focusedColumnIndex] = -1;
            return;
        }

        var currentIndex = selectedIndices[focusedColumnIndex] < 0 ? 0 : selectedIndices[focusedColumnIndex];
        selectedIndices[focusedColumnIndex] = Math.Clamp(currentIndex + delta, 0, visible.Count - 1);
    }

    private static void RenderBrowseWorkspace(
        ExplorerSessionState state,
        int focusedColumnIndex,
        IReadOnlyList<int> selectedIndices)
    {
        // Cursor-home: overwrite in-place without a full clear to avoid flicker.
        Console.SetCursorPosition(0, 0);
        var windowHeight = Console.WindowHeight;
        var windowWidth  = Console.WindowWidth;

        // ── Header (full width) ───────────────────────────────────────────────
        var root    = state.SelectedRootNode?.Name ?? "(none)";
        var current = GetFocusedResourceName(state.BrowseColumns, focusedColumnIndex, selectedIndices);
        var path    = state.NavigationStack.Count > 0
            ? string.Join(" > ", state.NavigationStack.Select(Markup.Escape))
            : Markup.Escape(state.CurrentUri);

        AnsiConsole.Write(new Panel(
                $"[green bold]ETP Explorer[/]\n" +
                $"[grey]Root:[/] [cyan]{Markup.Escape(root)}[/]    [grey]Focused:[/] [yellow]{Markup.Escape(current)}[/]    [grey]Selected endpoints:[/] [green]{state.SelectionSet.Count}[/]\n" +
                $"[grey]Path:[/] [cyan]{path}[/]")
            .Border(BoxBorder.Rounded)
            .Header(" Browse ", Justify.Left)
            .Expand());
        AnsiConsole.WriteLine();

        // ── Compute layout ────────────────────────────────────────────────────
        // Measure cursor row after header so column height fills the gap precisely.
        var headerEndRow = Console.CursorTop;

        var statusMessage = string.IsNullOrWhiteSpace(state.LastStatusMessage)
            ? "Use arrow keys to move, Enter to open, Space to add the focused node for streaming."
            : state.LastStatusMessage;

        // Plain-text length for the controls bar (markup tags are not visible chars).
        const string ControlsPlainText =
            "Keys: Up/Down scroll  Left/Right change column  Enter open  Space add for streaming  Backspace close pane  / search  Esc main menu";

        var statusLines   = PanelHeight(statusMessage.Length,     windowWidth);
        var controlsLines = PanelHeight(ControlsPlainText.Length, windowWidth);

        // Pin the bottom section so the controls panel's last row is windowHeight-2.
        // The trailing newline Spectre appends after the bottom border lands on
        // windowHeight-1, never triggering a terminal scroll.
        var bottomSectionLines = statusLines + 1 + controlsLines; // status + blank + controls
        var bottomSectionTop   = windowHeight - 1 - bottomSectionLines;

        // Column height fills the available space between header and bottom section.
        // -3 = column-panel top-border(1) + bottom-border(1) + blank line after columns(1).
        var visibleRows = Math.Max(4, bottomSectionTop - headerEndRow - 3);

        // ── Column panels ─────────────────────────────────────────────────────
        // First two columns: fixed 36-char width (32 content + 2 border + 2 padding).
        // Third column: fills the remaining terminal width.
        const int FixedPanelWidth = 36;
        const int FixedMaxNameLength = 26; // content(32) - prefix(2) - max-badges(4)

        IRenderable columnsSection;
        if (state.BrowseColumns.Count == 0)
        {
            columnsSection = new Panel("[grey]No browse columns loaded.[/]").Header(" Empty ").Expand();
        }
        else
        {
            var grid = new Grid();
            for (var i = 0; i < state.BrowseColumns.Count; i++)
            {
                grid.AddColumn(i < 2
                    ? new GridColumn().Width(FixedPanelWidth)
                    : new GridColumn());
            }

            grid.AddRow(state.BrowseColumns
                .Select((column, index) => (IRenderable)CreateBrowsePanel(
                    column,
                    index == focusedColumnIndex,
                    index < selectedIndices.Count ? selectedIndices[index] : column.SelectedIndex,
                    visibleRows,
                    index < 2 ? FixedMaxNameLength : null))
                .ToArray());

            columnsSection = grid;
        }

        AnsiConsole.Write(columnsSection);
        AnsiConsole.WriteLine();
        // Erase any stale content between the column area and the bottom section.
        Console.Write("\x1b[J");

        // ── Bottom section: Status + Controls (pinned) ────────────────────────
        // Always jump unconditionally — column content and terminal resizes may
        // leave the cursor anywhere above bottomSectionTop.
        Console.SetCursorPosition(0, bottomSectionTop);

        AnsiConsole.Write(new Panel($"[white]{Markup.Escape(statusMessage)}[/]")
            .Border(BoxBorder.Rounded)
            .Header(" Status ", Justify.Left)
            .Expand());
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Panel(
                "[grey]Keys:[/] [bold]Up/Down[/] scroll  [bold]Left/Right[/] change column  [bold]Enter[/] open  [bold]Space[/] add for streaming  [bold]Backspace[/] close pane  [bold]/[/] search  [bold]Esc[/] main menu")
            .Border(BoxBorder.Rounded)
            .Header(" Controls ", Justify.Left)
            .Expand());

        // Cursor is now at windowHeight-1 (the newline following the controls
        // bottom border). Erase the final row cleanly — no scroll triggered.
        Console.Write("\x1b[J");
    }

    /// <summary>
    /// Returns the number of rows a full-width <see cref="Panel"/> will occupy for
    /// content of the given plain-text length.
    /// Layout: 1 top-border + N content rows + 1 bottom-border.
    /// Inner content width = consoleWidth − 2 (borders) − 2 (default Panel padding l+r).
    /// </summary>
    private static int PanelHeight(int plainTextLength, int consoleWidth)
    {
        var innerWidth   = Math.Max(1, consoleWidth - 4);
        var contentLines = Math.Max(1, (int)Math.Ceiling((double)plainTextLength / innerWidth));
        return 2 + contentLines;
    }

    private static string GetFocusedResourceName(
        IReadOnlyList<ExplorerBrowseColumn> columns,
        int focusedColumnIndex,
        IReadOnlyList<int> selectedIndices)
    {
        if (columns.Count == 0 || focusedColumnIndex < 0 || focusedColumnIndex >= columns.Count)
            return "(none)";

        var column = columns[focusedColumnIndex];
        var visible = column.VisibleResources;
        if (visible.Count == 0)
            return string.IsNullOrWhiteSpace(column.SearchTerm) ? "(empty)" : "(no matches)";

        var selectedIndex = focusedColumnIndex < selectedIndices.Count
            ? selectedIndices[focusedColumnIndex]
            : column.SelectedIndex;

        if (selectedIndex < 0 || selectedIndex >= visible.Count)
            return "(none)";

        return visible[selectedIndex].Name;
    }

    private static Panel CreateBrowsePanel(
        ExplorerBrowseColumn column,
        bool isFocused,
        int selectedIndex,
        int visibleRows,
        int? maxNameLength = null)
    {
        var content = BuildColumnContent(column, isFocused, selectedIndex, visibleRows, maxNameLength);
        var searchSuffix = string.IsNullOrWhiteSpace(column.SearchTerm)
            ? string.Empty
            : $" [yellow]/[/][grey]{Markup.Escape(column.SearchTerm)}[/]";
        var title = isFocused
            ? $"[bold yellow]▶ {Markup.Escape(column.Title)}[/]{searchSuffix}"
            : $"{Markup.Escape(column.Title)}{searchSuffix}";

        return new Panel(new Markup(content))
            .Border(isFocused ? BoxBorder.Double : BoxBorder.Rounded)
            .Header($" {title} ", Justify.Left)
            .Padding(1, 0, 1, 0)
            .Expand();
    }

    private static string BuildColumnContent(ExplorerBrowseColumn column, bool isFocused, int selectedIndex, int visibleRows, int? maxNameLength = null)
    {
        var visibleResources = column.VisibleResources;

        if (visibleResources.Count == 0)
        {
            return string.IsNullOrWhiteSpace(column.SearchTerm)
                ? "[grey]No child nodes[/]"
                : $"[yellow]No matches for \"[/][grey]{Markup.Escape(column.SearchTerm)}[/][yellow]\"[/]";
        }

        var safeSelectedIndex = Math.Clamp(selectedIndex, 0, visibleResources.Count - 1);
        var start = Math.Max(0, safeSelectedIndex - (visibleRows / 2));
        var end = Math.Min(visibleResources.Count, start + visibleRows);
        start = Math.Max(0, end - visibleRows);

        var lines = new List<string>();
        if (start > 0)
            lines.Add("[grey]...[/]");

        for (var i = start; i < end; i++)
        {
            var resource = visibleResources[i];
            var name = (maxNameLength.HasValue && resource.Name.Length > maxNameLength.Value)
                ? resource.Name[..(maxNameLength.Value - 1)] + "\u2026"
                : resource.Name;

            var badges = new List<string>();
            if (resource.HasChildren != 0)
                badges.Add("[blue]>[/]");
            if (resource.ChannelSubscribable)
                badges.Add("[green]S[/]");

            var badgeText = badges.Count == 0 ? string.Empty : $" [grey]{string.Join(" ", badges)}[/]";
            var label = $"{Markup.Escape(name)}{badgeText}";

            if (i == safeSelectedIndex)
            {
                if (isFocused)
                    lines.Add($"[bold black on yellow]▶ {label}[/]");
                else
                    lines.Add($"[black on yellow]▷ {label}[/]");
            }
            else
                lines.Add($"[white]  {label}[/]");
        }

        if (end < visibleResources.Count)
            lines.Add("[grey]...[/]");

        while (lines.Count < visibleRows)
            lines.Add(" ");

        return string.Join("\n", lines);
    }

    private static void RenderSelectionTable(IReadOnlyList<SelectedEndpoint> selected)
    {
        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No endpoints selected.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("#")
            .AddColumn("Channel")
            .AddColumn("Type")
            .AddColumn("Index")
            .AddColumn("Source URI");

        for (var i = 0; i < selected.Count; i++)
        {
            var endpoint = selected[i].Endpoint;
            table.AddRow(
                (i + 1).ToString(),
                Markup.Escape(endpoint.ChannelName),
                Markup.Escape(endpoint.DataType),
                Markup.Escape(endpoint.IndexType),
                Markup.Escape(endpoint.SourceResourceUri));
        }

        AnsiConsole.Write(table);
    }
}
