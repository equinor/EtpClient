using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Unit tests for <see cref="SampleOutputWriter.WriteDiscovery"/>.
/// T011/T025 [US1, US3]: Verifies that discovery results and empty outcomes
/// are rendered correctly.
/// </summary>
public sealed class SampleOutputWriterDiscoveryTests
{
    // ── WriteDiscovery with resources ─────────────────────────────────────────

    [Fact]
    public void WriteDiscovery_WithResources_WritesAllResourceNames()
    {
        var outcome = CreateOutcomeWithResources(
        [
            ("eml://witsml20/well(001)", "WITSML Well 001", "DataObject"),
            ("eml://witsml20/well(002)", "WITSML Well 002", "DataObject"),
        ]);
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();

        writer.WriteDiscovery(outcome);

        Assert.Contains("WITSML Well 001", capture.Out);
        Assert.Contains("WITSML Well 002", capture.Out);
    }

    [Fact]
    public void WriteDiscovery_WithResources_WritesUris()
    {
        var outcome = CreateOutcomeWithResources(
        [
            ("eml://witsml20/well(abc)", "Well ABC", "DataObject"),
        ]);
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();

        writer.WriteDiscovery(outcome);

        Assert.Contains("eml://witsml20/well(abc)", capture.Out);
    }

    [Fact]
    public void WriteDiscovery_WithResources_WritesResourceType()
    {
        var outcome = CreateOutcomeWithResources(
        [
            ("eml://witsml20", "witsml20", "UriProtocol"),
        ]);
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();

        writer.WriteDiscovery(outcome);

        Assert.Contains("UriProtocol", capture.Out);
    }

    // ── WriteDiscovery empty ──────────────────────────────────────────────────

    [Fact]
    public void WriteDiscovery_EmptyResult_WritesNoChildrenMessage()
    {
        var outcome = CreateOutcomeEmpty();
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();

        writer.WriteDiscovery(outcome);

        Assert.Contains("no children", capture.Out);
    }

    [Fact]
    public void WriteDiscovery_EmptyResult_WritesRequestedUri()
    {
        var outcome = CreateOutcomeEmpty();
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();

        writer.WriteDiscovery(outcome);

        Assert.Contains("eml://", capture.Out);
    }

    // ── WriteDiscovery null ───────────────────────────────────────────────────

    [Fact]
    public void WriteDiscovery_NullDiscoveryResult_WritesNothing()
    {
        // Outcome has no discovery (e.g. discovery failed)
        var outcome = CreateOutcomeNoDiscovery();
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();

        writer.WriteDiscovery(outcome);

        Assert.Equal(string.Empty, capture.Out);
    }

    // ── WriteDiscovery — nothing written to stderr ─────────────────────────────

    [Fact]
    public void WriteDiscovery_WithResources_WritesNothingToStderr()
    {
        var outcome = CreateOutcomeWithResources(
        [
            ("eml://witsml20", "witsml20", "UriProtocol"),
        ]);
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();

        writer.WriteDiscovery(outcome);

        Assert.Equal(string.Empty, capture.Error);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static SampleRunOutcome CreateOutcomeWithResources(
        IEnumerable<(string uri, string name, string resourceType)> resources)
    {
        var discoveryResult = new DiscoveryResult
        {
            RequestedUri = "eml://",
            Resources = resources.Select(t => new DiscoveredResource
            {
                Uri = t.uri,
                ContentType = "",
                Name = t.name,
                ChannelSubscribable = false,
                CustomData = new Dictionary<string, string>(),
                ResourceType = t.resourceType,
                HasChildren = 0,
                ObjectNotifiable = false,
            }).ToList(),
            WasEmptyAcknowledged = false,
            MessageEncoding = EtpMessageEncoding.Binary,
        };

        return SampleRunOutcome.FromSuccess(
            SampleTestData.ConnectionResult(),
            discoveryResult);
    }

    private static SampleRunOutcome CreateOutcomeEmpty()
    {
        var discoveryResult = new DiscoveryResult
        {
            RequestedUri = "eml://",
            Resources = [],
            WasEmptyAcknowledged = true,
            MessageEncoding = EtpMessageEncoding.Binary,
        };

        return SampleRunOutcome.FromSuccess(
            SampleTestData.ConnectionResult(),
            discoveryResult);
    }

    private static SampleRunOutcome CreateOutcomeNoDiscovery() =>
        SampleRunOutcome.FromSuccess(SampleTestData.ConnectionResult(), discoveryResult: null);
}
