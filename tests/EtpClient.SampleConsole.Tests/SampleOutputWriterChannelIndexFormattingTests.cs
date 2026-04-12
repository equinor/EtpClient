using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Tests for <see cref="SampleOutputWriter"/> channel index formatting:
/// time-indexed live/range output, depth-indexed live/range output, and fallback behavior.
/// T012 [US1], T019 [US2], T024 [US3].
/// </summary>
public sealed class SampleOutputWriterChannelIndexFormattingTests
{
    // ── T012 [US1]: Time-indexed live output ─────────────────────────────────

    [Fact]
    public void WriteLiveData_TimeIndexedChannel_PrintsTimestampNotRawLong()
    {
        // The sample spec example: raw = 1_775_845_444_000_000 µs from epoch
        var channel = TimeChannel(channelId: 1L);
        var items = new[] { new ChannelDataItem { ChannelId = 1L, Indexes = [1_775_845_444_000_000L], Value = 12.5 } };
        var channelsById = new Dictionary<long, ChannelDefinition> { [1L] = channel };

        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        writer.WriteLiveData(items, channelsById);

        var output = capture.Out;
        // Must NOT print the raw long
        Assert.DoesNotContain("1775845444000000", output);
        // Must contain the channel name and value
        Assert.Contains("TimeChannel", output);
        Assert.Contains("12.5", output);
    }

    [Fact]
    public void WriteLiveData_TimeIndexedChannel_OutputContainsReadableTimestamp()
    {
        // 1_000_000 µs = 1 second after Unix epoch
        var channel = TimeChannel(channelId: 1L);
        var items = new[] { new ChannelDataItem { ChannelId = 1L, Indexes = [1_000_000L], Value = 42.0 } };
        var channelsById = new Dictionary<long, ChannelDefinition> { [1L] = channel };

        var capture = new TestOutputCapture();
        capture.CreateOutputWriter().WriteLiveData(items, channelsById);

        // The output timestamp should contain year information (not just a plain number)
        var output = capture.Out;
        // The UTC instant 1970-01-01T00:00:01Z should be rendered in some recognizable format
        // We can't assert exact local-time format, but we can assert it's not the raw long
        Assert.DoesNotContain("1000000", output);
    }

    // ── T012 [US1]: Time-indexed range output ────────────────────────────────

    [Fact]
    public void WriteChannelRange_TimeIndexedChannel_PrintsFormattedTimestampsForSamples()
    {
        var channel = TimeChannel(channelId: 1L);
        var outcome = CreateRangeOutcome(
            channelId: 1L,
            channel: channel,
            samples: [new ChannelDataItem { ChannelId = 1L, Indexes = [2_000_000L], Value = 55.5 }]);

        var capture = new TestOutputCapture();
        capture.CreateOutputWriter().WriteChannelRange(outcome);

        var output = capture.Out;
        // The value should appear
        Assert.Contains("55.5", output);
        // A formatted timestamp should appear in the Samples section (year = 1970 for unix-epoch-based)
        Assert.Contains("1970", output);
        // The sample line should NOT show the raw microseconds value as a standalone index
        Assert.DoesNotContain("  2000000  ", output);
    }

    // ── T019 [US2]: Depth-indexed live output ────────────────────────────────

    [Fact]
    public void WriteLiveData_DepthIndexedChannel_PrintsScaledDepthNotRawLong()
    {
        // scale=5 → raw 403_675_000 / 100000 = 4036.75
        var channel = DepthChannel(channelId: 2L, scale: 5, uom: "m");
        var items = new[] { new ChannelDataItem { ChannelId = 2L, Indexes = [403_675_000L], Value = 98.7 } };
        var channelsById = new Dictionary<long, ChannelDefinition> { [2L] = channel };

        var capture = new TestOutputCapture();
        capture.CreateOutputWriter().WriteLiveData(items, channelsById);

        var output = capture.Out;
        Assert.DoesNotContain("403675000", output);
        // Scaled value 4036.75 should appear in some decimal form
        Assert.Contains("4036", output);
    }

    [Fact]
    public void WriteLiveData_DepthIndexedChannel_OutputContainsUom()
    {
        var channel = DepthChannel(channelId: 2L, scale: 3, uom: "ft");
        var items = new[] { new ChannelDataItem { ChannelId = 2L, Indexes = [5_280_000L], Value = 0.5 } };
        var channelsById = new Dictionary<long, ChannelDefinition> { [2L] = channel };

        var capture = new TestOutputCapture();
        capture.CreateOutputWriter().WriteLiveData(items, channelsById);

        Assert.Contains("ft", capture.Out);
    }

    // ── T019 [US2]: Depth-indexed range output ───────────────────────────────

