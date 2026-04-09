using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// ETP Protocol 0 RequestSession message (messageType=1).
/// Reference: ETP v1.1 specification, Protocol 0 Core, section on session establishment.
/// The client sends this message immediately after the WebSocket connection is open,
/// declaring itself as a consumer and listing the protocols it wants to use.
/// </summary>
internal sealed class RequestSessionMessage
{
    public string ApplicationName { get; }
    public string ApplicationVersion { get; }
    public Guid ClientInstanceId { get; }
    public IReadOnlyList<SupportedProtocol> RequestedProtocols { get; }

    public RequestSessionMessage(
        string applicationName,
        string applicationVersion,
        Guid clientInstanceId,
        IReadOnlyList<SupportedProtocol> requestedProtocols)
    {
        ApplicationName = applicationName;
        ApplicationVersion = applicationVersion;
        ClientInstanceId = clientInstanceId;
        RequestedProtocols = requestedProtocols;
    }

    /// <summary>
    /// Encodes a complete binary frame (header + body) ready to send over WebSocket.
    /// </summary>
    public ReadOnlyMemory<byte> EncodeFrame(long messageId)
    {
        var w = new AvroWriter();

        // ── Header ────────────────────────────────────────────────────────────
        var header = new EtpMessageHeader(
            Protocol:     0,
            MessageType:  EtpMessageType.RequestSession,
            MessageId:    messageId,
            MessageFlags: EtpMessageFlags.FinalPart);
        header.WriteTo(w);

        // ── Body ──────────────────────────────────────────────────────────────
        w.WriteString(ApplicationName);
        w.WriteString(ApplicationVersion);

        // clientInstanceId: Avro fixed(16) — UUID bytes in standard .NET layout
        w.WriteFixed(ClientInstanceId.ToByteArray());

        // requestedProtocols: array of SupportedProtocol
        w.WriteArrayStart(RequestedProtocols.Count);
        foreach (var p in RequestedProtocols)
        {
            w.WriteInt(p.Protocol);
            // protocolVersion: Version record {major, minor, revision, patch}
            w.WriteInt(p.Version.Major);
            w.WriteInt(p.Version.Minor);
            w.WriteInt(p.Version.Revision);
            w.WriteInt(p.Version.Patch);
            w.WriteString(p.Role);
            // protocolCapabilities: empty map<string, DataValue>
            w.WriteMapStart(0);
            w.WriteMapEnd();
        }
        w.WriteArrayEnd();

        // supportedDataObjects: empty array
        w.WriteArrayStart(0);
        w.WriteArrayEnd();

        // supportedCompression: empty array<string>
        w.WriteArrayStart(0);
        w.WriteArrayEnd();

        // supportedFormats: ["xml"]
        w.WriteArrayStart(1);
        w.WriteString("xml");
        w.WriteArrayEnd();

        // currentDateTime and earliestReliableTime: Unix epoch microseconds
        long nowMicros = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
        w.WriteLong(nowMicros);
        w.WriteLong(nowMicros);

        // serverAuthorizationRequired: false
        w.WriteBool(false);

        return w.ToArray();
    }
}
