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
            CorrelationId: 0,
            MessageId:    messageId,
            MessageFlags: EtpMessageFlags.FinalPart);
        header.WriteTo(w);

        // ── Body ──────────────────────────────────────────────────────────────
        w.WriteString(ApplicationName);
        w.WriteString(ApplicationVersion);

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

        // supportedObjects: empty array for a minimal client role handshake
        w.WriteArrayStart(0);
        w.WriteArrayEnd();

        return w.ToArray();
    }
}
