using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace EtpClient.Instrumentation;

/// <summary>
/// Central owner of the <see cref="ActivitySource"/> and <see cref="Meter"/> singletons
/// for ETP instrumentation, plus helper methods used by <c>EtpSessionManager</c> call sites.
/// Internal to the library; application code never references this class directly.
/// </summary>
internal static class EtpInstrumentation
{
    private const string SourceName = "EtpClient";
    private const int MaxAttributeLength = 512;

    private static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    // ── Diagnostic primitives ────────────────────────────────────────────────

    internal static readonly ActivitySource Source = new(SourceName, Version);

    internal static readonly Meter EtpMeter = new(SourceName, Version);

    // ── Metric instruments ───────────────────────────────────────────────────

    internal static readonly UpDownCounter<int> ActiveConnections =
        EtpMeter.CreateUpDownCounter<int>(
            "etp.client.active_connections",
            unit: "{connection}",
            description: "Number of currently open ETP sessions.");

    internal static readonly Histogram<double> OperationDuration =
        EtpMeter.CreateHistogram<double>(
            "etp.client.operation.duration",
            unit: "s",
            description: "Duration of ETP operations in seconds.");

    internal static readonly Counter<long> OperationErrors =
        EtpMeter.CreateCounter<long>(
            "etp.client.operation.errors",
            unit: "{error}",
            description: "Number of failed ETP operations.");

    internal static readonly Counter<long> MessagesSent =
        EtpMeter.CreateCounter<long>(
            "etp.client.messages.sent",
            unit: "{message}",
            description: "Number of WebSocket frames sent.");

    internal static readonly Counter<long> MessagesReceived =
        EtpMeter.CreateCounter<long>(
            "etp.client.messages.received",
            unit: "{message}",
            description: "Number of WebSocket frames received.");

    // ── Activity helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Opens an <c>etp.connect</c> span as a child of the ambient context.
    /// Returns <see langword="null"/> if no listener is attached (zero-overhead path).
    /// </summary>
    internal static Activity? StartConnectActivity(string host, int port)
    {
        var activity = Source.StartActivity("etp.connect", ActivityKind.Client);
        if (activity is null)
            return null;

        activity.SetTag("server.address", host);
        activity.SetTag("server.port", port);
        return activity;
    }

    /// <summary>
    /// Opens a named span as a child of the ambient context.
    /// Returns <see langword="null"/> if no listener is attached.
    /// </summary>
    internal static Activity? StartOperationActivity(string spanName, string host, int port)
    {
        var activity = Source.StartActivity(spanName, ActivityKind.Client);
        if (activity is null)
            return null;

        activity.SetTag("server.address", host);
        activity.SetTag("server.port", port);
        return activity;
    }

    /// <summary>
    /// Increments <see cref="OperationErrors"/> with standard tags.
    /// <paramref name="etpErrorCode"/> is only included when non-null.
    /// </summary>
    internal static void RecordOperationError(string operation, string host, int? etpErrorCode)
    {
        if (etpErrorCode.HasValue)
        {
            OperationErrors.Add(1,
                new KeyValuePair<string, object?>("etp.operation", operation),
                new KeyValuePair<string, object?>("server.address", host),
                new KeyValuePair<string, object?>("etp.error_code", etpErrorCode.Value));
        }
        else
        {
            OperationErrors.Add(1,
                new KeyValuePair<string, object?>("etp.operation", operation),
                new KeyValuePair<string, object?>("server.address", host));
        }
    }

    /// <summary>
    /// Truncates <paramref name="value"/> to at most <see cref="MaxAttributeLength"/> characters.
    /// Prevents OTEL export failures for very long URI strings.
    /// </summary>
    internal static string TruncateAttribute(string value) =>
        value.Length <= MaxAttributeLength ? value : value[..MaxAttributeLength];
}
