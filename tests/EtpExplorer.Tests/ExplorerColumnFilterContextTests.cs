using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for active-column-only scoping: search and filter must not affect
/// other visible columns and per-column state must persist across focus changes.
/// </summary>
public sealed class ExplorerColumnFilterContextTests
{
    // ── Active-column scoping ────────────────────────────────────────────────

    [Fact]
    public void SearchTerm_IsIndependentPerColumn()
    {
        var col1 = BuildColumn("Well Alpha", "Well Bravo");
        col1.SearchTerm = "Alpha";
        var col2 = BuildColumn("Log A", "Log B");

        // col1 is filtered; col2 is not
        Assert.Single(col1.VisibleResources);
        Assert.Equal(2, col2.VisibleResources.Count);
    }

    [Fact]
    public async Task RunAsync_FilterOnFocusedColumn_DoesNotAffectSiblingColumn()
    {
        const string rootUri = "eml:///witsml14";
        const string wellUri = "eml:///witsml14/well(1)";
        var client = BuildClientWithRoot(
            rootUri,
            ("Well Alpha", wellUri, 1),
            ("Well Bravo", "eml:///witsml14/well(2)", 0));
        client.DiscoverResultsByUri[wellUri] = FakeExplorerClient.BuildDiscoveryResult(wellUri,
        [
            FakeExplorerClient.BuildResource("eml:///witsml14/well(1)/log(1)", "Log A"),
            FakeExplorerClient.BuildResource("eml:///witsml14/well(1)/log(2)", "Log B"),
        ]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        // Open Well Alpha to create a second column
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.OpenFocusedResource,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });

        // Focus moves to column 1 (the child column with Log A, Log B)
        // Apply a filter on column 1 - should not affect column 0
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 1,
            SelectedIndices = [0, 0],
            UpdatedSearchTerm = "Log A",
        });

        // Capture snapshot
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 1,
            SelectedIndices = [0, 0],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        var snapshot = ui.BrowseSnapshots[^1];
        Assert.Equal(2, snapshot.BrowseColumns.Count);

        // Column 0 (parent) should be unfiltered — two rows
        Assert.Equal(string.Empty, snapshot.BrowseColumns[0].SearchTerm);
        Assert.Equal(2, snapshot.BrowseColumns[0].VisibleResources.Count);

        // Column 1 (child) should be filtered — one match
        Assert.Equal("Log A", snapshot.BrowseColumns[1].SearchTerm);
        Assert.Single(snapshot.BrowseColumns[1].VisibleResources);
    }

    // ── Per-column state persistence ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SearchTermPreservedOnColumn_WhenFocusMoves()
    {
        const string rootUri = "eml:///witsml14";
        const string wellUri = "eml:///witsml14/well(1)";
        var client = BuildClientWithRoot(
            rootUri,
            ("Well Alpha", wellUri, 1),
            ("Well Bravo", "eml:///witsml14/well(2)", 0));
        client.DiscoverResultsByUri[wellUri] = FakeExplorerClient.BuildDiscoveryResult(wellUri,
        [
            FakeExplorerClient.BuildResource("eml:///witsml14/well(1)/log(1)", "Log X"),
        ]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });

        // Apply filter on column 0 first
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.UpdateSearchTerm,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
            UpdatedSearchTerm = "Alpha",
        });

        // Open the filtered resource so a child column is created; focus shifts to column 1
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.OpenFocusedResource,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });

        // Capture a snapshot while on column 1; column 0 should still carry its search term
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 1,
            SelectedIndices = [0, 0],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        var snapshot = ui.BrowseSnapshots[^1];
        Assert.Equal(2, snapshot.BrowseColumns.Count);

        // Column 0 retains its search term after focus moved to column 1
        Assert.Equal("Alpha", snapshot.BrowseColumns[0].SearchTerm);
        Assert.Single(snapshot.BrowseColumns[0].VisibleResources);
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
