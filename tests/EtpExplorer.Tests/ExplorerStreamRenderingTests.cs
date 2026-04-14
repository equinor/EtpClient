using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for rendered output attribution: multi-endpoint streams, value formatting,
/// index formatting, and non-data event rendering.
/// </summary>
public sealed class ExplorerStreamRenderingTests
{
    // ── StreamEventFormatter: data item formatting ────────────────────────────

    [Fact]
    public void StreamEventFormatter_FormatDataItem_ProducesCorrectChannelName()
    {
        var selection = BuildSelection(
            (1, "eml:///ch/1", "RPM"),
            (2, "eml:///ch/2", "WOB"));

        var formatter = new StreamEventFormatter(selection);
        var evt = BuildDataEvent(1, 1000L, 42.5);

        var rendered = formatter.Format(evt);

        Assert.Single(rendered);
        Assert.Equal("RPM", rendered[0].ChannelName);
        Assert.Equal(1, rendered[0].ChannelId);
    }

    [Fact]
    public void StreamEventFormatter_FormatDataItem_UnknownChannel_UsesChannelIdFallback()
    {
        var selection = BuildSelection(); // empty
        var formatter = new StreamEventFormatter(selection);
        var evt = BuildDataEvent(99, 1000L, 3.14);

        var rendered = formatter.Format(evt);

        Assert.Single(rendered);
        Assert.Equal("channel:99", rendered[0].ChannelName);
    }

    // ── Value formatting ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(42.5, "42.5")]
    [InlineData(0.0, "0")]
    [InlineData(-1.25, "-1.25")]
    public void StreamEventFormatter_FormatDouble_ProducesExpectedText(double value, string expected)
    {
        var selection = BuildSelection((1, "eml:///ch/1", "V"));
        var formatter = new StreamEventFormatter(selection);
        var evt = BuildDataEvent(1, 0L, value);

        var rendered = formatter.Format(evt);

        Assert.StartsWith(expected, rendered[0].ValueText);
    }

    [Fact]
    public void StreamEventFormatter_FormatNull_ProducesNullText()
    {
        var selection = BuildSelection((1, "eml:///ch/1", "V"));
        var formatter = new StreamEventFormatter(selection);
        var evt = new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.Data,
            DataItems = [new ChannelDataItem { ChannelId = 1, Indexes = [0L], Value = null }],
        };

