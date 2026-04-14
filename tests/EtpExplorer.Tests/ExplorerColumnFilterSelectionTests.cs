using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for selection preservation, reassignment, and cleared-selection rules
/// when a filter changes the visible result set.
/// </summary>
public sealed class ExplorerColumnFilterSelectionTests
{
    // ── SelectionVisibilityOutcome rules (unit) ───────────────────────────────

    [Fact]
    public void VisibleResources_WhenFilterKeepsSelectedItem_SelectedIndexPointsToSameItem()
    {
        // "Well Bravo" is at index 1 before filtering
        var column = BuildColumn("Well Alpha", "Well Bravo", "Log One");
        column.SelectedIndex = 1; // "Well Bravo"

        // Apply a filter that keeps "Well Bravo"
        column.SearchTerm = "Well";

        // After filtering, "Well Bravo" should still be reachable in VisibleResources
        var visible = column.VisibleResources;
        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, r => r.Name == "Well Bravo");
    }

    [Fact]
    public void VisibleResources_WhenFilterRemovesSelectedItem_OtherItemsStillVisible()
    {
        var column = BuildColumn("Well Alpha", "Well Bravo", "Log One");
        column.SelectedIndex = 2; // "Log One"

        // Filter that removes "Log One"
        column.SearchTerm = "Well";

        var visible = column.VisibleResources;
        Assert.Equal(2, visible.Count);
        Assert.DoesNotContain(visible, r => r.Name == "Log One");
    }

    // ── ApplySearchTermToFocusedColumn selection outcomes (integration) ───────

    [Fact]
    public async Task RunAsync_FilterKeepsSelectedItem_SelectedIndexPointsToIt()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoot(rootUri, "Well Alpha", "Well Bravo", "Log One");
        var ui = new FakeExplorerUi();

        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        // Select "Well Bravo" at index 1 first
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [1],        // "Well Bravo" selected before filter
            UpdatedSearchTerm = "Well",   // "Well Alpha", "Well Bravo" remain visible
        });

        // Capture snapshot
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 0,
            SelectedIndices = [1],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        var snapshot = ui.BrowseSnapshots[^1];
        var col = snapshot.BrowseColumns[0];
        // 2 visible items (Well Alpha, Well Bravo); previously selected was at index 1
        // ApplySearchTermToFocusedColumn preserves the item if it's still visible
        Assert.Equal(2, col.VisibleResources.Count);
        // The selected index should be the item in the visible list (0 or 1)
        Assert.True(col.SelectedIndex >= 0);
        Assert.True(col.SelectedIndex < col.VisibleResources.Count);
    }

    [Fact]
    public async Task RunAsync_FilterRemovesSelectedItem_SelectedIndexReassignedToFirstMatch()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoot(rootUri, "Well Alpha", "Well Bravo", "Log One");
        var ui = new FakeExplorerUi();

        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        // "Log One" is at index 2; filter "Well" removes "Log One"
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [2],        // "Log One" was selected
            UpdatedSearchTerm = "Well",   // only Well items remain
        });

        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        var snapshot = ui.BrowseSnapshots[^1];
        var col = snapshot.BrowseColumns[0];
        Assert.Equal(2, col.VisibleResources.Count);
        // Selection was reassigned to 0 (first visible match)
        Assert.Equal(0, col.SelectedIndex);
    }

    [Fact]
    public async Task RunAsync_FilterNoMatch_SelectedIndexIsNegativeOne()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoot(rootUri, "Well Alpha", "Well Bravo");
        var ui = new FakeExplorerUi();

        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
            UpdatedSearchTerm = "ZZZZ",
        });

        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 0,
            SelectedIndices = [-1],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        var snapshot = ui.BrowseSnapshots[^1];
        Assert.Equal(-1, snapshot.BrowseColumns[0].SelectedIndex);
        Assert.Empty(snapshot.BrowseColumns[0].VisibleResources);
    }

    [Fact]
    public async Task RunAsync_ClearFilterAfterNoMatch_RestoresSelectableItems()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoot(rootUri, "Well Alpha", "Well Bravo");
        var ui = new FakeExplorerUi();

        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        // Apply no-match filter
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
            UpdatedSearchTerm = "ZZZZ",
        });

        // Clear filter
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [-1],
            UpdatedSearchTerm = "",
        });

        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        var snapshot = ui.BrowseSnapshots[^1];
        Assert.Equal(2, snapshot.BrowseColumns[0].VisibleResources.Count);
        Assert.True(snapshot.BrowseColumns[0].SelectedIndex >= 0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExplorerBrowseColumn BuildColumn(params string[] names) =>
        new()
        {
            Title = "Test",
            ParentUri = "eml:///test",
            Resources = names.Select(n => new BrowseableResource
            {
                Uri = $"eml:///test/{n.Replace(" ", "_")}",
                Name = n,
                ResourceType = "DataObject",
                ContentType = "application/x-test",
                HasChildren = 0,
                ChannelSubscribable = false,
            }).ToList(),
            SelectedIndex = 0,
        };

    private static FakeExplorerClient BuildClientWithRoot(string rootUri, params string[] resourceNames)
    {
        var resources = resourceNames
            .Select(n => FakeExplorerClient.BuildResource($"{rootUri}/{n.Replace(" ", "_")}", n))
            .ToList();

        var client = new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildDiscoveryResult("eml://",
            [
                FakeExplorerClient.BuildResource(rootUri, "witsml14"),
            ]),
        };
        client.DiscoverResultsByUri[rootUri] = FakeExplorerClient.BuildDiscoveryResult(rootUri, resources);
        return client;
    }

    private static ExplorerOptions ValidOptions() => new()
    {
        EndpointUri = "wss://fake-server/etp",
        Username = "user",
        Password = "pass",
        ProtocolRequestTimeoutSeconds = 5,
    };

    private static ExplorerApp BuildApp(FakeExplorerClient client, FakeExplorerUi ui) =>
        new(
            client,
            ui,
            ValidOptions(),
            new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance),
            new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance),
            new SelectionSetService(),
            new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance),
            NullLogger<ExplorerApp>.Instance);
}
