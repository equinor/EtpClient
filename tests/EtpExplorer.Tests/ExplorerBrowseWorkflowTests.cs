using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for the pane-style browse workflow: root-node discovery, column loading,
/// child navigation, and empty-result handling.
/// </summary>
public sealed class ExplorerBrowseWorkflowTests
{
    private static ExplorerApp BuildApp(
        FakeExplorerClient client,
        FakeExplorerUi ui,
        SelectionSetService? selectionSet = null) =>
        new(
            client,
            ui,
            ValidOptions(),
            new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance),
            new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance),
            selectionSet ?? new SelectionSetService(),
            new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance),
            NullLogger<ExplorerApp>.Instance);

    // ── Root node discovery ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithMultipleRootNodes_PromptsRootNodeSelectionBeforePaneBrowse()
    {
        var client = BuildClientWithRoots("witsml14", "witsml20");
        client.DiscoverResultsByUri["eml:///witsml14"] = FakeExplorerClient.BuildEmptyDiscoveryResult("eml:///witsml14");

        var ui = new FakeExplorerUi();

        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        Assert.NotEmpty(ui.BrowseSnapshots);
        Assert.Equal("witsml14", ui.BrowseSnapshots[0].SelectedRootNode?.Name);
    }

    [Fact]
    public async Task RunAsync_AfterRootSelection_LoadsFirstColumnFromSelectedRoot()
    {
        const string rootUri = "eml:///witsml14";
        var client = BuildClientWithRoots("witsml14", "witsml20");
        client.DiscoverResultsByUri[rootUri] = FakeExplorerClient.BuildDiscoveryResult(rootUri,
        [
            FakeExplorerClient.BuildResource("eml:///witsml14/well(1)", "Well A", "DataObject", hasChildren: 1),
        ]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = rootUri });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.NotEmpty(ui.BrowseSnapshots);
        Assert.Contains(rootUri, client.DiscoveredUris);
        Assert.Single(ui.BrowseSnapshots[0].BrowseColumns);
        Assert.Equal(rootUri, ui.BrowseSnapshots[0].BrowseColumns[0].ParentUri);
        Assert.Equal("Well A", ui.BrowseSnapshots[0].BrowseColumns[0].Resources[0].Name);
    }

    // ── Root node mapping ─────────────────────────────────────────────────────

    [Fact]
    public void ExplorerBrowseService_MapRootNodes_ReturnsOnePerResource()
    {
        var service = new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance);
        var result = FakeExplorerClient.BuildDiscoveryResult("eml://",
        [
            FakeExplorerClient.BuildResource("eml:///witsml14", "witsml14"),
            FakeExplorerClient.BuildResource("eml:///witsml20", "witsml20"),
        ]);

        var nodes = service.MapRootNodes(result);

        Assert.Equal(2, nodes.Count);
        Assert.Equal("witsml14", nodes[0].Name);
        Assert.Equal("eml:///witsml14", nodes[0].Uri);
        Assert.Equal("witsml20", nodes[1].Name);
    }

    [Fact]
    public void ExplorerBrowseService_MapRootNodes_EmptyResult_ReturnsEmpty()
    {
        var service = new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance);
        var result = FakeExplorerClient.BuildEmptyDiscoveryResult("eml://");

        var nodes = service.MapRootNodes(result);

        Assert.Empty(nodes);
    }

    // ── Child navigation ──────────────────────────────────────────────────────

    [Fact]
    public void ExplorerBrowseService_MapResources_PreservesUriAndName()
    {
        var service = new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance);
        var parentUri = "eml:///witsml14";
        var result = FakeExplorerClient.BuildDiscoveryResult(parentUri,
        [
            FakeExplorerClient.BuildResource("eml:///witsml14/well(1)", "Well A", "DataObject", hasChildren: 1),
            FakeExplorerClient.BuildResource("eml:///witsml14/well(2)", "Well B", "DataObject", hasChildren: 0),
        ]);

        var resources = service.MapResources(result, parentUri, depth: 1);

        Assert.Equal(2, resources.Count);
        Assert.Equal("Well A", resources[0].Name);
        Assert.Equal("eml:///witsml14/well(1)", resources[0].Uri);
        Assert.Equal(1, resources[0].HasChildren);
        Assert.Equal(parentUri, resources[0].ParentUri);
        Assert.Equal(1, resources[0].Depth);
        Assert.Equal(0, resources[1].HasChildren);
    }

    [Fact]
    public void ExplorerBrowseService_MapResources_UsesUriAsNameWhenNameEmpty()
    {
        var service = new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance);
        var resource = new DiscoveredResource
        {
            Uri = "eml:///witsml14/well(x)",
            Name = "",    // empty name — should fall back to URI
            ContentType = "application/x-witsml+xml;type=well",
            ResourceType = "DataObject",
            HasChildren = 0,
            ChannelSubscribable = false,
            ObjectNotifiable = false,
        };
        var dr = new DiscoveryResult
        {
            RequestedUri = "eml:///witsml14",
            Resources = [resource],
            WasEmptyAcknowledged = false,
            MessageEncoding = EtpMessageEncoding.Binary,
        };

        var resources = service.MapResources(dr);

        Assert.Single(resources);
        Assert.Equal("eml:///witsml14/well(x)", resources[0].Name);
    }

    // ── Child navigation ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenOpeningChild_LoadsChildNodesIntoRightColumn()
    {
        var client = BuildClientWithRoots("witsml14");
        client.DiscoverResultsByUri["eml:///witsml14"] = FakeExplorerClient.BuildDiscoveryResult(
            "eml:///witsml14",
            [FakeExplorerClient.BuildResource("eml:///witsml14/well(1)", "Well A", "DataObject", hasChildren: 1)]);
        client.DiscoverResultsByUri["eml:///witsml14/well(1)"] = FakeExplorerClient.BuildDiscoveryResult(
            "eml:///witsml14/well(1)",
            [FakeExplorerClient.BuildResource("eml:///witsml14/well(1)/log(1)", "Log 1", "DataObject", channelSubscribable: true, hasChildren: 0)]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
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
            SelectedIndices = [0, 0],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        Assert.True(ui.BrowseSnapshots.Count >= 2);
        Assert.Equal(2, ui.BrowseSnapshots[1].BrowseColumns.Count);
        Assert.Equal("Well A", ui.BrowseSnapshots[1].BrowseColumns[1].Title);
        Assert.Equal("Log 1", ui.BrowseSnapshots[1].BrowseColumns[1].Resources[0].Name);
    }

    // ── Empty navigation ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenBrowseReturnsNoChildren_ShowsEmptyRootColumn()
    {
        var client = BuildClientWithRoots("witsml14");
        client.DiscoverResultsByUri["eml:///witsml14"] = FakeExplorerClient.BuildEmptyDiscoveryResult("eml:///witsml14");

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui);
        await app.RunAsync();

        Assert.Empty(ui.ErrorMessages);
        Assert.NotEmpty(ui.BrowseSnapshots);
        Assert.Empty(ui.BrowseSnapshots[0].BrowseColumns[0].Resources);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExplorerOptions ValidOptions() => new()
    {
        EndpointUri = "wss://fake-server/etp",
        Username = "user",
        Password = "pass",
        ProtocolRequestTimeoutSeconds = 5,
    };

    private static FakeExplorerClient BuildClientWithRoots(params string[] names)
    {
        var resources = names
            .Select(n => FakeExplorerClient.BuildResource($"eml:///{n}", n))
            .ToList();

        return new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildDiscoveryResult("eml://", resources),
        };
    }
}
