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

    [LoggerMessage(1006, LogLevel.Debug,
        "ETP using {MessageEncoding} encoding for connection to {EndpointHost}")]
    public static partial void EncodingSelected(ILogger logger, string endpointHost, EtpMessageEncoding messageEncoding);

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

    [LoggerMessage(1007, LogLevel.Debug,
        "ETP discovery request for URI '{DiscoveryUri}' at {EndpointHost}")]
    public static partial void DiscoveryStarted(ILogger logger, string endpointHost, string discoveryUri);

    [LoggerMessage(1008, LogLevel.Debug,
        "ETP discovery returned {ResourceCount} resource(s) for URI '{DiscoveryUri}' at {EndpointHost}")]
    public static partial void DiscoveryCompleted(
        ILogger logger, string endpointHost, string discoveryUri, int resourceCount);

    [LoggerMessage(1009, LogLevel.Debug,
        "ETP discovery for URI '{DiscoveryUri}' at {EndpointHost}: no children (Acknowledge)")]
    public static partial void DiscoveryEmpty(ILogger logger, string endpointHost, string discoveryUri);

    [LoggerMessage(1010, LogLevel.Warning,
        "ETP discovery failed for URI '{DiscoveryUri}' at {EndpointHost}: etpErrorCode={EtpErrorCode}")]
    public static partial void DiscoveryFailed(
        ILogger logger, string endpointHost, string discoveryUri, int? etpErrorCode);

    // ── Protocol 1 (ChannelStreaming) ─────────────────────────────────────────

    [LoggerMessage(1011, LogLevel.Debug,
        "ETP channel describe requested for '{ChannelTarget}' at {EndpointHost}")]
    public static partial void DescribeChannelsStarted(ILogger logger, string endpointHost, string channelTarget);

    [LoggerMessage(1012, LogLevel.Debug,
        "ETP channel describe returned {ChannelCount} channel(s) for '{ChannelTarget}' at {EndpointHost}")]
    public static partial void DescribeChannelsCompleted(
        ILogger logger, string endpointHost, string channelTarget, int channelCount);

    [LoggerMessage(1013, LogLevel.Warning,
        "ETP channel describe failed for '{ChannelTarget}' at {EndpointHost}: etpErrorCode={EtpErrorCode}")]
    public static partial void DescribeChannelsFailed(
        ILogger logger, string endpointHost, string channelTarget, int? etpErrorCode);

    [LoggerMessage(1014, LogLevel.Debug,
        "ETP channel streaming started for {ChannelCount} channel(s) at {EndpointHost}")]
    public static partial void StreamingStarted(ILogger logger, string endpointHost, int channelCount);

    [LoggerMessage(1015, LogLevel.Debug,
        "ETP channel streaming stopped for {ChannelCount} channel(s) at {EndpointHost}")]
    public static partial void StreamingStopped(ILogger logger, string endpointHost, int channelCount);

    [LoggerMessage(1016, LogLevel.Debug,
        "ETP channel streaming ended at {EndpointHost}: received ChannelRemove for channel {ChannelId}")]
    public static partial void StreamingChannelRemoved(ILogger logger, string endpointHost, long channelId);

    [LoggerMessage(1017, LogLevel.Warning,
        "ETP channel streaming failed at {EndpointHost}: etpErrorCode={EtpErrorCode}")]
    public static partial void StreamingFailed(ILogger logger, string endpointHost, int? etpErrorCode);

    [LoggerMessage(1018, LogLevel.Debug,
        "ETP channel range request started for {ChannelCount} channel(s) at {EndpointHost}")]
    public static partial void RangeRequestStarted(ILogger logger, string endpointHost, int channelCount);

    [LoggerMessage(1019, LogLevel.Debug,
        "ETP channel range request returned {SampleCount} sample(s) for {ChannelCount} channel(s) at {EndpointHost}")]
    public static partial void RangeRequestCompleted(ILogger logger, string endpointHost, int channelCount, int sampleCount);

    [LoggerMessage(1020, LogLevel.Warning,
        "ETP channel range request failed at {EndpointHost}: etpErrorCode={EtpErrorCode}")]
    public static partial void RangeRequestFailed(ILogger logger, string endpointHost, int? etpErrorCode);
}
