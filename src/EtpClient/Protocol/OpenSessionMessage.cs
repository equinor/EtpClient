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

        // ── applicationName, applicationVersion, sessionId ───────────────────
        var appName = r.ReadString();
        var appVersion = r.ReadString();
        var sessionId = r.ReadString();
        var serverId = Guid.TryParse(sessionId, out var parsedSessionId)
            ? parsedSessionId
            : Guid.Empty;

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

        // ── supportedObjects: array<string> — skip for the minimal client API ─
        while ((blockCount = r.ReadBlockCount()) > 0)
        {
            for (var i = 0; i < blockCount; i++)
                r.SkipString();
        }

        var session = new NegotiatedSessionInfo
        {
            ServerInstanceId       = serverId,
            SupportedProtocols     = protocols,
            SupportedCompression   = string.Empty,
            SupportedFormats       = [],
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
