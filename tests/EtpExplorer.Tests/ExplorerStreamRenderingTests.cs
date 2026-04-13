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
    public void StreamEventFormatter_MultipleIndexes_JoinsWithComma()
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
        Assert.Contains("100", rendered[0].PrimaryIndexText);
        Assert.Contains("200", rendered[0].PrimaryIndexText);
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
