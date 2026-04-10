using EtpClient.Models;
using Microsoft.Extensions.Configuration;

namespace EtpClient.IntegrationTests;

/// <summary>
/// Reads live-server credentials either from environment variables or from
/// .NET user secrets so that tests can connect to a real ETP server without
/// embedding credentials in source.
///
/// Environment variables:
///   ETP_LIVE_URI       – absolute ws:// or wss:// endpoint URI
///   ETP_LIVE_USERNAME  – Basic auth username
///   ETP_LIVE_PASSWORD  – Basic auth password
///
/// User secrets (secrets id: etp-client-integration-tests):
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:EndpointUri"  "wss://your-server/etp"
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:Username"     "your-username"
///   dotnet user-secrets --project tests/EtpClient.IntegrationTests set "Etp:Password"     "your-password"
/// </summary>
internal sealed class LiveServerSettings
{
    public string? Uri { get; }
    public string? Username { get; }
    public string? Password { get; }

    /// <summary>
    /// Returns a non-null message describing what is missing when the settings
    /// are incomplete, or <see langword="null"/> when all required values are set.
    /// </summary>
    public string? MissingReason { get; }

    public bool IsConfigured => MissingReason is null;

    private LiveServerSettings(string? uri, string? username, string? password, string source)
    {
        Uri = uri;
        Username = username;
        Password = password;

        if (string.IsNullOrWhiteSpace(uri))
            MissingReason = $"EndpointUri not set in {source}.";
        else if (string.IsNullOrWhiteSpace(username))
            MissingReason = $"Username not set in {source}.";
        else if (string.IsNullOrWhiteSpace(password))
            MissingReason = $"Password not set in {source}.";
    }

    /// <summary>
    /// Reads credentials from environment variables:
    /// <c>ETP_LIVE_URI</c>, <c>ETP_LIVE_USERNAME</c>, <c>ETP_LIVE_PASSWORD</c>.
    /// </summary>
    public static LiveServerSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("ETP_LIVE_URI"),
        Environment.GetEnvironmentVariable("ETP_LIVE_USERNAME"),
        Environment.GetEnvironmentVariable("ETP_LIVE_PASSWORD"),
        source: "environment variables (ETP_LIVE_URI / ETP_LIVE_USERNAME / ETP_LIVE_PASSWORD)");

    /// <summary>
    /// Reads credentials from .NET user secrets for this project
    /// (secrets id: <c>etp-client-integration-tests</c>), using the
    /// <c>Etp:EndpointUri</c>, <c>Etp:Username</c>, and <c>Etp:Password</c> keys.
    /// </summary>
    public static LiveServerSettings FromUserSecrets()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<LiveServerSettings>()
            .Build();

        return new(
            config["Etp:EndpointUri"],
            config["Etp:Username"],
            config["Etp:Password"],
            source: "user secrets (Etp:EndpointUri / Etp:Username / Etp:Password)");
    }

    /// <summary>Converts to <see cref="EtpConnectionOptions"/>. Only call when <see cref="IsConfigured"/> is true.</summary>
    public EtpConnectionOptions ToConnectionOptions() =>
        new(new System.Uri(Uri!), Username!, Password!);
}
