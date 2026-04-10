using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// ETP session codec using Avro JSON encoding over WebSocket text frames.
/// Messages are encoded as a two-element JSON array: [headerObject, bodyObject].
/// Both elements follow Avro JSON encoding conventions for their respective schemas.
/// </summary>
internal sealed class JsonEtpSessionCodec : IEtpSessionCodec
{
    public EtpMessageEncoding Encoding => EtpMessageEncoding.Json;
    public WebSocketMessageType FrameType => WebSocketMessageType.Text;

    public ReadOnlyMemory<byte> EncodeRequestSession(RequestSessionMessage message, long messageId)
    {
        // Build the requested protocols array
        var protocolsArray = new JsonArray();
        foreach (var p in message.RequestedProtocols)
        {
            protocolsArray.Add(new JsonObject
            {
                ["protocol"] = p.Protocol,
                ["protocolVersion"] = new JsonObject
                {
                    ["major"] = p.Version.Major,
                    ["minor"] = p.Version.Minor,
                    ["revision"] = p.Version.Revision,
                    ["patch"] = p.Version.Patch,
                },
                ["role"] = p.Role,
                ["protocolCapabilities"] = new JsonObject(),
            });
        }

        var msg = new JsonArray
        {
            // Header
            new JsonObject
            {
                ["protocol"] = 0,
                ["messageType"] = EtpMessageType.RequestSession,
                ["correlationId"] = 0L,
                ["messageId"] = messageId,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            // Body
            new JsonObject
            {
                ["applicationName"] = message.ApplicationName,
                ["applicationVersion"] = message.ApplicationVersion,
                ["requestedProtocols"] = protocolsArray,
                ["supportedObjects"] = new JsonArray(),
            },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    public int PeekMessageType(ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;
        // root[0] is the header object
        return root[0].GetProperty("messageType").GetInt32();
    }

    public (EtpMessageHeader Header, NegotiatedSessionInfo Session) DecodeOpenSession(ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;

        var headerEl = root[0];
        var bodyEl = root[1];

        var header = ParseHeader(headerEl);

        var protocols = new List<SupportedProtocol>();
        if (bodyEl.TryGetProperty("supportedProtocols", out var protsEl))
        {
            foreach (var p in protsEl.EnumerateArray())
            {
                var proto = p.GetProperty("protocol").GetInt32();
                var verEl = p.GetProperty("protocolVersion");
                var version = new ProtocolVersion(
                    verEl.GetProperty("major").GetInt32(),
                    verEl.GetProperty("minor").GetInt32(),
                    verEl.GetProperty("revision").GetInt32(),
                    verEl.GetProperty("patch").GetInt32());
                var role = p.GetProperty("role").GetString() ?? string.Empty;
                protocols.Add(new SupportedProtocol(proto, version, role));
            }
        }

        var serverIdStr = bodyEl.GetProperty("sessionId").GetString() ?? Guid.Empty.ToString();
        if (!Guid.TryParse(serverIdStr, out var serverId))
            serverId = Guid.Empty;

        var session = new NegotiatedSessionInfo
        {
            ServerInstanceId = serverId,
            SupportedProtocols = protocols,
            SupportedCompression = string.Empty,
            SupportedFormats = [],
            ServerApplicationName = bodyEl.GetProperty("applicationName").GetString() ?? string.Empty,
            ServerApplicationVersion = bodyEl.GetProperty("applicationVersion").GetString() ?? string.Empty,
        };

        return (header, session);
    }

    public (EtpMessageHeader Header, int ErrorCode, string Message) DecodeProtocolException(ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;

        var header = ParseHeader(root[0]);
        var bodyEl = root[1];

        var errorCode = bodyEl.GetProperty("errorCode").GetInt32();
        var message = bodyEl.TryGetProperty("errorMessage", out var errorMessageEl)
            ? errorMessageEl.GetString() ?? string.Empty
            : bodyEl.TryGetProperty("message", out var legacyMessageEl)
                ? legacyMessageEl.GetString() ?? string.Empty
                : string.Empty;

        return (header, errorCode, message);
    }

    // ── Protocol 3 (Discovery) ───────────────────────────────────────────────

    public ReadOnlyMemory<byte> EncodeGetResources(string uri, long messageId)
    {
        var msg = new JsonArray
        {
            // Header
            new JsonObject
            {
                ["protocol"] = EtpProtocol.Discovery,
                ["messageType"] = EtpDiscoveryMessageType.GetResources,
                ["correlationId"] = 0L,
                ["messageId"] = messageId,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            // Body: { "uri": "..." }
            new JsonObject
            {
                ["uri"] = uri,
            },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    public (EtpMessageHeader Header, DiscoveredResource Resource) DecodeGetResourcesResponse(ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;

        var header = ParseHeader(root[0]);
        var bodyEl = root[1];
        var resourceEl = bodyEl.GetProperty("resource");

        var customData = new Dictionary<string, string>();
        if (resourceEl.TryGetProperty("customData", out var customDataEl))
        {
            foreach (var prop in customDataEl.EnumerateObject())
                customData[prop.Name] = prop.Value.GetString() ?? string.Empty;
        }

        // uuid is a nullable union in Avro JSON: null | { "string": "..." } | plain string
        string? uuid = null;
        if (resourceEl.TryGetProperty("uuid", out var uuidEl))
        {
            if (uuidEl.ValueKind == JsonValueKind.String)
                uuid = uuidEl.GetString();
            else if (uuidEl.ValueKind == JsonValueKind.Object &&
                     uuidEl.TryGetProperty("string", out var innerUuid))
                uuid = innerUuid.GetString();
        }

        var resource = new DiscoveredResource
        {
            Uri = resourceEl.GetProperty("uri").GetString() ?? string.Empty,
            ContentType = resourceEl.GetProperty("contentType").GetString() ?? string.Empty,
            Name = resourceEl.GetProperty("name").GetString() ?? string.Empty,
            ChannelSubscribable = resourceEl.GetProperty("channelSubscribable").GetBoolean(),
            CustomData = customData,
            ResourceType = resourceEl.GetProperty("resourceType").GetString() ?? string.Empty,
            HasChildren = resourceEl.GetProperty("hasChildren").GetInt32(),
            Uuid = uuid,
            LastChanged = resourceEl.TryGetProperty("lastChanged", out var lc) ? lc.GetInt64() : 0L,
            ObjectNotifiable = resourceEl.GetProperty("objectNotifiable").GetBoolean(),
        };

        return (header, resource);
    }

    // ── shared helpers ────────────────────────────────────────────────────────

    public EtpMessageHeader DecodeHeader(ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        return ParseHeader(doc.RootElement[0]);
    }

    private static EtpMessageHeader ParseHeader(JsonElement headerEl) =>
        new(
            Protocol: headerEl.GetProperty("protocol").GetInt32(),
            MessageType: headerEl.GetProperty("messageType").GetInt32(),
            CorrelationId: headerEl.TryGetProperty("correlationId", out var correlationIdEl)
                ? correlationIdEl.GetInt64()
                : 0L,
            MessageId: headerEl.GetProperty("messageId").GetInt64(),
            MessageFlags: headerEl.GetProperty("messageFlags").GetInt32());
}
