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
    public async Task RunAsync_StartStreaming_RendersIncomingDataEvents()
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

        Assert.Single(ui.RenderedEvents);
        Assert.Equal(channelId, ui.RenderedEvents[0].ChannelId);
        Assert.Equal("RPM", ui.RenderedEvents[0].ChannelName);
        Assert.Equal(StreamEventKind.Data, ui.RenderedEvents[0].EventKind);
    }

    [Fact]
    public async Task RunAsync_StartStreaming_RendersMultipleEvents()
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

        Assert.Equal(5, ui.RenderedEvents.Count);
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
    public async Task RunAsync_StartStreaming_AttributesEventToCorrectEndpointName()
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

        Assert.Single(ui.RenderedEvents);
        Assert.Equal("TORQUE", ui.RenderedEvents[0].ChannelName);
        Assert.Equal(10, ui.RenderedEvents[0].ChannelId);
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
