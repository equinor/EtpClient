using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// ETP Protocol 0 OpenSession message (messageType=2).
/// The server sends this message in response to RequestSession when the session is accepted.
/// Reference: ETP v1.1 specification, Protocol 0 Core, session establishment.
/// </summary>
internal static class OpenSessionMessage
{
    /// <summary>
    /// Decodes a full binary frame (header + body) from <paramref name="frame"/>.
    /// </summary>
    /// <returns>The decoded header and the populated <see cref="NegotiatedSessionInfo"/>.</returns>
    public static (EtpMessageHeader Header, NegotiatedSessionInfo Session) DecodeFrame(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);

        // ── applicationName, applicationVersion ───────────────────────────────
        var appName = r.ReadString();
        var appVersion = r.ReadString();

        // ── serverInstanceId: fixed(16) bytes ─────────────────────────────────
        var idBytes = r.ReadFixed(16);
        var serverId = new Guid(idBytes);

        // ── supportedProtocols: array of SupportedProtocol ────────────────────
        var protocols = new List<SupportedProtocol>();
        long blockCount;
        while ((blockCount = r.ReadBlockCount()) > 0)
        {
            for (var i = 0; i < blockCount; i++)
            {
                var proto   = r.ReadInt();
                var major   = r.ReadInt();
                var minor   = r.ReadInt();
                var rev     = r.ReadInt();
                var patch   = r.ReadInt();
                var role    = r.ReadString();
                r.SkipStringDataValueMap(); // protocolCapabilities — not needed for session info
                protocols.Add(new SupportedProtocol(proto, new ProtocolVersion(major, minor, rev, patch), role));
            }
        }

        // ── supportedDataObjects: array — skip ────────────────────────────────
        while ((blockCount = r.ReadBlockCount()) > 0)
        {
            for (var i = 0; i < blockCount; i++)
            {
                r.SkipString();            // qualifiedType
                r.SkipStringDataValueMap(); // dataObjectCapabilities
            }
        }

        // ── supportedCompression: string ──────────────────────────────────────
        var compression = r.ReadString();

        // ── supportedFormats: array<string> ──────────────────────────────────
        var formats = new List<string>();
        while ((blockCount = r.ReadBlockCount()) > 0)
        {
            for (var i = 0; i < blockCount; i++)
                formats.Add(r.ReadString());
        }

        // ── currentDateTime, earliestReliableTime: long — skip ────────────────
        r.SkipLong();
        r.SkipLong();

        // ── endpointCapabilities: map<string, DataValue> — skip ───────────────
        r.SkipStringDataValueMap();

        var session = new NegotiatedSessionInfo
        {
            ServerInstanceId       = serverId,
            SupportedProtocols     = protocols,
            SupportedCompression   = compression,
            SupportedFormats       = formats,
            ServerApplicationName  = appName,
            ServerApplicationVersion = appVersion,
        };

        return (header, session);
    }
}

/// <summary>
/// ETP Protocol 0 ProtocolException message (messageType=1000).
/// The server sends this to reject or report a protocol-level error.
/// </summary>
internal static class ProtocolExceptionMessage
{
    /// <summary>
    /// Decodes a full binary ProtocolException frame.
    /// </summary>
    public static (EtpMessageHeader Header, int ErrorCode, string Message) DecodeFrame(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);
        var errorCode = r.ReadInt();
        var message   = r.ReadString();
        return (header, errorCode, message);
    }
}
