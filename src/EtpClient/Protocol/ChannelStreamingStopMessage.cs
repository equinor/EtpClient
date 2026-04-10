using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Encodes ETP Protocol 1 ChannelStreamingStop messages (consumer → producer) using Avro binary.
/// Body schema: { channelIds: array&lt;long&gt; }
/// </summary>
internal static class ChannelStreamingStopMessage
{
    /// <summary>Encodes a ChannelStreamingStop frame using Avro binary encoding.</summary>
    public static ReadOnlyMemory<byte> EncodeBinaryFrame(
        IReadOnlyList<long> channelIds, long messageId)
    {
        var w = new AvroWriter();

        // Header
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelStreamingStop);
        w.WriteLong(0L);          // correlationId
        w.WriteLong(messageId);
        w.WriteInt(EtpMessageFlags.FinalPart);

        // Body: { channelIds: array<long> }
        w.WriteArrayStart(channelIds.Count);
        foreach (var id in channelIds)
            w.WriteLong(id);
        w.WriteArrayEnd();

        return w.ToArray();
    }
}
