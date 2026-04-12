using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Unit tests for <see cref="SampleOutputWriter"/> Protocol 1 channel streaming output methods:
/// channel definition table display, streaming event display, and secret-safe failure output.
/// T011 [US1], T019 [US2].
/// </summary>
public sealed class SampleOutputWriterChannelStreamingTests
{
    // ── T011 [US1]: WriteChannelDescription output ────────────────────────────

    [Fact]
    public void WriteChannelDescription_WithChannels_PrintsChannelTable()
    {
        var out_ = new StringWriter();
        var err = new StringWriter();
        var writer = new SampleOutputWriter(out_, err);

        var outcome = CreateSuccessOutcomeWithChannels(2);
        writer.WriteChannelDescription(outcome);

        var output = out_.ToString();
        Assert.Contains("Channel", output);
    }

    [Fact]
    public void WriteChannelDescription_NullResult_WritesNothing()
    {
        var out_ = new StringWriter();
        var err = new StringWriter();
        var writer = new SampleOutputWriter(out_, err);

        var outcome = CreateSuccessOutcomeWithChannels(0, descriptionResult: null);
        writer.WriteChannelDescription(outcome);

        Assert.Empty(out_.ToString());
    }

    [Fact]
    public void WriteChannelDescription_EmptyChannels_PrintsNoChannelsMessage()
    {
        var out_ = new StringWriter();
        var err = new StringWriter();
        var writer = new SampleOutputWriter(out_, err);

        var outcome = CreateSuccessOutcomeWithChannels(0, descriptionResult: CreateEmptyDescriptionResult());
        writer.WriteChannelDescription(outcome);

        var output = out_.ToString();
        Assert.Contains("channel", output, StringComparison.OrdinalIgnoreCase);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static SampleRunOutcome CreateSuccessOutcomeWithChannels(
        int channelCount,
        ChannelDescriptionResult? descriptionResult = null)
    {
        var connResult = SampleTestData.ConnectionResult();
        if (descriptionResult is null && channelCount > 0)
        {
            var channels = Enumerable.Range(1, channelCount).Select(i => new ChannelDefinition
            {
                ChannelId = i,
                ChannelUri = $"eml://witsml14/well(abc)/log(L1)/channel(CH{i})",
                ChannelName = $"CH{i}",
                DataType = "double",
                Uom = "rpm",
                IndexType = "Time",
                IndexUom = "ms",
                IndexDirection = "Increasing",
                Description = "",
                Status = "Active",
                Source = "test",
                MeasureClass = "",
            }).ToList<ChannelDefinition>();

            descriptionResult = new ChannelDescriptionResult
            {
                RequestedUris = ["eml://witsml14/well(abc)/log(L1)"],
                Channels = channels,
                MessageEncoding = EtpMessageEncoding.Binary,
                WasMultipart = false,
                State = ChannelDescriptionState.Completed,
            };
        }

        return SampleRunOutcome.FromSuccess(connResult, channelDescriptionResult: descriptionResult);
    }

    private static ChannelDescriptionResult CreateEmptyDescriptionResult() =>
        new()
        {
            RequestedUris = ["eml://witsml14/well(abc)/log(L1)"],
            Channels = [],
            MessageEncoding = EtpMessageEncoding.Binary,
            WasMultipart = false,
            State = ChannelDescriptionState.Completed,
        };

    // ── T019 [US2]: WriteLiveStreaming output ─────────────────────────────────

    [Fact]
    public void WriteLiveStreaming_WithResult_PrintsChannelIdsAndEventCount()
    {
        var out_ = new StringWriter();
        var err = new StringWriter();
        var writer = new SampleOutputWriter(out_, err);

        var outcome = CreateSuccessOutcomeWithStreaming(
            subscribedIds: [1L, 2L],
            eventsReceived: 5,
            endedByRemove: true);
        writer.WriteLiveStreaming(outcome);

        var output = out_.ToString();
        Assert.Contains("1", output);
        Assert.Contains("5", output);
    }

    [Fact]
    public void WriteLiveStreaming_NullResult_WritesNothing()
    {
        var out_ = new StringWriter();
        var err = new StringWriter();
        var writer = new SampleOutputWriter(out_, err);

        var connResult = SampleTestData.ConnectionResult();
        var outcome = SampleRunOutcome.FromSuccess(connResult);
        writer.WriteLiveStreaming(outcome);

        Assert.Empty(out_.ToString());
    }

    [Fact]
    public void WriteLiveData_WithChannelItems_PrintsIndexNameAndValue()
    {
        var out_ = new StringWriter();
        var err = new StringWriter();
        var writer = new SampleOutputWriter(out_, err);
        var channelsById = new Dictionary<long, ChannelDefinition>
        {
            [7L] = new()
            {
                ChannelId = 7L,
                ChannelUri = "eml://witsml14/well(abc)/log(L1)/channel(RPM)",
                ChannelName = "RPM",
                DataType = "double",
                Uom = "rpm",
                IndexType = "Time",
                IndexUom = "ms",
                IndexDirection = "Increasing",
                Description = string.Empty,
                Status = "Active",
                Source = "test",
                MeasureClass = string.Empty,
            },
        };

        writer.WriteLiveData(
            [new ChannelDataItem { Indexes = [12345L], ChannelId = 7L, Value = 98.6 }],
            channelsById);

        var output = out_.ToString();
        // After index formatting, raw 12345 is rendered as a timestamp, not the plain long
        Assert.DoesNotContain("12345  RPM", output);
        Assert.Contains("RPM", output);
        Assert.Contains("98.6", output);
    }

    private static SampleRunOutcome CreateSuccessOutcomeWithStreaming(
        IReadOnlyList<long> subscribedIds, int eventsReceived, bool endedByRemove)
    {
        var connResult = SampleTestData.ConnectionResult();
        var streamingResult = new LiveStreamingResult
        {
            SubscribedChannelIds = subscribedIds,
            EventsReceived = eventsReceived,
            EndedByRemove = endedByRemove,
        };
        return SampleRunOutcome.FromSuccess(connResult, liveStreamingResult: streamingResult);
    }
}
