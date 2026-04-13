using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for browse rendering: resource lists, navigation context (breadcrumbs),
/// root-node prompts, and status messaging.
/// </summary>
public sealed class ExplorerBrowseRenderingTests
{
    // ── BrowseableResource formatting ─────────────────────────────────────────

    [Fact]
    public void MapResources_ChannelSubscribableResource_HasChannelSubscribableTrue()
    {
        var service = new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance);
        var result = FakeExplorerClient.BuildDiscoveryResult("eml:///witsml14",
        [
            FakeExplorerClient.BuildResource(
                "eml:///witsml14/log(x)",
                "SomeLog",
                resourceType: "DataObject",
                channelSubscribable: true,
                hasChildren: 0)
        ]);

        var resources = service.MapResources(result);

        Assert.Single(resources);
        Assert.True(resources[0].ChannelSubscribable);
    }

    [Fact]
    public void MapResources_NonSubscribableResource_HasChannelSubscribableFalse()
    {
        var service = new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance);
        var result = FakeExplorerClient.BuildDiscoveryResult("eml:///witsml14",
        [
            FakeExplorerClient.BuildResource("eml:///witsml14/well(y)", "Well Y", channelSubscribable: false)
        ]);

        var resources = service.MapResources(result);

        Assert.False(resources[0].ChannelSubscribable);
    }

    // ── Breadcrumb / navigation stack ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AfterRootSelection_NavigationStackContainsRootName()
    {
        var client = new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildDiscoveryResult("eml://",
            [
                FakeExplorerClient.BuildResource("eml:///witsml14", "witsml14"),
            ]),
        };
        client.DiscoverResultsByUri["eml:///witsml14"] = FakeExplorerClient.BuildEmptyDiscoveryResult("eml:///witsml14");

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = new ExplorerApp(
            client,
            ui,
            ValidOptions(),
            new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance),
            new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance),
            new SelectionSetService(),
            new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance),
            NullLogger<ExplorerApp>.Instance);

        await app.RunAsync();

        Assert.NotEmpty(ui.BrowseSnapshots);
        Assert.Contains("witsml14", ui.BrowseSnapshots[0].NavigationStack);
    }

    [Fact]
    public async Task RunAsync_AfterOpeningChild_FocusedColumnMovesRight()
    {
        var client = new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildDiscoveryResult("eml://",
            [
                FakeExplorerClient.BuildResource("eml:///witsml14", "witsml14"),
            ]),
        };
        client.DiscoverResultsByUri["eml:///witsml14"] = FakeExplorerClient.BuildDiscoveryResult(
            "eml:///witsml14",
            [FakeExplorerClient.BuildResource("eml:///witsml14/well(1)", "Well A", hasChildren: 1)]);
        client.DiscoverResultsByUri["eml:///witsml14/well(1)"] = FakeExplorerClient.BuildEmptyDiscoveryResult("eml:///witsml14/well(1)");

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
            SelectedIndices = [0, -1],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildSimpleApp(client, ui);
        await app.RunAsync();

        Assert.True(ui.BrowseSnapshots.Count >= 2);
        Assert.Equal(1, ui.BrowseSnapshots[1].FocusedBrowseColumnIndex);
    }

    // ── Status messages on root-less discovery ─────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenRootDiscoveryReturnsEmpty_ShowsErrorAndExits()
    {
        var client = new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildEmptyDiscoveryResult("eml://"),
        };
        var ui = new FakeExplorerUi();
        var app = BuildSimpleApp(client, ui);

        var exit = await app.RunAsync();

        Assert.Equal(1, exit);
        Assert.NotEmpty(ui.ErrorMessages);
    }

    // ── Depth tracking ────────────────────────────────────────────────────────

    [Fact]
    public void MapResources_DepthIsPassedThrough()
    {
        var service = new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance);
        var result = FakeExplorerClient.BuildDiscoveryResult("eml:///r",
        [
            FakeExplorerClient.BuildResource("eml:///r/a", "A")
        ]);

        var resources = service.MapResources(result, depth: 3);

        Assert.Equal(3, resources[0].Depth);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExplorerOptions ValidOptions() => new()
    {
        EndpointUri = "wss://fake/etp",
        Username = "user",
        Password = "pass",
        ProtocolRequestTimeoutSeconds = 5,
    };

    private static ExplorerApp BuildSimpleApp(FakeExplorerClient client, FakeExplorerUi ui) =>
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
