using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Unit tests for the discovery integration in <see cref="SampleConsoleRunner"/>.
/// T011/T025 [US1, US3]: Verifies runner calls discovery after connecting and
/// handles discovery failures gracefully.
/// </summary>
public sealed class SampleConsoleRunnerDiscoveryTests
{
    private static readonly SupportedProtocol DiscoveryProtocol = new(3, ProtocolVersion.Etp11, "store");

    // ── discovery is called after connect ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_AfterConnect_CallsDiscoverResourcesAsync()
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(SampleTestData.ConnectionResult(supportedProtocols: [DiscoveryProtocol]));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        await runner.RunAsync();

        await connector.Received(1).DiscoverResourcesAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DiscoverySucceeds_OutcomeContainsDiscoveryResult()
    {
        var discoveryResult = CreateDiscoveryResult(["eml://witsml20", "eml://eml21"]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(SampleTestData.ConnectionResult(supportedProtocols: [DiscoveryProtocol]));
        connector.DiscoverResourcesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(discoveryResult);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        var outcome = await runner.RunAsync();

        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.DiscoveryResult);
        Assert.Equal(2, outcome.DiscoveryResult!.Resources.Count);
    }

    [Fact]
    public async Task RunAsync_DiscoveryThrowsEtpDiscoveryException_StillSucceeds()
    {
        // Discovery failure is non-fatal: runner logs a warning but returns success
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(SampleTestData.ConnectionResult(supportedProtocols: [DiscoveryProtocol]));
        connector.DiscoverResourcesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpDiscoveryException("Server error", "eml://"));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        var outcome = await runner.RunAsync();

        // Still reports success (connection succeeded) even though discovery failed
        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.DiscoveryResult);
    }

    [Fact]
    public async Task RunAsync_DiscoveryThrowsEtpDiscoveryException_WritesNoDiscoveryOutput()
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(SampleTestData.ConnectionResult(supportedProtocols: [DiscoveryProtocol]));
        connector.DiscoverResourcesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpDiscoveryException("Server error", "eml://"));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        await runner.RunAsync();

        // Success output written but no discovery section
        Assert.Contains("ETP Session Established", capture.Out);
        Assert.DoesNotContain("Discovery Results", capture.Out);
    }

    [Fact]
    public async Task RunAsync_DiscoverySucceeds_WritesDiscoveryResultsToOutput()
    {
        var discoveryResult = CreateDiscoveryResult(["eml://witsml20"]);

        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(SampleTestData.ConnectionResult(supportedProtocols: [DiscoveryProtocol]));
        connector.DiscoverResourcesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(discoveryResult);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        await runner.RunAsync();

        Assert.Contains("Discovery Results", capture.Out);
        Assert.Contains("eml://witsml20", capture.Out);
    }

    [Fact]
    public async Task RunAsync_WhenDiscoveryNotNegotiated_DoesNotCallDiscoverResourcesAsync()
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(SampleTestData.ConnectionResult());

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        var outcome = await runner.RunAsync();

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.DiscoveryResult);
        await connector.DidNotReceive().DiscoverResourcesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain("Discovery Results", capture.Out);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static SampleConsoleRunner CreateRunner(
        SampleConsoleOptions options,
        TestOutputCapture capture,
        Func<IEtpConnector> factory) =>
        new(options, factory, capture.CreateOutputWriter(), NullLogger<SampleConsoleRunner>.Instance);

    private static DiscoveryResult CreateDiscoveryResult(IEnumerable<string> uris) =>
        new()
        {
            RequestedUri = "eml://",
            Resources = uris.Select(u => new DiscoveredResource
            {
                Uri = u,
                ContentType = "",
                Name = u.Replace("eml://", ""),
                ChannelSubscribable = false,
                CustomData = new Dictionary<string, string>(),
                ResourceType = "UriProtocol",
                HasChildren = 1,
                ObjectNotifiable = false,
            }).ToList(),
            WasEmptyAcknowledged = false,
            MessageEncoding = EtpMessageEncoding.Binary,
        };
}
