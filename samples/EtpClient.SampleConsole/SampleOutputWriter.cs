using EtpClient.Models;

namespace EtpClient.SampleConsole;

/// <summary>
/// Writes secret-safe sample run summaries to the console.
/// </summary>
public sealed class SampleOutputWriter
{
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    /// <summary>Creates a writer targeting the standard console streams.</summary>
    public SampleOutputWriter() : this(Console.Out, Console.Error) { }

    /// <summary>Creates a writer targeting the specified output streams (for testing).</summary>
    public SampleOutputWriter(TextWriter @out, TextWriter error)
    {
        _out = @out;
        _error = error;
    }

    /// <summary>Writes a success summary to stdout.</summary>
    public void WriteSuccess(SampleRunOutcome outcome, bool showSessionDetails)
    {
        _out.WriteLine();
        _out.WriteLine("=== ETP Session Established ===");
        _out.WriteLine($"  Endpoint : {outcome.EndpointHost}");
        _out.WriteLine($"  Encoding : {outcome.MessageEncoding}");
        _out.WriteLine($"  State    : {outcome.FinalState}");

        if (showSessionDetails)
        {
            _out.WriteLine($"  Server   : {outcome.ServerApplicationName} {outcome.ServerApplicationVersion}");
            _out.WriteLine($"  Instance : {outcome.ServerInstanceId}");

            if (outcome.SupportedProtocols.Count == 0)
            {
                _out.WriteLine("  Protocols: (none reported)");
            }
            else
            {
                _out.WriteLine("  Protocols:");
                foreach (var protocol in outcome.SupportedProtocols)
                    _out.WriteLine($"    - {protocol.Protocol} v{protocol.Version} role={protocol.Role}");
            }
        }

        _out.WriteLine("================================");
        _out.WriteLine();
    }

    /// <summary>Writes a failure summary to stderr.</summary>
    public void WriteFailure(SampleRunOutcome outcome)
    {
        _error.WriteLine();
        _error.WriteLine("=== ETP Sample Failed ===");
        _error.WriteLine($"  State    : {outcome.FinalState}");
        _error.WriteLine($"  Category : {outcome.FailureCategory}");
        _error.WriteLine($"  Message  : {outcome.FailureMessage}");
        _error.WriteLine("=========================");
        _error.WriteLine();
    }

    /// <summary>Writes a discovery result summary to stdout.</summary>
    public void WriteDiscovery(SampleRunOutcome outcome)
    {
        var discovery = outcome.DiscoveryResult;
        if (discovery is null)
            return;

        _out.WriteLine();
        _out.WriteLine("=== Discovery Results ===");
        _out.WriteLine($"  URI      : {discovery.RequestedUri}");

        if (discovery.WasEmptyAcknowledged || discovery.Resources.Count == 0)
        {
            _out.WriteLine("  (no children found)");
        }
        else
        {
            foreach (var resource in discovery.Resources)
            {
                _out.WriteLine($"  [{resource.ResourceType}] {resource.Name}");
                _out.WriteLine($"    Uri: {resource.Uri}");
            }
        }

        _out.WriteLine("=========================");
        _out.WriteLine();
    }

    /// <summary>Writes a channel description result summary to stdout.</summary>
    public void WriteChannelDescription(SampleRunOutcome outcome)
    {
        var description = outcome.ChannelDescriptionResult;
        if (description is null)
            return;

        _out.WriteLine();
        _out.WriteLine("=== Channel Description ===");
        _out.WriteLine($"  URIs     : {string.Join(", ", description.RequestedUris)}");
        _out.WriteLine($"  Encoding : {description.MessageEncoding}");

        if (description.Channels.Count == 0)
        {
            _out.WriteLine("  (no channels found)");
        }
        else
        {
            foreach (var channel in description.Channels)
            {
                _out.WriteLine($"  [{channel.ChannelId}] {channel.ChannelName}  ({channel.DataType}) {channel.Uom}");
                _out.WriteLine($"    Uri   : {channel.ChannelUri}");
                _out.WriteLine($"    Index : {channel.IndexType} [{channel.IndexUom}] {channel.IndexDirection}");
                _out.WriteLine($"    Status: {channel.Status}");
            }
        }

        _out.WriteLine("===========================");
        _out.WriteLine();
    }

    /// <summary>Writes a live streaming result summary to stdout.</summary>
    public void WriteLiveStreaming(SampleRunOutcome outcome)
    {
        var streaming = outcome.LiveStreamingResult;
        if (streaming is null)
            return;

        _out.WriteLine();
        _out.WriteLine("=== Live Streaming Result ===");
        _out.WriteLine($"  Channels : {string.Join(", ", streaming.SubscribedChannelIds)}");
        _out.WriteLine($"  Events   : {streaming.EventsReceived}");
        _out.WriteLine($"  Ended by : {(streaming.EndedByRemove ? "ChannelRemove" : "cancellation")}");
        _out.WriteLine("=============================");
        _out.WriteLine();
    }

    /// <summary>Writes one line per streamed data item as it arrives.</summary>
    public void WriteLiveData(
        IReadOnlyList<ChannelDataItem> items,
        IReadOnlyDictionary<long, ChannelDefinition> channelsById)
    {
        foreach (var item in items)
        {
            var index = item.Indexes.Count == 0
                ? "(no index)"
                : string.Join(", ", item.Indexes);
            var name = channelsById.TryGetValue(item.ChannelId, out var channel)
                ? channel.ChannelName
                : $"Channel {item.ChannelId}";

            _out.WriteLine($"{index}  {name}  {FormatValue(item.Value)}");
        }
    }

    /// <summary>Writes a channel range result summary to stdout.</summary>
    public void WriteChannelRange(SampleRunOutcome outcome)
    {
        var range = outcome.ChannelRangeResult;
        if (range is null)
            return;

        _out.WriteLine();
        _out.WriteLine("=== Channel Range Result ===");
        _out.WriteLine($"  Channels : {string.Join(", ", range.Request.ChannelIds)}");
        _out.WriteLine($"  Range    : {range.Request.FromIndex} - {range.Request.ToIndex}");
        _out.WriteLine($"  Samples  : {range.Samples.Count}");
        _out.WriteLine($"  Multipart: {range.WasMultipart}");
        _out.WriteLine($"  State    : {range.State}");
        _out.WriteLine("============================");
        _out.WriteLine();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "(null)",
        double[] doubles => string.Join(", ", doubles),
        byte[] bytes => Convert.ToHexString(bytes),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
