namespace EtpClient.Protocol;

/// <summary>ETP protocol number constants.</summary>
internal static class EtpProtocol
{
    public const int Core = 0;
    public const int ChannelStreaming = 1;
    public const int Discovery = 3;
}

/// <summary>ETP Protocol 1 (ChannelStreaming) message type constants.</summary>
internal static class EtpChannelStreamingMessageType
{
    /// <summary>Start — consumer → producer. Signals channel streaming session start.</summary>
    public const int Start = 0;

    /// <summary>ChannelDescribe — consumer → producer. Requests metadata for channel URIs.</summary>
    public const int ChannelDescribe = 1;

    /// <summary>ChannelMetadata — producer → consumer. Returns channel definitions (multipart).</summary>
    public const int ChannelMetadata = 2;

    /// <summary>ChannelData — producer → consumer. Live or range data values.</summary>
    public const int ChannelData = 3;

    /// <summary>ChannelStreamingStart — consumer → producer. Subscribes to live channel data.</summary>
    public const int ChannelStreamingStart = 4;

    /// <summary>ChannelStreamingStop — consumer → producer. Cancels live subscriptions.</summary>
    public const int ChannelStreamingStop = 5;

    /// <summary>ChannelDataChange — producer → consumer. Notifies of historic data edits.</summary>
    public const int ChannelDataChange = 6;

    /// <summary>ChannelRemove — producer → consumer. Signals a channel is being removed.</summary>
    public const int ChannelRemove = 8;

    /// <summary>ChannelRangeRequest — consumer → producer. Requests historic data range.</summary>
    public const int ChannelRangeRequest = 9;

    /// <summary>ChannelStatusChange — producer → consumer. Notifies of channel status change.</summary>
    public const int ChannelStatusChange = 10;
}

/// <summary>ETP Protocol 3 (Discovery) message type constants.</summary>
internal static class EtpDiscoveryMessageType
{
    /// <summary>GetResources request (customer → store).</summary>
    public const int GetResources = 1;

    /// <summary>GetResourcesResponse (store → customer, multipart one-resource-per-message).</summary>
    public const int GetResourcesResponse = 2;
}

/// <summary>ETP Protocol 0 (Core) message type constants.</summary>
internal static class EtpMessageType
{
    public const int RequestSession    = 1;
    public const int OpenSession       = 2;
    public const int CloseSession      = 5;
    public const int ProtocolException = 1000;
    public const int Acknowledge       = 1001;
}

/// <summary>ETP message flag bit values.</summary>
internal static class EtpMessageFlags
{
    /// <summary>Set on the last (or only) part of a multi-part or single-part message.</summary>
    public const int FinalPart = 0x02;
}

/// <summary>
/// Binary framing header present at the start of every ETP message.
/// Reference: ETP v1.1 specification, Protocol 0 (Core) framing.
/// Fields: protocol (int), messageType (int), correlationId (long),
/// messageId (long), messageFlags (int).
/// </summary>
internal readonly record struct EtpMessageHeader(
    int Protocol,
    int MessageType,
    long CorrelationId,
    long MessageId,
    int MessageFlags)
{
    public void WriteTo(AvroWriter writer)
    {
        writer.WriteInt(Protocol);
        writer.WriteInt(MessageType);
        writer.WriteLong(CorrelationId);
        writer.WriteLong(MessageId);
        writer.WriteInt(MessageFlags);
    }

    public static EtpMessageHeader ReadFrom(AvroReader reader) =>
        new(
            Protocol:     reader.ReadInt(),
            MessageType:  reader.ReadInt(),
            CorrelationId: reader.ReadLong(),
            MessageId:    reader.ReadLong(),
            MessageFlags: reader.ReadInt());
}
