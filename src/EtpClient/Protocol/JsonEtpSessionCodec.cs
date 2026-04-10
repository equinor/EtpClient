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

    // ── Protocol 1 (ChannelStreaming) ────────────────────────────────────────

    public ReadOnlyMemory<byte> EncodeChannelDescribe(IReadOnlyList<string> uris, long messageId)
    {
        var arr = new JsonArray();
        foreach (var u in uris) arr.Add(u);

        var msg = new JsonArray
        {
            new JsonObject
            {
                ["protocol"] = EtpProtocol.ChannelStreaming,
                ["messageType"] = EtpChannelStreamingMessageType.ChannelDescribe,
                ["correlationId"] = 0L,
                ["messageId"] = messageId,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            new JsonObject { ["uris"] = arr },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    public (EtpMessageHeader Header, IReadOnlyList<Models.ChannelDefinition> Channels) DecodeChannelMetadata(ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;
        var header = ParseHeader(root[0]);
        var bodyEl = root[1];

        // Body: { "channels": [ channelMetadataRecord, ... ] }
        var channelsEl = bodyEl.GetProperty("channels");
        var channels = new List<Models.ChannelDefinition>();
        foreach (var ch in channelsEl.EnumerateArray())
            channels.Add(DecodeChannelDefinitionFromJson(ch));
        return (header, channels);
    }

    public ReadOnlyMemory<byte> EncodeChannelStreamingStart(
        IReadOnlyList<Models.ChannelSubscriptionInfo> subscriptions, long messageId)
    {
        var arr = new JsonArray();
        foreach (var s in subscriptions)
        {
            JsonNode startIndex;
            if (s.StartLatest)
                startIndex = JsonValue.Create((object?)null)!;
            else
                startIndex = new JsonObject { ["item"] = s.StartIndexValue };

            arr.Add(new JsonObject
            {
                ["channelId"] = s.ChannelId,
                ["startIndex"] = startIndex,
                ["receiveChangeNotification"] = s.ReceiveChangeNotifications,
            });
        }

        var msg = new JsonArray
        {
            new JsonObject
            {
                ["protocol"] = EtpProtocol.ChannelStreaming,
                ["messageType"] = EtpChannelStreamingMessageType.ChannelStreamingStart,
                ["correlationId"] = 0L,
                ["messageId"] = messageId,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            new JsonObject { ["channels"] = arr },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    public ReadOnlyMemory<byte> EncodeChannelStreamingStop(
        IReadOnlyList<long> channelIds, long messageId)
    {
        var arr = new JsonArray();
        foreach (var id in channelIds) arr.Add(id);

        var msg = new JsonArray
        {
            new JsonObject
            {
                ["protocol"] = EtpProtocol.ChannelStreaming,
                ["messageType"] = EtpChannelStreamingMessageType.ChannelStreamingStop,
                ["correlationId"] = 0L,
                ["messageId"] = messageId,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            new JsonObject { ["channelIds"] = arr },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    public (EtpMessageHeader Header, IReadOnlyList<Models.ChannelDataItem> Items) DecodeChannelData(
        ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;
        var header = ParseHeader(root[0]);
        var channelDataEl = root[1].GetProperty("data");
        var items = new List<Models.ChannelDataItem>();

        foreach (var itemEl in channelDataEl.EnumerateArray())
        {
            var indexesEl = itemEl.GetProperty("indexes");
            var indexes = new List<long>();
            foreach (var ix in indexesEl.EnumerateArray())
                indexes.Add(ix.GetInt64());

            var channelId = itemEl.GetProperty("channelId").GetInt64();

            object? value = null;
            if (itemEl.TryGetProperty("value", out var valueEl) &&
                valueEl.ValueKind != JsonValueKind.Null)
            {
                if (valueEl.ValueKind == JsonValueKind.Object)
                {
                    // Avro JSON union: { "double": 1.23 } etc.
                    if (valueEl.TryGetProperty("double", out var dv)) value = dv.GetDouble();
                    else if (valueEl.TryGetProperty("float", out var fv)) value = (float)fv.GetSingle();
                    else if (valueEl.TryGetProperty("int", out var iv)) value = iv.GetInt32();
                    else if (valueEl.TryGetProperty("long", out var lv)) value = lv.GetInt64();
                    else if (valueEl.TryGetProperty("string", out var sv)) value = sv.GetString();
                    else if (valueEl.TryGetProperty("boolean", out var bv)) value = bv.GetBoolean();
                }
                else if (valueEl.ValueKind == JsonValueKind.Number)
                {
                    value = valueEl.GetDouble();
                }
                else if (valueEl.ValueKind == JsonValueKind.String)
                {
                    value = valueEl.GetString();
                }
                else if (valueEl.ValueKind == JsonValueKind.True ||
                         valueEl.ValueKind == JsonValueKind.False)
                {
                    value = valueEl.GetBoolean();
                }
            }

            items.Add(new Models.ChannelDataItem
            {
                Indexes = indexes,
                ChannelId = channelId,
                Value = value,
            });
        }

        return (header, items);
    }

    public (EtpMessageHeader Header, long ChannelId, long StartIndex, long EndIndex) DecodeChannelDataChange(
        ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;
        var header = ParseHeader(root[0]);
        var bodyEl = root[1];
        return (header,
            bodyEl.GetProperty("channelId").GetInt64(),
            bodyEl.GetProperty("startIndex").GetInt64(),
            bodyEl.GetProperty("endIndex").GetInt64());
    }

    public (EtpMessageHeader Header, long ChannelId, string NewStatus) DecodeChannelStatusChange(
        ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;
        var header = ParseHeader(root[0]);
        var bodyEl = root[1];
        var channelId = bodyEl.GetProperty("channelId").GetInt64();
        var status = bodyEl.GetProperty("status").GetString() ?? string.Empty;
        return (header, channelId, status);
    }

    public (EtpMessageHeader Header, long ChannelId, string? Reason) DecodeChannelRemove(
        ReadOnlyMemory<byte> frame)
    {
        using var doc = JsonDocument.Parse(frame);
        var root = doc.RootElement;
        var header = ParseHeader(root[0]);
        var bodyEl = root[1];
        var channelId = bodyEl.GetProperty("channelId").GetInt64();
        string? reason = null;
        if (bodyEl.TryGetProperty("removeReason", out var reasonEl) &&
            reasonEl.ValueKind == JsonValueKind.String)
            reason = reasonEl.GetString();
        return (header, channelId, reason);
    }

    public ReadOnlyMemory<byte> EncodeChannelRangeRequest(
        IReadOnlyList<Models.ChannelRangeInfoWire> ranges, long messageId)
    {
        var arr = new JsonArray();
        foreach (var r in ranges)
        {
            var ids = new JsonArray();
            foreach (var id in r.ChannelIds) ids.Add(id);
            arr.Add(new JsonObject
            {
                ["channelId"] = ids,
                ["startIndex"] = r.StartIndex,
                ["endIndex"] = r.EndIndex,
            });
        }

        var msg = new JsonArray
        {
            new JsonObject
            {
                ["protocol"] = EtpProtocol.ChannelStreaming,
                ["messageType"] = EtpChannelStreamingMessageType.ChannelRangeRequest,
                ["correlationId"] = 0L,
                ["messageId"] = messageId,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            new JsonObject { ["channelRanges"] = arr },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    // ── JSON decode helpers ─────────────────────────────────────────────

    private static Models.ChannelDefinition DecodeChannelDefinitionFromJson(JsonElement ch)
    {
        // Read primary index metadata from indexes array (first entry)
        var indexType = "Time";
        var indexUom = string.Empty;
        var indexDirection = "Increasing";
        if (ch.TryGetProperty("indexes", out var indexesEl) &&
            indexesEl.GetArrayLength() > 0)
        {
            var idx = indexesEl[0];
            indexType = ReadEnumString(idx, "indexType",
                new[] { "Time", "Depth" }, defaultValue: "Time");
            indexUom = idx.TryGetProperty("uom", out var uomEl)
                ? uomEl.GetString() ?? string.Empty : string.Empty;
            indexDirection = ReadEnumString(idx, "direction",
                new[] { "Increasing", "Decreasing" }, defaultValue: "Increasing");
        }

        long? startIndex = null;
        if (ch.TryGetProperty("startIndex", out var siEl) &&
            siEl.ValueKind != JsonValueKind.Null)
            startIndex = siEl.GetInt64();

        long? endIndex = null;
        if (ch.TryGetProperty("endIndex", out var eiEl) &&
            eiEl.ValueKind != JsonValueKind.Null)
            endIndex = eiEl.GetInt64();

        string? uuid = null;
        if (ch.TryGetProperty("uuid", out var uuidEl) &&
            uuidEl.ValueKind == JsonValueKind.String)
            uuid = uuidEl.GetString();

        var customData = new Dictionary<string, string>();
        if (ch.TryGetProperty("customData", out var customDataEl))
            foreach (var p in customDataEl.EnumerateObject())
                customData[p.Name] = p.Value.GetString() ?? string.Empty;

        return new Models.ChannelDefinition
        {
            ChannelUri = ch.GetProperty("channelUri").GetString() ?? string.Empty,
            ChannelId = ch.GetProperty("channelId").GetInt64(),
            ChannelName = ch.GetProperty("channelName").GetString() ?? string.Empty,
            DataType = ch.GetProperty("dataType").GetString() ?? string.Empty,
            Uom = ch.GetProperty("uom").GetString() ?? string.Empty,
            IndexType = indexType,
            IndexUom = indexUom,
            IndexDirection = indexDirection,
            StartIndex = startIndex,
            EndIndex = endIndex,
            Description = ch.TryGetProperty("description", out var descEl)
                ? descEl.GetString() ?? string.Empty : string.Empty,
            Status = ReadEnumString(ch, "status",
                new[] { "Active", "Inactive", "Closed" }, defaultValue: "Active"),
            ContentType = ch.TryGetProperty("contentType", out var ctEl) &&
                ctEl.ValueKind == JsonValueKind.String
                ? ctEl.GetString() ?? string.Empty : string.Empty,
            Source = ch.TryGetProperty("source", out var srcEl)
                ? srcEl.GetString() ?? string.Empty : string.Empty,
            MeasureClass = ch.TryGetProperty("measureClass", out var mcEl)
                ? mcEl.GetString() ?? string.Empty : string.Empty,
            Uuid = uuid,
            CustomData = customData,
        };
    }

    private static string ReadEnumString(
        JsonElement el, string propertyName, string[] values, string defaultValue)
    {
        if (!el.TryGetProperty(propertyName, out var prop))
            return defaultValue;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var idx))
            return idx >= 0 && idx < values.Length ? values[idx] : defaultValue;
        return prop.GetString() ?? defaultValue;
    }
}
