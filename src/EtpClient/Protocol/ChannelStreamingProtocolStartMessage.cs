namespace EtpClient.Protocol;

/// <summary>
/// Encodes ETP Protocol 1 Start messages (consumer -> producer) using Avro binary.
/// Body schema: { maxMessageRate: int, maxDataItems: int }
/// </summary>
internal static class ChannelStreamingProtocolStartMessage
{
    /// <summary>
    /// Encodes a Protocol 1 Start frame.
    /// </summary>
    public static ReadOnlyMemory<byte> EncodeBinaryFrame(int maxMessageRate, int maxDataItems, long messageId)
    {
        var writer = new AvroWriter();

        writer.WriteInt(EtpProtocol.ChannelStreaming);
        writer.WriteInt(EtpChannelStreamingMessageType.Start);
        writer.WriteLong(0L);
        writer.WriteLong(messageId);
        writer.WriteInt(EtpMessageFlags.FinalPart);

        writer.WriteInt(maxMessageRate);
        writer.WriteInt(maxDataItems);

        return writer.ToArray();
    }
}
