using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for the selection review screen: display, remove-one, clear-all, and duplicate prevention.
/// </summary>
public sealed class ExplorerSelectionReviewTests
{
    // ── Review action: Done ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReviewSelection_WithDoneAction_RetainsAllEndpoints()
    {
        var selection = new SelectionSetService();
        var e1 = BuildEndpoint(1, "eml:///ch/1", "RPM", "eml:///src");
        var e2 = BuildEndpoint(2, "eml:///ch/2", "WOB", "eml:///src");
        selection.AddEndpoints([e1, e2]);

        var client = BuildClientWithRoot();
        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.ReviewSelection);
        // Done → return
        ui.EnqueueSelectionReview(SelectionReviewAction.Done);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        Assert.Equal(2, selection.CurrentSelection.Count);
    }

    // ── Review action: Remove one ─────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReviewSelection_RemoveOne_RemovesTargetEndpoint()
    {
        var selection = new SelectionSetService();
        var e1 = BuildEndpoint(1, "eml:///ch/1", "RPM", "eml:///src");
        var e2 = BuildEndpoint(2, "eml:///ch/2", "WOB", "eml:///src");
        selection.AddEndpoints([e1, e2]);

        var toRemove = selection.CurrentSelection[0]; // RPM
        var client = BuildClientWithRoot();
        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.ReviewSelection);
        ui.EnqueueSelectionReview(SelectionReviewAction.RemoveOne);
        ui.EnqueueRemoveEndpoint(toRemove);
        ui.EnqueueSelectionReview(SelectionReviewAction.Done);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        Assert.Single(selection.CurrentSelection);
        Assert.DoesNotContain(selection.CurrentSelection, s => s.Endpoint.ChannelName == "RPM");
    }

    // ── Review action: Clear all ──────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReviewSelection_ClearAll_RemovesAllEndpoints()
    {
        var selection = new SelectionSetService();
        selection.AddEndpoints([
            BuildEndpoint(1, "eml:///ch/1", "A", "eml:///src"),
            BuildEndpoint(2, "eml:///ch/2", "B", "eml:///src"),
            BuildEndpoint(3, "eml:///ch/3", "C", "eml:///src"),
        ]);

        var client = BuildClientWithRoot();
        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.ReviewSelection);
        ui.EnqueueSelectionReview(SelectionReviewAction.ClearAll);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        Assert.Empty(selection.CurrentSelection);
    }

    // ── SelectionSetService unit-level ────────────────────────────────────────

    [Fact]
    public void SelectionSetService_RemoveEndpoint_WithKnownKey_ReturnsTrueAndRemoves()
    {
        var svc = new SelectionSetService();
        var endpoint = BuildEndpoint(1, "eml:///ch/1", "RPM", "eml:///src");
        svc.AddEndpoints([endpoint]);

        var key = svc.CurrentSelection[0].SelectionKey;
        var removed = svc.RemoveEndpoint(key);

        Assert.True(removed);
        Assert.Empty(svc.CurrentSelection);
    }

    [Fact]
    public void SelectionSetService_RemoveEndpoint_WithUnknownKey_ReturnsFalse()
    {
        var svc = new SelectionSetService();
        var removed = svc.RemoveEndpoint("non-existent-key");
        Assert.False(removed);
    }

    [Fact]
    public void SelectionSetService_ClearAll_EmptiesSelection()
    {
        var svc = new SelectionSetService();
        svc.AddEndpoints([
            BuildEndpoint(1, "eml:///ch/1", "A", "eml:///src"),
            BuildEndpoint(2, "eml:///ch/2", "B", "eml:///src"),
        ]);

        svc.ClearAll();

        Assert.Empty(svc.CurrentSelection);
    }

    // ── Stream blocked when selection empty ───────────────────────────────────

    [Fact]
    public async Task RunAsync_StartStreamingWithEmptySelection_ShowsStatusAndDoesNotStream()
    {
        var client = BuildClientWithRoot();
        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var selection = new SelectionSetService(); // empty
        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        // Should not have called StartChannelStreaming
        Assert.Equal(0, client.StartStreamingCallCount);
        // Should have shown a status message about empty selection
        Assert.Contains(ui.StatusMessages, m => m.Contains("endpoint") || m.Contains("selection"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExplorerOptions ValidOptions() => new()
    {
        EndpointUri = "wss://fake/etp",
        Username = "user",
        Password = "pass",
        ProtocolRequestTimeoutSeconds = 5,
    };

    private static FakeExplorerClient BuildClientWithRoot() => new()
    {
        DiscoverResult = FakeExplorerClient.BuildDiscoveryResult("eml://",
            [FakeExplorerClient.BuildResource("eml:///witsml14", "witsml14")]),
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
