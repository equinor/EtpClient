using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Encodes ETP Protocol 1 ChannelRangeRequest messages (consumer → producer) using Avro binary.
/// Body schema: { channelRanges: array&lt;ChannelRangeInfo&gt; }
/// ChannelRangeInfo fields: channelId(array&lt;long&gt;), startIndex(long), endIndex(long)
/// </summary>
internal static class ChannelRangeRequestMessage
{
    /// <summary>Encodes a ChannelRangeRequest frame using Avro binary encoding.</summary>
    public static ReadOnlyMemory<byte> EncodeBinaryFrame(
        IReadOnlyList<ChannelRangeInfoWire> ranges, long messageId)
    {
        var w = new AvroWriter();

        // Header
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelRangeRequest);
        w.WriteLong(0L);          // correlationId
        w.WriteLong(messageId);
        w.WriteInt(EtpMessageFlags.FinalPart);

        // Body: { channelRanges: array<ChannelRangeInfo> }
        w.WriteArrayStart(ranges.Count);
        foreach (var range in ranges)
        {
            // channelId: array<long>
            w.WriteArrayStart(range.ChannelIds.Count);
            foreach (var id in range.ChannelIds)
                w.WriteLong(id);
            w.WriteArrayEnd();

            w.WriteLong(range.StartIndex);
            w.WriteLong(range.EndIndex);
        }
        w.WriteArrayEnd();

        return w.ToArray();
    }
}
