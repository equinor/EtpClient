using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for filter rendering: no-match feedback, filtered-list rendering, and
/// status distinguishing between empty columns and no-match states.
/// </summary>
public sealed class ExplorerColumnFilterRenderingTests
{
    // ── No-match vs empty-column distinction ──────────────────────────────────

    [Fact]
    public void VisibleResources_EmptyColumn_DistinctFromNoMatchFilter()
    {
        // An empty column has no resources
        var emptyColumn = new ExplorerBrowseColumn
        {
            Title = "Empty",
            ParentUri = "eml:///test",
            Resources = [],
            SelectedIndex = -1,
        };
        Assert.Empty(emptyColumn.VisibleResources);
        Assert.Empty(emptyColumn.SearchTerm);

        // A column with resources but an active filter that matches nothing
        var filteredColumn = new ExplorerBrowseColumn
        {
            Title = "Filtered",
            ParentUri = "eml:///test",
            Resources =
            [
                new BrowseableResource
                {
                    Uri = "eml:///test/well(1)",
                    Name = "Well A",
                    ResourceType = "DataObject",
                    ContentType = "application/x-test",
                    HasChildren = 0,
                    ChannelSubscribable = false,
                },
            ],
            SelectedIndex = 0,
            SearchTerm = "Log",
        };
        Assert.Empty(filteredColumn.VisibleResources);
        Assert.False(string.IsNullOrEmpty(filteredColumn.SearchTerm));
    }

    [Fact]
    public void VisibleResources_ActiveFilter_ShowsMatchCountNotTotalCount()
    {
        var column = new ExplorerBrowseColumn
        {
            Title = "Test",
            ParentUri = "eml:///test",
            Resources =
            [
                new BrowseableResource { Uri = "eml:///test/a", Name = "Well Alpha", ResourceType = "DataObject", ContentType = "x", HasChildren = 0, ChannelSubscribable = false },
                new BrowseableResource { Uri = "eml:///test/b", Name = "Well Bravo", ResourceType = "DataObject", ContentType = "x", HasChildren = 0, ChannelSubscribable = false },
                new BrowseableResource { Uri = "eml:///test/c", Name = "Log One",   ResourceType = "DataObject", ContentType = "x", HasChildren = 0, ChannelSubscribable = false },
            ],
            SelectedIndex = 0,
            SearchTerm = "Log",
        };

        Assert.Equal(3, column.Resources.Count);
        Assert.Single(column.VisibleResources);
    }

    // ── Status message flow ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_FilterWithMatches_CompletesWithoutError()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoot(rootUri, "Well Alpha", "Log One");
        var ui = new FakeExplorerUi();

        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
            UpdatedSearchTerm = "Log",
        });
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

        // After filter applied, visible resources in snapshot should be 1 (only "Log One")
        var snapshot = ui.BrowseSnapshots[^1];
        Assert.Single(snapshot.BrowseColumns[0].VisibleResources);
        Assert.Equal("Log One", snapshot.BrowseColumns[0].VisibleResources[0].Name);
    }

    [Fact]
    public async Task RunAsync_FilterNoMatch_SetsNegativeSelectedIndexInSnapshot()
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
    public async Task RunAsync_FilterClear_RestoresAllResourcesInSnapshot()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoot(rootUri, "Well Alpha", "Well Bravo", "Log One");
        var ui = new FakeExplorerUi();

        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
            UpdatedSearchTerm = "Log",
        });
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
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
        Assert.Equal(3, snapshot.BrowseColumns[0].VisibleResources.Count);
        Assert.True(string.IsNullOrEmpty(snapshot.BrowseColumns[0].SearchTerm));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
