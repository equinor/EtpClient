using EtpClient.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Opt-in live integration tests for encoding selection against a real ETP endpoint.
/// T029 [US3]: Skipped automatically when credentials are not configured.
///
/// Requires live server credentials plus an encoding selection:
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:EndpointUri"  "wss://my-server/etp"
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:Username"     "user"
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:Password"     "secret"
///
/// Run only these tests:
///   dotnet test --filter "FullyQualifiedName~LiveConnectAsyncEncoding"
/// </summary>
public sealed class LiveConnectAsyncEncodingTests
{
    private static LiveServerSettings ResolveSettings()
    {
        var secrets = LiveServerSettings.FromUserSecrets();
        return secrets.IsConfigured ? secrets : LiveServerSettings.FromEnvironment();
    }

    private readonly LiveServerSettings _settings = ResolveSettings();

    private global::EtpClient.EtpClient BuildClient()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        return new global::EtpClient.EtpClient(loggerFactory.CreateLogger<global::EtpClient.EtpClient>());
    }

    // ── Binary encoding against live server ───────────────────────────────────

    [LiveFact]
    public async Task ConnectAsync_LiveServer_BinaryEncoding_Connects()
    {
        await using var client = BuildClient();
        var options = new EtpConnectionOptions(
            _settings.ToConnectionOptions().EndpointUri,
            _settings.ToConnectionOptions().Username,
            _settings.ToConnectionOptions().Password,
            messageEncoding: EtpMessageEncoding.Binary);

        var result = await client.ConnectAsync(options);

        Assert.Equal(EtpConnectionState.Connected, client.State);
        Assert.Equal(EtpMessageEncoding.Binary, result.MessageEncoding);
    }

    // ── JSON encoding against live server ─────────────────────────────────────

    [LiveFact]
    public async Task ConnectAsync_LiveServer_JsonEncoding_Connects()
    {
        await using var client = BuildClient();
        var options = new EtpConnectionOptions(
            _settings.ToConnectionOptions().EndpointUri,
            _settings.ToConnectionOptions().Username,
            _settings.ToConnectionOptions().Password,
            messageEncoding: EtpMessageEncoding.Json);

        var result = await client.ConnectAsync(options);

        Assert.Equal(EtpConnectionState.Connected, client.State);
        Assert.Equal(EtpMessageEncoding.Json, result.MessageEncoding);
    }
}
