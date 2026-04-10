using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Encodes ETP Protocol 1 ChannelDescribe messages (consumer → producer) using Avro binary.
/// </summary>
internal static class ChannelDescribeMessage
{
    /// <summary>
    /// Encodes a ChannelDescribe frame using Avro binary encoding.
    /// Body schema: { uris: array&lt;string&gt; }
    /// </summary>
    public static ReadOnlyMemory<byte> EncodeBinaryFrame(
        IReadOnlyList<string> uris, long messageId)
    {
        var w = new AvroWriter();

        // Header
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelDescribe);
        w.WriteLong(0L);                        // correlationId = 0 for new requests
        w.WriteLong(messageId);
        w.WriteInt(EtpMessageFlags.FinalPart);

        // Body: { uris: array<string> }
        w.WriteArrayStart(uris.Count);
        foreach (var uri in uris)
            w.WriteString(uri);
        w.WriteArrayEnd();

        return w.ToArray();
    }
}
