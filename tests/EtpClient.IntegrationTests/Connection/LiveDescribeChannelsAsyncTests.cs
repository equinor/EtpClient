using EtpClient.Models;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Live integration tests for <see cref="EtpClient.DescribeChannelsAsync"/>.
/// These tests are skipped automatically when the base server credentials are
/// not configured (via user secrets or environment variables).
///
/// Fill in the constant below before running:
///   <see cref="_describeUri"/>  – URI passed to ChannelDescribe
///
/// Run only these tests:
///   dotnet test --filter "FullyQualifiedName~LiveDescribeChannels"
/// </summary>
public sealed class LiveDescribeChannelsAsyncTests
{
    private readonly LiveServerSettings _settings = ResolveSettings();
    private readonly ITestOutputHelper _output;

    private readonly string _describeUri;

    private static readonly string[] ExpectedChannelNames =
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

    private static LiveServerSettings ResolveSettings()
    {
        var secrets = LiveServerSettings.FromUserSecrets();
        return secrets.IsConfigured ? secrets : LiveServerSettings.FromEnvironment();
    }

    public LiveDescribeChannelsAsyncTests(ITestOutputHelper output)
    {
        _output = output;
        var wellUid = "ddb2db2c-ffcc-429b-be07-2f76aa277f22";
        var wellboreUid = "6541703c-a77d-4c1b-b9a8-08c51939d13f";
        var logUid = "MSP_Surface_Time_VLOG";
        _describeUri = $"eml://witsml14/well({wellUid})/wellbore({wellboreUid})/log({logUid})";
        var foo =
            "eml://witsml14/well(ddb2db2c-ffcc-429b-be07-2f76aa277f22)/wellbore(6541703c-a77d-4c1b-b9a8-08c51939d13f)/log(MSP_Surface_Time_VLOG)";
    }

    private static EtpClient BuildClient()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
        return new EtpClient(loggerFactory.CreateLogger<EtpClient>());
    }

    /// <summary>
    /// Connects and describes channels, asserting that all expected mnemonics are present
    /// and logging full metadata for each returned channel.
    /// </summary>
    [LiveFact]
    public async Task DescribeChannelsAsync_LiveServer_ReturnsExpectedChannels()
    {
        if (string.IsNullOrWhiteSpace(_describeUri))
        {
            _output.WriteLine("[LiveTest] Skipping: DescribeUri constant is not set.");
            return;
        }

        await using var client = BuildClient();
        await client.ConnectAsync(_settings.ToConnectionOptions());

        _output.WriteLine($"[LiveTest] DescribeUri: {_describeUri}");

        var result = await client.DescribeChannelsAsync([_describeUri]);

        _output.WriteLine($"[LiveTest] State       : {result.State}");
        _output.WriteLine($"[LiveTest] WasMultipart: {result.WasMultipart}");
        _output.WriteLine($"[LiveTest] Channels    : {result.Channels.Count}");
        _output.WriteLine(string.Empty);

        foreach (var ch in result.Channels.Where(c => ExpectedChannelNames.Contains(c.ChannelName)))
        {
            _output.WriteLine($"  ChannelId  : {ch.ChannelId}");
            _output.WriteLine($"  Name       : {ch.ChannelName}");
            _output.WriteLine($"  Uri        : {ch.ChannelUri}");
            _output.WriteLine($"  DataType   : {ch.DataType}");
            _output.WriteLine($"  Uom        : {ch.Uom}");
            _output.WriteLine($"  Status     : {ch.Status}");
            _output.WriteLine($"  IndexType  : {ch.IndexType}");
            _output.WriteLine($"  IndexUom   : {ch.IndexUom}");
            _output.WriteLine($"  IndexDir   : {ch.IndexDirection}");
            _output.WriteLine($"  IndexScale : {ch.IndexScale}");
            _output.WriteLine($"  TimeDatum  : {ch.IndexTimeDatum ?? "null"}");
            _output.WriteLine($"  StartIndex : {ch.StartIndex?.ToString() ?? "null"}");
            _output.WriteLine($"  EndIndex   : {ch.EndIndex?.ToString() ?? "null"}");
            _output.WriteLine(string.Empty);
        }

        Assert.Equal(ChannelDescriptionState.Completed, result.State);
        Assert.NotEmpty(result.Channels);

        var returnedNames = result.Channels.Select(c => c.ChannelName).ToHashSet();
        foreach (var expected in ExpectedChannelNames)
        {
            Assert.Contains(expected, returnedNames);
        }
    }
}
