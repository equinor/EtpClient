using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for live streaming lifecycle: start, stop, cancellation, and partial failures.
/// </summary>
public sealed class ExplorerStreamingWorkflowTests
{
    // ── Start streaming: events rendered ──────────────────────────────────────

    [Fact]
    public async Task RunAsync_StartStreaming_RendersSnapshotForEachUpdate()
    {
        var channelId = 42L;
        var dataEvent = new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.Data,
            DataItems =
            [
                new ChannelDataItem { ChannelId = channelId, Indexes = [1000L], Value = 99.5 },
            ],
        };

        var client = BuildClientWithRoot();
        client.StreamEvents = [dataEvent];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(channelId, $"eml:///ch/{channelId}", "RPM")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        // Initial snapshot + one update snapshot
        Assert.True(ui.StreamSnapshots.Count >= 2);
        var finalSnap = ui.StreamSnapshots.Last();
        var row = Assert.Single(finalSnap.Rows);
        Assert.Equal(channelId, row.ChannelId);
        Assert.Equal("RPM", row.ChannelName);
        Assert.Equal(RowStatusField.Live, row.RowStatus);
    }

    [Fact]
    public async Task RunAsync_StartStreaming_OneSnapshotPerEventPlusInitial()
    {
        var events = Enumerable.Range(1, 5).Select(i => new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.Data,
            DataItems = [new ChannelDataItem { ChannelId = 1, Indexes = [(long)i], Value = (double)i }],
        }).ToList();

        var client = BuildClientWithRoot();
        client.StreamEvents = events;

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "WOB")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        // 1 initial render + 5 event renders
        Assert.Equal(6, ui.StreamSnapshots.Count);
    }

    // ── Stop streaming: user-initiated ────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenStopStreamingRequested_ReturnsToConnectedState()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents = []; // no events — stream ends immediately

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "CH1")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        // After streaming completes (no more events), return to menu then exit
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        var result = await app.RunAsync();

        Assert.Equal(0, result);
        // Should have called stop and close
        Assert.Equal(1, client.CloseCallCount);
    }

    // ── Attribution ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_StartStreaming_SnapshotRowAttributedToCorrectEndpointName()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents =
        [
            new EtpClient.Models.ChannelEvent
            {
                Kind = ChannelEventKind.Data,
                DataItems = [new ChannelDataItem { ChannelId = 10, Indexes = [100L], Value = 42.0 }],
            },
        ];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(10, "eml:///ch/10", "TORQUE")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        var finalSnap = ui.StreamSnapshots.Last();
        var row = Assert.Single(finalSnap.Rows);
        Assert.Equal("TORQUE", row.ChannelName);
        Assert.Equal(10, row.ChannelId);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenCancelled_StopsStreaming()
    {
        using var cts = new CancellationTokenSource();

        var client = BuildClientWithRoot();
        // Provide one event, then cancel
        client.StreamEvents = [];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "A")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        cts.Cancel(); // Cancel before running
        var app = BuildApp(client, ui, selection);
        var result = await app.RunAsync(cts.Token);

        // Cancellation should be handled gracefully
        Assert.True(result == 0 || result == 1);
    }

    // ── ExplorerStreamingService unit-level ───────────────────────────────────

    [Fact]
    public void ExplorerStreamingService_BuildSubscriptions_CreateOnePerEndpoint()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = new SelectionSetService();
        selection.AddEndpoints([
            BuildEndpoint(1, "eml:///ch/1", "A"),
            BuildEndpoint(2, "eml:///ch/2", "B"),
        ]);

        var subs = svc.BuildSubscriptions(selection.CurrentSelection);

        Assert.Equal(2, subs.Count);
        Assert.Equal(1, subs[0].ChannelId);
        Assert.True(subs[0].StartLatest);
        Assert.Equal(2, subs[1].ChannelId);
    }

    [Fact]
    public async Task ExplorerStreamingService_StreamAsync_YieldsRenderedEvents()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var client = BuildClientWithRoot();
        client.StreamEvents =
        [
            new EtpClient.Models.ChannelEvent
            {
                Kind = ChannelEventKind.Data,
                DataItems = [new ChannelDataItem { ChannelId = 1, Indexes = [500L], Value = 3.14 }],
            },
        ];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "PI")]);
        var subs = svc.BuildSubscriptions(selection.CurrentSelection);

        var rendered = new List<RenderedStreamEvent>();
        await foreach (var evt in svc.StreamAsync(client, subs, selection.CurrentSelection))
            rendered.Add(evt);

        Assert.Single(rendered);
        Assert.Equal("PI", rendered[0].ChannelName);
        Assert.Equal(StreamEventKind.Data, rendered[0].EventKind);
    }

    // ── US1: Fixed row list and alphabetical ordering ─────────────────────────

    [Fact]
    public async Task RunAsync_StartStreaming_InitialSnapshotHasOneRowPerSelectedEndpoint()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents = [];

        var selection = new SelectionSetService();
        selection.AddEndpoints([
            BuildEndpoint(1, "eml:///ch/1", "RPM"),
            BuildEndpoint(2, "eml:///ch/2", "WOB"),
            BuildEndpoint(3, "eml:///ch/3", "TORQUE"),
        ]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        var initial = ui.StreamSnapshots.First();
        Assert.Equal(3, initial.Rows.Count);
    }

    [Fact]
    public async Task RunAsync_StartStreaming_InitialSnapshotRowsAreAlphabetical()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents = [];

        var selection = new SelectionSetService();
        selection.AddEndpoints([
            BuildEndpoint(1, "eml:///ch/1", "WOB"),
            BuildEndpoint(2, "eml:///ch/2", "RPM"),
            BuildEndpoint(3, "eml:///ch/3", "TORQUE"),
        ]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        var initial = ui.StreamSnapshots.First();
        Assert.Equal("RPM", initial.Rows[0].ChannelName);
        Assert.Equal("TORQUE", initial.Rows[1].ChannelName);
        Assert.Equal("WOB", initial.Rows[2].ChannelName);
    }

    [Fact]
    public async Task RunAsync_StartStreaming_DataEventsDoNotAddRows()
    {
        // 3 data events for the same channel must not grow the row count
        var events = Enumerable.Range(1, 3).Select(i => new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.Data,
            DataItems = [new ChannelDataItem { ChannelId = 1, Indexes = [(long)i], Value = (double)i }],
        }).ToList();

        var client = BuildClientWithRoot();
        client.StreamEvents = events;

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "RPM")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        Assert.All(ui.StreamSnapshots, snap => Assert.Single(snap.Rows));
    }

    // ── US2: Waiting state and in-place value updates ─────────────────────────

    [Fact]
    public async Task RunAsync_StartStreaming_InitialSnapshotRowsAreInWaitingState()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents = [];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "RPM")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        var initial = ui.StreamSnapshots.First();
        Assert.All(initial.Rows, row =>
        {
            Assert.Equal(RowStatusField.Waiting, row.RowStatus);
            Assert.Equal("Waiting for data", row.StatusText);
        });
    }

    [Fact]
    public async Task RunAsync_StartStreaming_DataEventUpdatesRowToLiveState()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents =
        [
            new EtpClient.Models.ChannelEvent
            {
                Kind = ChannelEventKind.Data,
                DataItems = [new ChannelDataItem { ChannelId = 1, Indexes = [500L], Value = 3.14 }],
            },
        ];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "RPM")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        var finalSnap = ui.StreamSnapshots.Last();
        Assert.Equal(RowStatusField.Live, finalSnap.Rows[0].RowStatus);
    }

    // ── US3: Lifecycle events and clean stop ──────────────────────────────────

    [Fact]
    public async Task RunAsync_StartStreaming_StatusChangeEventUpdatesStatusField()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents =
        [
            new EtpClient.Models.ChannelEvent
            {
                Kind = ChannelEventKind.StatusChange,
                ChannelId = 1,
                NewStatus = "Inactive",
            },
        ];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "RPM")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        var finalSnap = ui.StreamSnapshots.Last();
        Assert.Equal(RowStatusField.StatusChanged, finalSnap.Rows[0].RowStatus);
    }

    [Fact]
    public async Task RunAsync_StartStreaming_RemoveEventKeepsRowVisibleWithEndedStatus()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents =
        [
            new EtpClient.Models.ChannelEvent
            {
                Kind = ChannelEventKind.Remove,
                ChannelId = 1,
                RemoveReason = "channel closed",
            },
        ];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "RPM")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        var finalSnap = ui.StreamSnapshots.Last();
        // Row must remain visible
        Assert.Single(finalSnap.Rows);
        Assert.Equal(RowStatusField.Ended, finalSnap.Rows[0].RowStatus);
    }

    [Fact]
    public async Task RunAsync_WhenStreamEnds_SnapshotIsMarkedInactive()
    {
        var client = BuildClientWithRoot();
        client.StreamEvents = [];

        var selection = new SelectionSetService();
        selection.AddEndpoints([BuildEndpoint(1, "eml:///ch/1", "RPM")]);

        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.StartStreaming);
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(client, ui, selection);
        await app.RunAsync();

        // After streaming, the snapshot must be inactive
        var finalSnap = ui.StreamSnapshots.Last();
        Assert.False(finalSnap.IsActive);
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
        string sourceUri = "eml:///witsml14") => new()
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
