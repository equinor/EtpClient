using EtpClient.Models;
using Microsoft.Extensions.Logging;

namespace EtpClient.Diagnostics;

/// <summary>
/// High-performance, secret-safe log messages for the ETP client.
/// No credential values are ever passed to any log method.
/// </summary>
internal static partial class EtpClientLog
{
    [LoggerMessage(1001, LogLevel.Information, "ETP connecting to {EndpointHost}")]
    public static partial void Connecting(ILogger logger, string endpointHost);

    [LoggerMessage(1002, LogLevel.Information,
        "ETP session established with '{ServerAppName}' at {EndpointHost}")]
    public static partial void SessionEstablished(ILogger logger, string serverAppName, string endpointHost);

    [LoggerMessage(1003, LogLevel.Warning,
        "ETP authentication rejected at {EndpointHost} (HTTP {StatusCode})")]
    public static partial void AuthenticationFailed(ILogger logger, string endpointHost, int statusCode);

    [LoggerMessage(1004, LogLevel.Error,
        "ETP session error at {EndpointHost}: category={FailureCategory}, etpErrorCode={EtpErrorCode}")]
    public static partial void SessionError(
        ILogger logger,
        string endpointHost,
        EtpConnectionFailureCategory failureCategory,
        int? etpErrorCode);

    [LoggerMessage(1005, LogLevel.Information, "ETP session closed for {EndpointHost}")]
    public static partial void SessionClosed(ILogger logger, string endpointHost);
}
