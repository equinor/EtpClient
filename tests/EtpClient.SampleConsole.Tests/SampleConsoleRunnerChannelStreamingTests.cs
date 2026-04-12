using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Unit tests for <see cref="SampleConsoleRunner"/> Protocol 1 channel streaming behavior:
/// channel description display, live streaming start/stop, range request display,
/// and secret-safe failure handling.
/// T011 [US1], T019 [US2], (no US3 runner tests needed beyond T030).
/// Tests are written test-first; they will fail until implementation is complete.
/// </summary>
public sealed class SampleConsoleRunnerChannelStreamingTests
{
    // ── T011 [US1]: describe-channels runner behavior ─────────────────────────

    [Fact]
    public async Task RunAsync_ServerSupportsChannelStreaming_CallsDescribeChannels()
    {
        // Arrange: connector supports Protocol 1
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(1, ProtocolVersion.Etp11, "producer"),
        ]);
        var describeResult = CreateDescriptionResult(["eml://witsml14/well(abc)/log(L1)"]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);
        connector.DescribeChannelsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(describeResult);

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        options.ChannelUri = "eml://witsml14/well(abc)/log(L1)";
        var runner = CreateRunner(options, () => connector, capture);

        // Act
        var outcome = await runner.RunAsync();

        // Assert: describe was called
        await connector.Received(1).DescribeChannelsAsync(
            Arg.Is<IReadOnlyList<string>>(uris => uris.Contains(options.ChannelUri!)),
            Arg.Any<CancellationToken>());
        Assert.NotNull(outcome.ChannelDescriptionResult);
    }

    [Fact]
    public async Task RunAsync_DescribeChannelsFails_OutcomeStillSucceeds_AndMessageInOutput()
    {
        // A failed describe should not fail the overall run (warn + continue)
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(1, ProtocolVersion.Etp11, "producer"),
        ]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);
        connector.DescribeChannelsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpChannelStreamingException("URI not supported.", "eml://bad/uri", etpErrorCode: 7));

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        options.ChannelUri = "eml://bad/uri";
        var runner = CreateRunner(options, () => connector, capture);

        var outcome = await runner.RunAsync();

        // Session itself succeeded
        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public async Task RunAsync_NoChannelUriConfigured_SkipsChannelDescribe()
    {
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(1, ProtocolVersion.Etp11, "producer"),
        ]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(); // no ChannelUri set
        var runner = CreateRunner(options, () => connector, capture);

        await runner.RunAsync();

        // DescribeChannelsAsync must NOT be called when no channel URI
        await connector.DidNotReceive().DescribeChannelsAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ServerDoesNotSupportChannelStreaming_SkipsChannelDescribe()
    {
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(3, ProtocolVersion.Etp11, "store"),
        ]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        options.ChannelUri = "eml://witsml14/well(abc)/log(L1)";
        var runner = CreateRunner(options, () => connector, capture);

        var outcome = await runner.RunAsync();

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.ChannelDescriptionResult);
        await connector.DidNotReceive().DescribeChannelsAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DescribeChannelsTimesOut_OutcomeStillSucceeds()
    {
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(1, ProtocolVersion.Etp11, "producer"),
        ]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);
        connector.DescribeChannelsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitUntilCanceledAsync<ChannelDescriptionResult>(callInfo.Arg<CancellationToken>()));

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        options.ChannelUri = "eml://witsml14/well(abc)/log(L1)";
        options.ProtocolRequestTimeoutSeconds = 1;
        var runner = CreateRunner(options, () => connector, capture);

        var outcome = await runner.RunAsync();

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.ChannelDescriptionResult);
        await connector.Received(1).DescribeChannelsAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DescribedChannelsAreClosedOrInvalid_SkipsLiveStreaming()
    {
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(1, ProtocolVersion.Etp11, "producer"),
        ]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);
        connector.DescribeChannelsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescriptionResult(
                ["eml://witsml14/well(abc)/log(L1)//logcurveinfo(RPM)"],
                channelId: -1L,
                status: "Closed"));

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        options.ChannelUri = "eml://witsml14/well(abc)/log(L1)//logcurveinfo(RPM)";
        var runner = CreateRunner(options, () => connector, capture);

        var outcome = await runner.RunAsync();

        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.ChannelDescriptionResult);
        Assert.Null(outcome.LiveStreamingResult);
        connector.DidNotReceive().StartChannelStreamingAsync(
            Arg.Any<IReadOnlyList<ChannelSubscriptionInfo>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DescribedChannelsAreClosedOrInvalid_SkipsChannelRangeRequest()
    {
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(1, ProtocolVersion.Etp11, "producer"),
        ]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);
        connector.DescribeChannelsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescriptionResult(
                ["eml://witsml14/well(abc)/log(L1)//logcurveinfo(RPM)"],
                channelId: -1L,
                status: "Closed"));

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        options.ChannelUri = "eml://witsml14/well(abc)/log(L1)//logcurveinfo(RPM)";
        options.ChannelRangeFromIndex = 1L;
        options.ChannelRangeToIndex = 10L;
        var runner = CreateRunner(options, () => connector, capture);

        var outcome = await runner.RunAsync();

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.ChannelRangeResult);
        await connector.DidNotReceive().RequestChannelRangeAsync(
            Arg.Any<ChannelRangeRequestModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LiveDataArrives_PrintsIndexNameAndValue()
    {
        var connResult = SampleTestData.ConnectionResult(supportedProtocols:
        [
            new SupportedProtocol(1, ProtocolVersion.Etp11, "producer"),
        ]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connResult);
        connector.DescribeChannelsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescriptionResult(["eml://witsml14/well(abc)/log(L1)"], channelId: 11L));
        connector.StartChannelStreamingAsync(Arg.Any<IReadOnlyList<ChannelSubscriptionInfo>>(), Arg.Any<CancellationToken>())
            .Returns(CreateLiveEvents());

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        options.ChannelUri = "eml://witsml14/well(abc)/log(L1)";
        var runner = CreateRunner(options, () => connector, capture);

        var outcome = await runner.RunAsync();

        Assert.True(outcome.Succeeded);
        // After formatting, raw index 1000 is rendered as a timestamp, not the plain long
        Assert.DoesNotContain("\n1000 ", capture.Out);
        Assert.Contains("CH1", capture.Out);
        Assert.Contains("12.5", capture.Out);
    }

    // ── factory helpers ───────────────────────────────────────────────────────

    private static ChannelDescriptionResult CreateDescriptionResult(
        IReadOnlyList<string> requestedUris,
        long channelId = 1L,
        string status = "Active")
    {
        var channels = requestedUris.Select((uri, i) => new ChannelDefinition
        {
            ChannelId = channelId + i,
            ChannelUri = uri + "/channel(CH1)",
            ChannelName = $"CH{i + 1}",
            DataType = "double",
            Uom = "rpm",
            IndexType = "Time",
            IndexUom = "ms",
            IndexDirection = "Increasing",
            Description = "",
            Status = status,
            Source = "test",
            MeasureClass = "",
        }).ToList();

        return new ChannelDescriptionResult
        {
            RequestedUris = requestedUris,
            Channels = channels,
            MessageEncoding = EtpMessageEncoding.Binary,
            WasMultipart = false,
            State = ChannelDescriptionState.Completed,
        };
    }

    private static SampleConsoleRunner CreateRunner(
        SampleConsoleOptions options,
        Func<IEtpConnector> connectorFactory,
        TestOutputCapture capture) =>
        new(options, connectorFactory, capture.CreateOutputWriter(), NullLogger<SampleConsoleRunner>.Instance);

    private static async Task<T> WaitUntilCanceledAsync<T>(CancellationToken ct)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        throw new InvalidOperationException("Unreachable");
    }

    private static async IAsyncEnumerable<ChannelEvent> CreateLiveEvents()
    {
        yield return new ChannelEvent
        {
            Kind = ChannelEventKind.Data,
            DataItems = [new ChannelDataItem { Indexes = [1000L], ChannelId = 11L, Value = 12.5 }],
        };

        yield return new ChannelEvent
        {
            Kind = ChannelEventKind.Remove,
            ChannelId = 11L,
        };

        await Task.CompletedTask;
    }
}