        var rendered = formatter.Format(evt);
        Assert.Equal("null", rendered[0].ValueText);
    }

    // ── Index formatting ──────────────────────────────────────────────────────

    [Fact]
    public void StreamEventFormatter_SingleIndex_ProducesIndexString()
    {
        var selection = BuildSelection((1, "eml:///ch/1", "V"));
        var formatter = new StreamEventFormatter(selection);
        var evt = BuildDataEvent(1, 1234567890L, 1.0);

        var rendered = formatter.Format(evt);
        Assert.Equal("1234567890", rendered[0].PrimaryIndexText);
    }

    [Fact]
    public void StreamEventFormatter_MultipleIndexes_UsesFirstAsPrimaryIndex()
    {
        var selection = BuildSelection((1, "eml:///ch/1", "V"));
        var formatter = new StreamEventFormatter(selection);
        var evt = new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.Data,
            DataItems =
            [
                new ChannelDataItem { ChannelId = 1, Indexes = [100L, 200L], Value = 5.0 },
            ],
        };

        var rendered = formatter.Format(evt);
        // Only the first (primary) index is displayed
        Assert.Equal("100", rendered[0].PrimaryIndexText);
    }

    // ── Non-data event kinds ──────────────────────────────────────────────────

    [Fact]
    public void StreamEventFormatter_StatusChange_ProducesStatusChangeKind()
    {
        var selection = BuildSelection((1, "eml:///ch/1", "V"));
        var formatter = new StreamEventFormatter(selection);
        var evt = new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.StatusChange,
            ChannelId = 1,
            NewStatus = "Inactive",
        };

        var rendered = formatter.Format(evt);

        Assert.Single(rendered);
        Assert.Equal(StreamEventKind.StatusChange, rendered[0].EventKind);
        Assert.Contains("Inactive", rendered[0].ValueText);
    }

    [Fact]
    public void StreamEventFormatter_Remove_ProducesRemoveKind()
    {
        var selection = BuildSelection((1, "eml:///ch/1", "V"));
        var formatter = new StreamEventFormatter(selection);
        var evt = new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.Remove,
            ChannelId = 1,
            RemoveReason = "channel closed",
        };

        var rendered = formatter.Format(evt);

        Assert.Single(rendered);
        Assert.Equal(StreamEventKind.Remove, rendered[0].EventKind);
        Assert.Contains("remove", rendered[0].ValueText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StreamEventFormatter_DataChange_ProducesDataChangeKind()
    {
        var selection = BuildSelection((1, "eml:///ch/1", "V"));
        var formatter = new StreamEventFormatter(selection);
        var evt = new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.DataChange,
            ChannelId = 1,
            StartIndex = 100,
            EndIndex = 200,
        };

        var rendered = formatter.Format(evt);

        Assert.Single(rendered);
        Assert.Equal(StreamEventKind.DataChange, rendered[0].EventKind);
    }

    // ── Multi-endpoint attribution ─────────────────────────────────────────────

    [Fact]
    public void StreamEventFormatter_MultipleItems_EachAttributedToCorrectEndpoint()
    {
        var selection = BuildSelection(
            (10, "eml:///ch/10", "ChannelTen"),
            (20, "eml:///ch/20", "ChannelTwenty"));

        var formatter = new StreamEventFormatter(selection);
        var evt = new EtpClient.Models.ChannelEvent
        {
            Kind = ChannelEventKind.Data,
            DataItems =
            [
                new ChannelDataItem { ChannelId = 10, Indexes = [1L], Value = 1.0 },
                new ChannelDataItem { ChannelId = 20, Indexes = [1L], Value = 2.0 },
            ],
        };

        var rendered = formatter.Format(evt);

        Assert.Equal(2, rendered.Count);
        Assert.Equal("ChannelTen", rendered[0].ChannelName);
        Assert.Equal("ChannelTwenty", rendered[1].ChannelName);
    }

    // ── SourceResourceUri preservation ───────────────────────────────────────

    [Fact]
    public void StreamEventFormatter_FormatDataItem_PreservesSourceResourceUri()
    {
        const string sourceUri = "eml:///witsml14/log(x)";
        var selection = new List<SelectedEndpoint>
        {
            new()
            {
                SelectionKey = SelectedEndpoint.BuildKey(sourceUri, "eml:///ch/1"),
                Endpoint = new ResolvedStreamableEndpoint
                {
                    SourceResourceUri = sourceUri,
                    ChannelId = 1,
                    ChannelUri = "eml:///ch/1",
                    ChannelName = "RPM",
                    DataType = "double",
                    IndexType = "Time",
                    Status = "Active",
                },
                SelectedAtUtc = DateTimeOffset.UtcNow,
            },
        };

        var formatter = new StreamEventFormatter(selection);
        var evt = BuildDataEvent(1, 0L, 5.0);

        var rendered = formatter.Format(evt);

        Assert.Equal(sourceUri, rendered[0].SourceResourceUri);
    }

    // ── US1: Fixed-row initialization and alphabetical ordering ──────────────

    [Fact]
    public void BuildInitialSnapshot_EmptySelection_ProducesNoRows()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var snap = svc.BuildInitialSnapshot([]);
        Assert.Empty(snap.Rows);
        Assert.True(snap.IsActive);
    }

    [Fact]
    public void BuildInitialSnapshot_CreatesOneRowPerSelectedEndpoint()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection(
            (1, "eml:///ch/1", "RPM"),
            (2, "eml:///ch/2", "WOB"),
            (3, "eml:///ch/3", "TORQUE"));

        var snap = svc.BuildInitialSnapshot(selection);

        Assert.Equal(3, snap.Rows.Count);
    }

    [Fact]
    public void BuildInitialSnapshot_RowsAreSortedAlphabetically()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection(
            (1, "eml:///ch/1", "WOB"),
            (2, "eml:///ch/2", "RPM"),
            (3, "eml:///ch/3", "TORQUE"));

        var snap = svc.BuildInitialSnapshot(selection);

        Assert.Equal("RPM", snap.Rows[0].ChannelName);
        Assert.Equal("TORQUE", snap.Rows[1].ChannelName);
        Assert.Equal("WOB", snap.Rows[2].ChannelName);
    }

    [Fact]
    public void BuildInitialSnapshot_EachRowStartsInWaitingState()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection(
            (1, "eml:///ch/1", "RPM"),
            (2, "eml:///ch/2", "WOB"));

        var snap = svc.BuildInitialSnapshot(selection);

        Assert.All(snap.Rows, row =>
        {
            Assert.Equal(RowStatusField.Waiting, row.RowStatus);
            Assert.Equal("Waiting for data", row.StatusText);
            Assert.Empty(row.PrimaryIndexText);
            Assert.Empty(row.ValueText);
        });
    }

    // ── US2: In-place row updates and waiting-to-live transition ─────────────

    [Fact]
    public void ApplyEvent_DataEvent_UpdatesRowIndexAndValue()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection((1, "eml:///ch/1", "RPM"));
        var snap = svc.BuildInitialSnapshot(selection);

        var update = new RenderedStreamEvent
        {
            ChannelId = 1,
            ChannelName = "RPM",
            SourceResourceUri = "eml:///src",
            PrimaryIndexText = "1000",
            ValueText = "42.5",
            EventKind = StreamEventKind.Data,
            ObservedAtUtc = DateTimeOffset.UtcNow,
        };

        svc.ApplyEvent(snap, update);

        var row = snap.Rows[0];
        Assert.Equal("1000", row.PrimaryIndexText);
        Assert.Equal("42.5", row.ValueText);
        Assert.Equal(RowStatusField.Live, row.RowStatus);
    }

    [Fact]
    public void ApplyEvent_DataEvent_TransitionsStatusFromWaitingToLive()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection((1, "eml:///ch/1", "RPM"));
        var snap = svc.BuildInitialSnapshot(selection);

        Assert.Equal(RowStatusField.Waiting, snap.Rows[0].RowStatus);

        var update = new RenderedStreamEvent
        {
            ChannelId = 1,
            ChannelName = "RPM",
            SourceResourceUri = "eml:///src",
            PrimaryIndexText = "1",
            ValueText = "1.0",
            EventKind = StreamEventKind.Data,
            ObservedAtUtc = DateTimeOffset.UtcNow,
        };

        svc.ApplyEvent(snap, update);

        Assert.Equal(RowStatusField.Live, snap.Rows[0].RowStatus);
    }

    [Fact]
    public void ApplyEvent_RepeatedDataEvents_UpdateSameRowInPlace()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection((1, "eml:///ch/1", "RPM"));
        var snap = svc.BuildInitialSnapshot(selection);

        for (var i = 1; i <= 5; i++)
        {
            svc.ApplyEvent(snap, new RenderedStreamEvent
            {
                ChannelId = 1,
                ChannelName = "RPM",
                SourceResourceUri = "eml:///src",
                PrimaryIndexText = i.ToString(),
                ValueText = i.ToString(),
                EventKind = StreamEventKind.Data,
                ObservedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        Assert.Single(snap.Rows);
        Assert.Equal("5", snap.Rows[0].PrimaryIndexText);
        Assert.Equal("5", snap.Rows[0].ValueText);
    }

    [Fact]
    public void ApplyEvent_UnknownChannelId_DoesNotCreateExtraRow()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection((1, "eml:///ch/1", "RPM"));
        var snap = svc.BuildInitialSnapshot(selection);

        svc.ApplyEvent(snap, new RenderedStreamEvent
        {
            ChannelId = 999,
            ChannelName = "Unknown",
            SourceResourceUri = "eml:///src",
            PrimaryIndexText = "1",
            ValueText = "1",
            EventKind = StreamEventKind.Data,
            ObservedAtUtc = DateTimeOffset.UtcNow,
        });

        Assert.Single(snap.Rows);
    }

    // ── US3: Lifecycle status field and remove-row persistence ───────────────

    [Fact]
    public void ApplyEvent_StatusChange_UpdatesStatusFieldOnly()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection((1, "eml:///ch/1", "RPM"));
        var snap = svc.BuildInitialSnapshot(selection);
        snap.Rows[0].PrimaryIndexText = "50";
        snap.Rows[0].ValueText = "12.5";

        svc.ApplyEvent(snap, new RenderedStreamEvent
        {
            ChannelId = 1,
            ChannelName = "RPM",
            SourceResourceUri = "eml:///src",
            PrimaryIndexText = string.Empty,
            ValueText = "status: Inactive",
            EventKind = StreamEventKind.StatusChange,
            ObservedAtUtc = DateTimeOffset.UtcNow,
        });

        var row = snap.Rows[0];
        Assert.Equal(RowStatusField.StatusChanged, row.RowStatus);
        Assert.Equal("status: Inactive", row.StatusText);
        // Index and value are preserved
        Assert.Equal("50", row.PrimaryIndexText);
        Assert.Equal("12.5", row.ValueText);
    }

    [Fact]
    public void ApplyEvent_RemoveEvent_MarksRowEndedWithoutDeletingIt()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection((1, "eml:///ch/1", "RPM"));
        var snap = svc.BuildInitialSnapshot(selection);

        svc.ApplyEvent(snap, new RenderedStreamEvent
        {
            ChannelId = 1,
            ChannelName = "RPM",
            SourceResourceUri = "eml:///src",
            PrimaryIndexText = string.Empty,
            ValueText = "(removed)",
            EventKind = StreamEventKind.Remove,
            ObservedAtUtc = DateTimeOffset.UtcNow,
        });

        Assert.Single(snap.Rows);
        Assert.Equal(RowStatusField.Ended, snap.Rows[0].RowStatus);
        Assert.Equal("Ended", snap.Rows[0].StatusText);
    }

    [Fact]
    public void ApplyEvent_DataChange_MarkRowChangedWithoutTouchingIndexOrValue()
    {
        var svc = new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance);
        var selection = BuildSelection((1, "eml:///ch/1", "RPM"));
        var snap = svc.BuildInitialSnapshot(selection);
        snap.Rows[0].PrimaryIndexText = "100";
        snap.Rows[0].ValueText = "7.7";

        svc.ApplyEvent(snap, new RenderedStreamEvent
        {
            ChannelId = 1,
            ChannelName = "RPM",
            SourceResourceUri = "eml:///src",
            PrimaryIndexText = "100–200",
            ValueText = "(changed)",
            EventKind = StreamEventKind.DataChange,
            ObservedAtUtc = DateTimeOffset.UtcNow,
        });

        var row = snap.Rows[0];
        Assert.Equal(RowStatusField.Changed, row.RowStatus);
        // PrimaryIndexText and ValueText are NOT replaced by DataChange
        Assert.Equal("100", row.PrimaryIndexText);
        Assert.Equal("7.7", row.ValueText);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<SelectedEndpoint> BuildSelection(
        params (long channelId, string channelUri, string name)[] channels)
    {
        return channels.Select(c => new SelectedEndpoint
        {
            SelectionKey = SelectedEndpoint.BuildKey("eml:///src", c.channelUri),
            Endpoint = new ResolvedStreamableEndpoint
            {
                SourceResourceUri = "eml:///src",
                ChannelId = c.channelId,
                ChannelUri = c.channelUri,
                ChannelName = c.name,
                DataType = "double",
                IndexType = "Time",
                Status = "Active",
            },
            SelectedAtUtc = DateTimeOffset.UtcNow,
        }).ToList();
    }

    private static EtpClient.Models.ChannelEvent BuildDataEvent(long channelId, long index, double value) =>
        new()
        {
            Kind = ChannelEventKind.Data,
            DataItems = [new ChannelDataItem { ChannelId = channelId, Indexes = [index], Value = value }],
        };
}
