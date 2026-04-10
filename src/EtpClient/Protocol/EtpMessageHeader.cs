namespace EtpClient.Protocol;

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
