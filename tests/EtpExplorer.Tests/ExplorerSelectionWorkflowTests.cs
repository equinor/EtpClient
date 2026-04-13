using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for endpoint resolution (channel describe) and multi-select behavior.
/// </summary>
public sealed class ExplorerSelectionWorkflowTests
{
    // ── Endpoint resolver ─────────────────────────────────────────────────────

    [Fact]
    public void ExplorerEndpointResolver_ResolveEndpoints_MapsAllActiveChannels()
    {
        var resolver = new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance);
        var describeResult = FakeExplorerClient.BuildDescribeResult(
            ["eml:///witsml14/log(x)"],
            [
                FakeExplorerClient.BuildChannel(1, "eml:///ch/1", "RPM", "Active"),
                FakeExplorerClient.BuildChannel(2, "eml:///ch/2", "WOB", "Active"),
            ]);

        var endpoints = resolver.ResolveEndpoints(describeResult, "eml:///witsml14/log(x)");

        Assert.Equal(2, endpoints.Count);
        Assert.All(endpoints, e => Assert.Equal("eml:///witsml14/log(x)", e.SourceResourceUri));
        Assert.Equal(1, endpoints[0].ChannelId);
        Assert.Equal("RPM", endpoints[0].ChannelName);
        Assert.Equal(2, endpoints[1].ChannelId);
    }

    [Fact]
    public void ExplorerEndpointResolver_ResolveEndpoints_ExcludesClosedChannels()
    {
        var resolver = new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance);
        var describeResult = FakeExplorerClient.BuildDescribeResult(
            ["eml:///witsml14/log(x)"],
            [
                FakeExplorerClient.BuildChannel(1, "eml:///ch/1", "RPM", "Active"),
                FakeExplorerClient.BuildChannel(2, "eml:///ch/2", "OLD", "Closed"),
                FakeExplorerClient.BuildChannel(3, "eml:///ch/3", "WOB", "Inactive"),
            ]);

        var endpoints = resolver.ResolveEndpoints(describeResult, "eml:///witsml14/log(x)");

        Assert.Equal(2, endpoints.Count);
        Assert.DoesNotContain(endpoints, e => e.ChannelName == "OLD");
    }

    [Fact]
    public void ExplorerEndpointResolver_ResolveEndpoints_EmptyResult_ReturnsEmpty()
    {
        var resolver = new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance);
        var describeResult = FakeExplorerClient.BuildEmptyDescribeResult(["eml:///witsml14/log(x)"]);

        var endpoints = resolver.ResolveEndpoints(describeResult, "eml:///witsml14/log(x)");

        Assert.Empty(endpoints);
    }

    // ── SelectionSetService: add ──────────────────────────────────────────────

    [Fact]
    public void SelectionSetService_AddEndpoints_AddsDistinctEndpoints()
    {
        var svc = new SelectionSetService();
        var e1 = BuildEndpoint(1, "eml:///ch/1", "RPM", "eml:///witsml14/log(x)");
        var e2 = BuildEndpoint(2, "eml:///ch/2", "WOB", "eml:///witsml14/log(x)");

        var added = svc.AddEndpoints([e1, e2]);

        Assert.Equal(2, added);
        Assert.Equal(2, svc.CurrentSelection.Count);
    }

    [Fact]
    public void SelectionSetService_AddEndpoints_DuplicatesAreIgnored()
    {
        var svc = new SelectionSetService();
        var e1 = BuildEndpoint(1, "eml:///ch/1", "RPM", "eml:///witsml14/log(x)");

        svc.AddEndpoints([e1]);
        var addedSecond = svc.AddEndpoints([e1]); // same key

        Assert.Equal(0, addedSecond);
        Assert.Single(svc.CurrentSelection);
    }

    [Fact]
    public void SelectionSetService_AddEndpoints_ReturnsCountOfActuallyAdded()
    {
        var svc = new SelectionSetService();
        var e1 = BuildEndpoint(1, "eml:///ch/1", "A", "eml:///src");
        var e2 = BuildEndpoint(2, "eml:///ch/2", "B", "eml:///src");

        svc.AddEndpoints([e1]);
        var added = svc.AddEndpoints([e1, e2]); // e1 is duplicate

        Assert.Equal(1, added);
        Assert.Equal(2, svc.CurrentSelection.Count);
    }

    [Fact]
    public void SelectionSetService_AddEndpoints_SelectionKeyIncludesSourceUri()
    {
        var svc = new SelectionSetService();
        // Same channel URI but different source URIs — should be separate entries
        var e1 = BuildEndpoint(1, "eml:///ch/1", "RPM", "eml:///witsml14/log(a)");
        var e2 = BuildEndpoint(1, "eml:///ch/1", "RPM", "eml:///witsml14/log(b)");

        svc.AddEndpoints([e1]);
        var added = svc.AddEndpoints([e2]);

        Assert.Equal(1, added);
        Assert.Equal(2, svc.CurrentSelection.Count);
    }

    // ── SelectionSetService: ordering ─────────────────────────────────────────

    [Fact]
    public void SelectionSetService_Selection_RetainsInsertionOrder()
    {
        var svc = new SelectionSetService();
        var fixedTime = DateTimeOffset.UtcNow;

        var e1 = BuildEndpoint(1, "eml:///ch/1", "First", "eml:///src");
        var e2 = BuildEndpoint(2, "eml:///ch/2", "Second", "eml:///src");
        var e3 = BuildEndpoint(3, "eml:///ch/3", "Third", "eml:///src");

        svc.AddEndpoints([e1, e2, e3], fixedTime);

        var names = svc.CurrentSelection.Select(s => s.Endpoint.ChannelName).ToList();
        Assert.Equal(["First", "Second", "Third"], names);
    }

    // ── App-level: resolve then add via Browse ──────────────────────────────────

    [Fact]
    public async Task RunAsync_ResolveAndAddEndpointsInBrowse_PopulatesSelectionSet()
    {
        var client = new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildDiscoveryResult("eml://",
                [FakeExplorerClient.BuildResource("eml:///witsml14", "witsml14")]),
        };
        client.DiscoverResultsByUri["eml:///witsml14"] = FakeExplorerClient.BuildDiscoveryResult(
            "eml:///witsml14",
            [FakeExplorerClient.BuildResource("eml:///witsml14/log(x)", "Log X", "DataObject", channelSubscribable: true, hasChildren: 0)]);
        client.DescribeResultsByUri["eml:///witsml14/log(x)"] = FakeExplorerClient.BuildDescribeResult(
            ["eml:///witsml14/log(x)"],
            [FakeExplorerClient.BuildChannel(1, "eml:///ch/1", "RPM")]);

        var selection = new SelectionSetService();
        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.SelectFocusedResourceForStreaming,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });
        ui.EnqueueBrowseResult(new BrowseWorkspaceResult
        {
            Action = BrowseWorkspaceAction.ReturnToMain,
            FocusedColumnIndex = 0,
            SelectedIndices = [0],
        });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        Assert.Single(selection.CurrentSelection);
        Assert.Equal("RPM", selection.CurrentSelection[0].Endpoint.ChannelName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExplorerOptions ValidOptions() => new()
    {
        EndpointUri = "wss://fake/etp",
        Username = "user",
        Password = "pass",
        ProtocolRequestTimeoutSeconds = 5,
    };

    private static ExplorerApp BuildApp(
        FakeExplorerClient client,
        FakeExplorerUi ui,
        SelectionSetService selectionSet) =>
        new(
            client,
            ui,
            ValidOptions(),
            new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance),
            new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance),
            selectionSet,
            new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance),
            NullLogger<ExplorerApp>.Instance);

    private static ResolvedStreamableEndpoint BuildEndpoint(
        long channelId,
        string channelUri,
        string name,
        string sourceUri) => new()
    {
        SourceResourceUri = sourceUri,
        ChannelId = channelId,
        ChannelUri = channelUri,
        ChannelName = name,
        DataType = "double",
        IndexType = "Time",
        Status = "Active",
    };
}
