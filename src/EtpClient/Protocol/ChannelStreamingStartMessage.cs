using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Encodes ETP Protocol 1 ChannelStreamingStart messages (consumer → producer) using Avro binary.
/// Body schema: { channels: array&lt;ChannelStreamingInfo&gt; }
/// </summary>
internal static class ChannelStreamingStartMessage
{
    /// <summary>
    /// Encodes a ChannelStreamingStart frame using Avro binary encoding.
    /// ChannelStreamingInfo fields: channelId(long), startIndex(StreamingStartIndex union), receiveChangeNotification(bool)
    /// StreamingStartIndex union: 0=null(latestValue), 1=int(indexCount), 2=long(indexValue)
    /// </summary>
    public static ReadOnlyMemory<byte> EncodeBinaryFrame(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions, long messageId)
    {
        var w = new AvroWriter();

        // Header
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelStreamingStart);
        w.WriteLong(0L);          // correlationId
        w.WriteLong(messageId);
        w.WriteInt(EtpMessageFlags.FinalPart);

        // Body: { channels: array<ChannelStreamingInfo> }
        w.WriteArrayStart(subscriptions.Count);
        foreach (var sub in subscriptions)
        {
            w.WriteLong(sub.ChannelId);

            // startIndex union: 0=null/latestValue, 1=int/indexCount, 2=long/indexValue
            if (sub.StartLatest)
            {
                w.WriteLong(0L); // null / start at latest value
            }
            else
            {
                w.WriteLong(2L); // long/indexValue
                w.WriteLong(sub.StartIndexValue ?? 0L);
            }

            w.WriteBool(sub.ReceiveChangeNotifications);
        }
        w.WriteArrayEnd();

        return w.ToArray();
    }
}
