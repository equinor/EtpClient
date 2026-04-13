using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for filtering the active column to a smaller working set:
/// apply, refine, restore, and no-match navigation.
/// </summary>
public sealed class ExplorerColumnFilterWorkflowTests
{
    // ── Apply and refine filter ───────────────────────────────────────────────

    [Fact]
    public void VisibleResources_FilterApplied_ShowsOnlyMatchingItems()
    {
        var column = BuildColumn("Well Alpha", "Well Bravo", "Log One", "Log Two");
        column.SearchTerm = "Log";

        Assert.Equal(2, column.VisibleResources.Count);
        Assert.All(column.VisibleResources, r =>
            Assert.Contains("Log", r.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VisibleResources_FilterRefined_ReducesMatchCount()
    {
        var column = BuildColumn("Well Alpha", "Well Bravo", "Well Charlie");
        column.SearchTerm = "Well";
        Assert.Equal(3, column.VisibleResources.Count);

        column.SearchTerm = "Well B";
        Assert.Single(column.VisibleResources);
        Assert.Equal("Well Bravo", column.VisibleResources[0].Name);
    }

    [Fact]
    public void VisibleResources_FilterCleared_RestoresFullList()
    {
        var column = BuildColumn("Well Alpha", "Well Bravo", "Log One");
        column.SearchTerm = "Well";
        Assert.Equal(2, column.VisibleResources.Count);

        column.SearchTerm = string.Empty;
        Assert.Equal(3, column.VisibleResources.Count);
    }

    [Fact]
    public void VisibleResources_FilterNoMatches_ReturnsEmpty()
    {
        var column = BuildColumn("Well Alpha", "Well Bravo");
        column.SearchTerm = "Log";
        Assert.Empty(column.VisibleResources);
    }

    // ── Navigate within filtered list ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_FilterActive_CanOpenFilteredResourceViaEnter()
    {
        const string rootUri = "eml:///witsml14";
        var wellUri = $"{rootUri}/well_alpha";
        var client = BuildClientWithRoot(rootUri,
            ("Well Alpha", wellUri, 1),
            ("Well Bravo", $"{rootUri}/well_bravo", 0));
        client.DiscoverResultsByUri[wellUri] = FakeExplorerClient.BuildEmptyDiscoveryResult(wellUri);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        // Apply filter to show only "Well Alpha" (index 0 in filtered list)
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
            UpdatedSearchTerm = "Alpha",
        });

        // Open the focused (filtered) resource
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.OpenFocusedResource,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });

        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 1,
            SelectedIndices = [0, -1],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(ui.ErrorMessages);
        // Discovery was called for the filtered resource
        Assert.Contains(wellUri, client.DiscoveredUris);
    }

    [Fact]
    public async Task RunAsync_FilterWithNoMatches_ThenRefine_RecoversBrowseSession()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoot(rootUri,
            ("Well Alpha", $"{rootUri}/well_alpha", 0),
            ("Well Bravo", $"{rootUri}/well_bravo", 0));

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        // Apply a no-match filter
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [-1],
            UpdatedSearchTerm = "ZZZZ",
        });

        // Refine to a matching filter
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
            UpdatedSearchTerm = "Alpha",
        });

        // Snapshot here should have 1 visible item
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(ui.ErrorMessages);

        var lastSnapshot = ui.BrowseSnapshots[^1];
        Assert.Equal("Alpha", lastSnapshot.BrowseColumns[0].SearchTerm);
        Assert.Single(lastSnapshot.BrowseColumns[0].VisibleResources);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BrowseableResource MakeResource(string name) => new()
    {
        Uri = $"eml:///test/{name.Replace(" ", "_")}",
        Name = name,
        ResourceType = "DataObject",
        ContentType = "application/x-test",
        HasChildren = 0,
        ChannelSubscribable = false,
    };

    private static ExplorerBrowseColumn BuildColumn(params string[] names) =>
        new()
        {
            Title = "Test",
            ParentUri = "eml:///test",
            Resources = names.Select(MakeResource).ToList(),
            SelectedIndex = 0,
        };

    private static FakeExplorerClient BuildClientWithRoot(
        string rootUri,
        params (string name, string uri, int hasChildren)[] items)
    {
        var resources = items
            .Select(i => FakeExplorerClient.BuildResource(i.uri, i.name, hasChildren: i.hasChildren))
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