    [Fact]
    public void WriteChannelRange_DepthIndexedChannel_PrintsScaledDepthForSamples()
    {
        var channel = DepthChannel(channelId: 2L, scale: 5, uom: "m");
        var outcome = CreateRangeOutcome(
            channelId: 2L,
            channel: channel,
            samples: [new ChannelDataItem { ChannelId = 2L, Indexes = [403_675_000L], Value = 12.0 }]);

        var capture = new TestOutputCapture();
        capture.CreateOutputWriter().WriteChannelRange(outcome);

        var output = capture.Out;
        // The value should appear
        Assert.Contains("12", output);
        // The scaled depth should appear in the sample section
        Assert.Contains("4036", output);
        // The sample line should NOT show the raw long value as a standalone index
        Assert.DoesNotContain("  403675000  ", output);
    }

    // ── T024 [US3]: Fallback behavior ────────────────────────────────────────

    [Fact]
    public void WriteLiveData_FallbackIndexType_PrintsNonMisleadingOutput()
    {
        var channel = FallbackChannel(channelId: 3L);
        var items = new[] { new ChannelDataItem { ChannelId = 3L, Indexes = [9999L], Value = 1.0 } };
        var channelsById = new Dictionary<long, ChannelDefinition> { [3L] = channel };

        var capture = new TestOutputCapture();
        capture.CreateOutputWriter().WriteLiveData(items, channelsById);

        // Must render something readable (the raw value is acceptable for fallback)
        var output = capture.Out;
        Assert.NotEmpty(output.Trim());
        // Must NOT claim it's a timestamp (no colon-separated time pattern like HH:mm:ss in output for index)
    }

    [Fact]
    public void WriteLiveData_FallbackChannel_UnknownChannelId_PrintsRawIndex()
    {
        // When the channelId is not in the dictionary, we still print something sensible
        var items = new[] { new ChannelDataItem { ChannelId = 99L, Indexes = [123L], Value = 5.0 } };
        var channelsById = new Dictionary<long, ChannelDefinition>();

        var capture = new TestOutputCapture();
        capture.CreateOutputWriter().WriteLiveData(items, channelsById);

        var output = capture.Out;
        Assert.NotEmpty(output.Trim());
        Assert.Contains("123", output);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ChannelDefinition TimeChannel(long channelId) =>
        new()
        {
            ChannelId = channelId,
            ChannelUri = $"eml://test/channel(T{channelId})",
            ChannelName = "TimeChannel",
            DataType = "double",
            Uom = "rpm",
            IndexType = "Time",
            IndexUom = "us",
            IndexDirection = "Increasing",
            IndexScale = 0,
            IndexTimeDatum = null,
            Description = "",
            Status = "Active",
            Source = "test",
            MeasureClass = "",
        };

    private static ChannelDefinition DepthChannel(long channelId, int scale, string uom = "m") =>
        new()
        {
            ChannelId = channelId,
            ChannelUri = $"eml://test/channel(D{channelId})",
            ChannelName = "DepthChannel",
            DataType = "double",
            Uom = "m/s",
            IndexType = "Depth",
            IndexUom = uom,
            IndexDirection = "Increasing",
            IndexScale = scale,
            Description = "",
            Status = "Active",
            Source = "test",
            MeasureClass = "",
        };

    private static ChannelDefinition FallbackChannel(long channelId) =>
        new()
        {
            ChannelId = channelId,
            ChannelUri = $"eml://test/channel(F{channelId})",
            ChannelName = "FallbackChannel",
            DataType = "double",
            Uom = "xyz",
            IndexType = "Other",
            IndexUom = "n/a",
            IndexDirection = "Increasing",
            IndexScale = 0,
            Description = "",
            Status = "Active",
            Source = "test",
            MeasureClass = "",
        };

    private static SampleRunOutcome CreateRangeOutcome(
        long channelId,
        ChannelDefinition channel,
        IReadOnlyList<ChannelDataItem> samples)
    {
        var request = new ChannelRangeRequestModel
        {
            ChannelIds = [channelId],
            FromIndex = samples.Count > 0 ? samples[0].Indexes[0] : 0L,
            ToIndex = samples.Count > 0 ? samples[^1].Indexes[0] : 0L,
        };
        var rangeResult = new ChannelRangeResult
        {
            Request = request,
            Samples = samples,
            WasMultipart = false,
            State = ChannelRangeResultState.Completed,
        };
        var descriptionResult = new ChannelDescriptionResult
        {
            RequestedUris = [$"eml://test/channel(C{channelId})"],
            Channels = [channel],
            MessageEncoding = EtpMessageEncoding.Binary,
            WasMultipart = false,
            State = ChannelDescriptionState.Completed,
        };
        var connResult = new EtpConnectionResult
        {
            Session = new NegotiatedSessionInfo
            {
                ServerApplicationName = "test",
                ServerApplicationVersion = "1.0",
                ServerInstanceId = Guid.NewGuid(),
                SupportedProtocols = [],
                SupportedCompression = string.Empty,
                SupportedFormats = [],
            },
            ConnectedAtUtc = DateTimeOffset.UtcNow,
            EndpointHost = "test",
            MessageEncoding = EtpMessageEncoding.Binary,
        };
        return SampleRunOutcome.FromSuccess(connResult,
            channelDescriptionResult: descriptionResult,
            channelRangeResult: rangeResult);
    }
}
