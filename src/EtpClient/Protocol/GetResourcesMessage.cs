namespace EtpClient.Protocol;

/// <summary>
/// ETP Protocol 3 GetResources message (messageType=1).
/// Sent from the customer (client) to the store (server) to enumerate child resources for a URI.
/// Reference: ETP v1.1 specification §3.4.4.2.
/// Schema: { "uri": "string" } — a single URI field.
/// </summary>
internal static class GetResourcesMessage
{
    /// <summary>
    /// Encodes a complete binary GetResources frame (header + body) ready to send over WebSocket.
    /// </summary>
    /// <param name="uri">The URI whose children should be enumerated.</param>
    /// <param name="messageId">The caller-assigned message identifier.</param>
    public static ReadOnlyMemory<byte> EncodeBinaryFrame(string uri, long messageId)
    {
        var w = new AvroWriter();

        var header = new EtpMessageHeader(
            Protocol:     EtpProtocol.Discovery,
            MessageType:  EtpDiscoveryMessageType.GetResources,
            CorrelationId: 0,
            MessageId:    messageId,
            MessageFlags: EtpMessageFlags.FinalPart);
        header.WriteTo(w);

        w.WriteString(uri);

        return w.ToArray();
    }
}
