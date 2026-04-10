using EtpClient.Models;

namespace EtpClient.SampleConsole.Tests.TestSupport;

/// <summary>
/// Factory helpers for creating test instances of sample app types.
/// </summary>
internal static class SampleTestData
{
    /// <summary>Creates a fully populated valid <see cref="SampleConsoleOptions"/>.</summary>
    public static SampleConsoleOptions ValidOptions(
        string endpointUri = "wss://localhost:9090/etp",
        string username = "testuser",
        string password = "testpass",
        bool showSessionDetails = false) =>
        new()
        {
            EndpointUri = endpointUri,
            Username = username,
            Password = password,
            ShowSessionDetails = showSessionDetails,
        };

    /// <summary>Creates a <see cref="NegotiatedSessionInfo"/> for testing success outcomes.</summary>
    public static NegotiatedSessionInfo NegotiatedSession(
        string appName = "TestServer",
        string appVersion = "1.0.0",
        Guid? instanceId = null) =>
        new()
        {
            ServerApplicationName = appName,
            ServerApplicationVersion = appVersion,
            ServerInstanceId = instanceId ?? Guid.NewGuid(),
            SupportedProtocols = Array.Empty<SupportedProtocol>(),
            SupportedCompression = string.Empty,
            SupportedFormats = Array.Empty<string>(),
        };

    /// <summary>Creates an <see cref="EtpConnectionResult"/> for testing success outcomes.</summary>
    public static EtpConnectionResult ConnectionResult(
        string endpointHost = "localhost",
        string appName = "TestServer",
        string appVersion = "1.0.0",
        Guid? instanceId = null) =>
        new()
        {
            Session = NegotiatedSession(appName, appVersion, instanceId),
            ConnectedAtUtc = DateTimeOffset.UtcNow,
            EndpointHost = endpointHost,
        };
}
