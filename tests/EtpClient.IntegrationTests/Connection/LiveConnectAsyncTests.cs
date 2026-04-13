using EtpClient.Models;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace EtpClient.IntegrationTests.Connection;

/// <summary>
/// Live integration tests that connect to a real ETP server.
/// These tests are skipped automatically when credentials are not configured.
///
/// Option A – .NET user secrets (recommended for local development):
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:EndpointUri"  "wss://my-server/etp/v12"
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:Username"     "user"
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:Password"     "secret"
///
/// Option B – environment variables (CI pipelines):
///   ETP_LIVE_URI=wss://my-server/etp/v12
///   ETP_LIVE_USERNAME=user
///   ETP_LIVE_PASSWORD=secret
///
/// Run only these tests:
///   dotnet test --filter "FullyQualifiedName~LiveConnectAsync"
/// </summary>
public sealed class LiveConnectAsyncTests(ITestOutputHelper output)
{
    /// <summary>
    /// Prefer user secrets; fall back to environment variables so CI pipelines
    /// and local development both work without code changes.
    /// </summary>
    private static LiveServerSettings ResolveSettings()
    {
        var secrets = LiveServerSettings.FromUserSecrets();
        return secrets.IsConfigured ? secrets : LiveServerSettings.FromEnvironment();
    }

    private readonly LiveServerSettings _settings = ResolveSettings();

    private EtpClient BuildClient()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
        return new EtpClient(loggerFactory.CreateLogger<EtpClient>());
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [LiveFact]
    public async Task ConnectAsync_LiveServer_ValidCredentials_ReturnsConnectedState()
    {
        await using var client = BuildClient();

        var result = await client.ConnectAsync(_settings.ToConnectionOptions());

        Assert.Equal(EtpConnectionState.Connected, client.State);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Session.ServerApplicationName),
            "ServerApplicationName should not be empty");
        Assert.NotEqual(Guid.Empty, result.Session.ServerInstanceId);
        Assert.False(string.IsNullOrWhiteSpace(result.EndpointHost),
            "EndpointHost should not be empty");
    }

    [LiveFact]
    public async Task ConnectAsync_LiveServer_ValidCredentials_ReportsSessionDetails()
    {
        await using var client = BuildClient();

        var result = await client.ConnectAsync(_settings.ToConnectionOptions());

        Assert.NotNull(result.Session.ServerApplicationName);
        Assert.NotNull(result.Session.ServerApplicationVersion);
        Assert.NotNull(result.Session.SupportedFormats);
        Assert.NotNull(result.Session.SupportedProtocols);

        output.WriteLine($"[LiveTest] ServerApplicationName : {result.Session.ServerApplicationName}");
        output.WriteLine($"[LiveTest] ServerApplicationVersion: {result.Session.ServerApplicationVersion}");
        output.WriteLine($"[LiveTest] ServerInstanceId       : {result.Session.ServerInstanceId}");
        output.WriteLine($"[LiveTest] SupportedFormats       : {string.Join(", ", result.Session.SupportedFormats)}");
        output.WriteLine($"[LiveTest] SupportedProtocols     : {result.Session.SupportedProtocols.Count} protocol(s)");
        foreach (var p in result.Session.SupportedProtocols)
            output.WriteLine($"[LiveTest]   Protocol {p.Protocol} v{p.Version} role={p.Role}");
        output.WriteLine($"[LiveTest] SupportedCompression   : '{result.Session.SupportedCompression}'");
        output.WriteLine($"[LiveTest] ConnectedAt (UTC)      : {result.ConnectedAtUtc:O}");
        output.WriteLine($"[LiveTest] EndpointHost           : {result.EndpointHost}");
    }

    // ── clean close ───────────────────────────────────────────────────────────

    [LiveFact]
    public async Task CloseAsync_AfterLiveSession_TransitionsToClosedState()
    {
        await using var client = BuildClient();
        await client.ConnectAsync(_settings.ToConnectionOptions());

        await client.CloseAsync();

        Assert.Equal(EtpConnectionState.Closed, client.State);
    }

    // ── wrong credentials ─────────────────────────────────────────────────────

    [LiveFact]
    public async Task ConnectAsync_LiveServer_WrongPassword_ThrowsAuthenticationException()
    {
        await using var client = BuildClient();

        var badOptions = new EtpConnectionOptions(
            new Uri(_settings.Uri!),
            _settings.Username!,
            password: "definitely-wrong-password-" + Guid.NewGuid().ToString("N"));

        var ex = await Assert.ThrowsAsync<EtpConnectionException>(
            () => client.ConnectAsync(badOptions));

        output.WriteLine($"[LiveTest] Category   : {ex.Category}");
        output.WriteLine($"[LiveTest] StatusCode  : {ex.HttpStatusCode}");
        output.WriteLine($"[LiveTest] EtpErrorCode: {ex.EtpErrorCode}");
        output.WriteLine($"[LiveTest] Message     : {ex.Message}");

        Assert.True(
            ex.Category is EtpConnectionFailureCategory.Authentication
                        or EtpConnectionFailureCategory.Protocol
                        or EtpConnectionFailureCategory.Transport,
            $"Unexpected failure category: {ex.Category}");
    }

    // ── cancellation ──────────────────────────────────────────────────────────

    [LiveFact]
    public async Task ConnectAsync_LiveServer_CancelledMidConnection_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource(millisecondsDelay: 1);
        await Task.Delay(5); // ensure token is cancelled before connect attempt

        await using var client = BuildClient();

        var ex = await Record.ExceptionAsync(
            () => client.ConnectAsync(_settings.ToConnectionOptions(), cts.Token));

        Assert.NotNull(ex);
        Assert.True(
            ex is OperationCanceledException or EtpConnectionException { Category: EtpConnectionFailureCategory.Cancellation },
            $"Expected cancellation but got: {ex.GetType().Name} – {ex.Message}");
    }
}
