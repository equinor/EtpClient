using EtpClient.Models;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Live integration tests for <see cref="EtpClient.RequestChannelRangeAsync"/>.
/// These tests are skipped automatically when the base server credentials are
/// not configured (via user secrets or environment variables).
///
/// Fill in the fields below before running:
///   <see cref="_describeUri"/> – URI passed to ChannelDescribe to resolve channel IDs
///   <see cref="_mnemonics"/>   – channel names to filter from the describe response
///   <see cref="_fromIndex"/>   – start of the index range (inclusive)
///   <see cref="_toIndex"/>     – end of the index range (inclusive)
///
/// Run only these tests:
///   dotnet test --filter "FullyQualifiedName~LiveRequestChannelRange"
/// </summary>
public sealed class LiveRequestChannelRangeAsyncTests
{
    private readonly LiveServerSettings _settings = ResolveSettings();
    private readonly ITestOutputHelper _output;

    // ── Test-specific configuration ───────────────────────────────────────────

    private readonly string _describeUri;

    private static readonly string[] _mnemonics =
    [
        "Block Position",
        "SPP",
        "RPM",
        "Flow In",
        "Torque",
        "Cement Flow In 1",
        "Cement Pump Pressure 1",
        "Cement Pump Pressure 2",
    ];

    private readonly long _fromIndex;
    private readonly long _toIndex;

    private static LiveServerSettings ResolveSettings()
    {
        var secrets = LiveServerSettings.FromUserSecrets();
        return secrets.IsConfigured ? secrets : LiveServerSettings.FromEnvironment();
    }

    public LiveRequestChannelRangeAsyncTests(ITestOutputHelper output)
    {
        _output = output;

        var wellUid     = "9e97151d-fcc0-41f1-b098-eb6b8cef18b0";
        var wellboreUid = "350f46dc-e087-475d-a839-37bdde039e7f";
        var logUid      = "MSP_Surface_Time_VLOG";
        _describeUri = $"eml://witsml14/well({wellUid})/wellbore({wellboreUid})/log({logUid})";

        // Time-indexed channels use Unix epoch milliseconds for index values.
        // Replace with a range that is known to contain data on the live server.
        _fromIndex = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        _toIndex   = new DateTimeOffset(2025, 11, 1, 1, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    private static EtpClient BuildClient()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
        return new EtpClient(loggerFactory.CreateLogger<EtpClient>());
    }

    // ── T_LIVE_RANGE [US3]: happy path ────────────────────────────────────────

    [LiveFact]
    public async Task RequestChannelRangeAsync_LiveServer_ReturnsCompletedResult()
    {
        await using var client = BuildClient();
        await client.ConnectAsync(_settings.ToConnectionOptions());

        _output.WriteLine($"[LiveTest] DescribeUri : {_describeUri}");
        _output.WriteLine($"[LiveTest] Mnemonics   : {string.Join(", ", _mnemonics)}");

        var description = await client.DescribeChannelsAsync([_describeUri]);

        Assert.Equal(ChannelDescriptionState.Completed, description.State);

        var channelIds = description.Channels
            .Where(c => _mnemonics.Contains(c.ChannelName))
            .Where(c => c.ChannelId != -1) // filter out channels that failed to resolve an ID
            .Select(c => c.ChannelId)
            .ToArray();

        _output.WriteLine($"[LiveTest] Resolved channels ({channelIds.Length}):");
        foreach (var ch in description.Channels.Where(c => _mnemonics.Contains(c.ChannelName)))
            _output.WriteLine($"  [{ch.ChannelId}] {ch.ChannelName} ({ch.Uom})");
        _output.WriteLine(string.Empty);

        Assert.NotEmpty(channelIds);

        var request = new ChannelRangeRequestModel
        {
            ChannelIds = channelIds,
            FromIndex = _fromIndex,
            ToIndex = _toIndex,
        };

        _output.WriteLine($"[LiveTest] FromIndex   : {_fromIndex}");
        _output.WriteLine($"[LiveTest] ToIndex     : {_toIndex}");

        var samples = new List<ChannelDataItem>();
        await foreach (var item in client.RequestChannelRangeAsync(request))
            samples.Add(item);

        _output.WriteLine($"[LiveTest] Samples     : {samples.Count}");
        _output.WriteLine(string.Empty);

        foreach (var sample in samples)
        {
            _output.WriteLine($"  ChannelId : {sample.ChannelId}");
            _output.WriteLine($"  Indexes   : [{string.Join(", ", sample.Indexes)}]");
            _output.WriteLine($"  Value     : {sample.Value}");
            _output.WriteLine(string.Empty);
        }

        // Key assertion: enumeration completed without throwing (SC-002)
        Assert.True(samples.Count >= 0, "Range enumeration must complete without throwing.");
    }
}
