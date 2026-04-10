using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EtpClient.Models;
using EtpClient.Protocol;

namespace EtpClient.UnitTests.Protocol;

/// <summary>
/// Unit tests for binary and JSON codec discovery methods:
/// <see cref="IEtpSessionCodec.EncodeGetResources"/>,
/// <see cref="IEtpSessionCodec.DecodeGetResourcesResponse"/>, and
/// <see cref="IEtpSessionCodec.DecodeHeader"/>.
/// T002/T009 [US1, US2]: Codec round-trip correctness for discovery messages.
/// </summary>
public sealed class DiscoverySessionCodecTests
{
    // ── Binary: EncodeGetResources ────────────────────────────────────────────

    [Fact]
    public void Binary_EncodeGetResources_HeaderHasDiscoveryProtocolAndType()
    {
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeGetResources("eml://", messageId: 2L);

        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.Discovery, header.Protocol);
        Assert.Equal(EtpDiscoveryMessageType.GetResources, header.MessageType);
        Assert.Equal(2L, header.MessageId);
        Assert.NotEqual(0, header.MessageFlags & EtpMessageFlags.FinalPart);
    }

    [Fact]
    public void Binary_EncodeGetResources_UriIsEncodedInBody()
    {
        const string uri = "eml://witsml20/well";
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeGetResources(uri, messageId: 3L);

        // Skip header (5 varint/long fields) then read uri
        var r = new AvroReader(frame);
        _ = EtpMessageHeader.ReadFrom(r);
        var decodedUri = r.ReadString();

        Assert.Equal(uri, decodedUri);
    }

    // ── Binary: DecodeGetResourcesResponse ───────────────────────────────────

    [Fact]
    public void Binary_DecodeGetResourcesResponse_PopulatesAllFields()
    {
        var expected = CreateSampleResource(uuid: "sample-uuid-1234");
        var frame = BuildBinaryResponseFrame(expected, messageId: 2L, finalPart: true);
        var codec = new BinaryEtpSessionCodec();

        var (header, resource) = codec.DecodeGetResourcesResponse(frame);

        Assert.Equal(EtpProtocol.Discovery, header.Protocol);
        Assert.Equal(EtpDiscoveryMessageType.GetResourcesResponse, header.MessageType);
        Assert.Equal(EtpMessageFlags.FinalPart, header.MessageFlags & EtpMessageFlags.FinalPart);

        Assert.Equal(expected.Uri, resource.Uri);
        Assert.Equal(expected.ContentType, resource.ContentType);
        Assert.Equal(expected.Name, resource.Name);
        Assert.Equal(expected.ChannelSubscribable, resource.ChannelSubscribable);
        Assert.Equal(expected.ResourceType, resource.ResourceType);
        Assert.Equal(expected.HasChildren, resource.HasChildren);
        Assert.Equal(expected.Uuid, resource.Uuid);
        Assert.Equal(expected.LastChanged, resource.LastChanged);
        Assert.Equal(expected.ObjectNotifiable, resource.ObjectNotifiable);
    }

    [Fact]
    public void Binary_DecodeGetResourcesResponse_NullUuid_IsNull()
    {
        var resource = CreateSampleResource(uuid: null);
        var frame = BuildBinaryResponseFrame(resource, messageId: 2L, finalPart: true);
        var codec = new BinaryEtpSessionCodec();

        var (_, decoded) = codec.DecodeGetResourcesResponse(frame);

        Assert.Null(decoded.Uuid);
    }

    [Fact]
    public void Binary_DecodeGetResourcesResponse_NonNullUuid_IsString()
    {
        const string expectedUuid = "6ba7b810-9dad-11d1-80b4-00c04fd430c8";
        var resource = CreateSampleResource(uuid: expectedUuid);
        var frame = BuildBinaryResponseFrame(resource, messageId: 2L, finalPart: true);
        var codec = new BinaryEtpSessionCodec();

        var (_, decoded) = codec.DecodeGetResourcesResponse(frame);

        Assert.Equal(expectedUuid, decoded.Uuid);
    }

    [Fact]
    public void Binary_DecodeGetResourcesResponse_WithCustomData_PopulatesMap()
    {
        var resource = CreateSampleResource(uuid: null, customData: new Dictionary<string, string>
        {
            { "depth", "3000m" },
            { "operator", "TestCorp" },
        });
        var frame = BuildBinaryResponseFrame(resource, messageId: 2L, finalPart: true);
        var codec = new BinaryEtpSessionCodec();

        var (_, decoded) = codec.DecodeGetResourcesResponse(frame);

        Assert.Equal(2, decoded.CustomData.Count);
        Assert.Equal("3000m", decoded.CustomData["depth"]);
        Assert.Equal("TestCorp", decoded.CustomData["operator"]);
    }

    [Fact]
    public void Binary_DecodeGetResourcesResponse_FinalPartFlag_IsCorrectlyDetected()
    {
        var resource = CreateSampleResource(uuid: null);

        var finalFrame = BuildBinaryResponseFrame(resource, messageId: 2L, finalPart: true);
        var nonFinalFrame = BuildBinaryResponseFrame(resource, messageId: 2L, finalPart: false);

        var codec = new BinaryEtpSessionCodec();

        var (finalHeader, _) = codec.DecodeGetResourcesResponse(finalFrame);
        var (nonFinalHeader, _) = codec.DecodeGetResourcesResponse(nonFinalFrame);

        Assert.NotEqual(0, finalHeader.MessageFlags & EtpMessageFlags.FinalPart);
        Assert.Equal(0, nonFinalHeader.MessageFlags & EtpMessageFlags.FinalPart);
    }

    // ── Binary: DecodeHeader ─────────────────────────────────────────────────

    [Fact]
    public void Binary_DecodeHeader_FromGetResourcesFrame_ReturnsCorrectProtocolAndType()
    {
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeGetResources("eml://", messageId: 5L);

        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.Discovery, header.Protocol);
        Assert.Equal(EtpDiscoveryMessageType.GetResources, header.MessageType);
    }

    // ── JSON: EncodeGetResources ──────────────────────────────────────────────

    [Fact]
    public void Json_EncodeGetResources_ProducesTwoElementJsonArray()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeGetResources("eml://", messageId: 2L);

        // Must be a text frame (checked via FrameType)
        Assert.Equal(WebSocketMessageType.Text, codec.FrameType);

        var json = Encoding.UTF8.GetString(frame.Span);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());
    }

    [Fact]
    public void Json_EncodeGetResources_HeaderHasDiscoveryProtocolAndType()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeGetResources("eml://witsml20/well", messageId: 3L);
        var json = Encoding.UTF8.GetString(frame.Span);

        using var doc = JsonDocument.Parse(json);
        var header = doc.RootElement[0];

        Assert.Equal(EtpProtocol.Discovery, header.GetProperty("protocol").GetInt32());
        Assert.Equal(EtpDiscoveryMessageType.GetResources, header.GetProperty("messageType").GetInt32());
        Assert.Equal(3L, header.GetProperty("messageId").GetInt64());
    }

    [Fact]
    public void Json_EncodeGetResources_BodyHasUriField()
    {
        const string uri = "eml://witsml20/well(abc123)";
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeGetResources(uri, messageId: 2L);
        var json = Encoding.UTF8.GetString(frame.Span);

        using var doc = JsonDocument.Parse(json);
        var body = doc.RootElement[1];

        Assert.Equal(uri, body.GetProperty("uri").GetString());
    }

    // ── JSON: DecodeGetResourcesResponse ─────────────────────────────────────

    [Fact]
    public void Json_DecodeGetResourcesResponse_PopulatesAllFields()
    {
        const string resourceUri = "eml://witsml20/well(test-001)";
        var frame = BuildJsonResponseFrame(
            resourceUri: resourceUri,
            contentType: "application/x-witsml+xml;version=2.0",
            name: "Test Well",
            channelSubscribable: false,
            resourceType: "DataObject",
            hasChildren: 3,
            uuid: "test-uuid-001",
            lastChanged: 1_700_000_000L,
            objectNotifiable: true,
            messageId: 2L,
            finalPart: true);

        var codec = new JsonEtpSessionCodec();
        var (header, resource) = codec.DecodeGetResourcesResponse(frame);

        Assert.Equal(EtpProtocol.Discovery, header.Protocol);
        Assert.Equal(EtpDiscoveryMessageType.GetResourcesResponse, header.MessageType);
        Assert.Equal(resourceUri, resource.Uri);
        Assert.Equal("Test Well", resource.Name);
        Assert.Equal("DataObject", resource.ResourceType);
        Assert.Equal(3, resource.HasChildren);
        Assert.Equal("test-uuid-001", resource.Uuid);
        Assert.Equal(1_700_000_000L, resource.LastChanged);
        Assert.True(resource.ObjectNotifiable);
    }

    [Fact]
    public void Json_DecodeGetResourcesResponse_NullUuid_IsNull()
    {
        var frame = BuildJsonResponseFrame(
            resourceUri: "eml://",
            contentType: "",
            name: "Root",
            channelSubscribable: false,
            resourceType: "Folder",
            hasChildren: 1,
            uuid: null,
            lastChanged: 0L,
            objectNotifiable: false,
            messageId: 2L,
            finalPart: true);

        var codec = new JsonEtpSessionCodec();
        var (_, resource) = codec.DecodeGetResourcesResponse(frame);

        Assert.Null(resource.Uuid);
    }

    // ── JSON: DecodeHeader ────────────────────────────────────────────────────

    [Fact]
    public void Json_DecodeHeader_FromGetResourcesFrame_ReturnsCorrectProtocolAndType()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeGetResources("eml://", messageId: 7L);

        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.Discovery, header.Protocol);
        Assert.Equal(EtpDiscoveryMessageType.GetResources, header.MessageType);
        Assert.Equal(7L, header.MessageId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static DiscoveredResource CreateSampleResource(
        string? uuid,
        IDictionary<string, string>? customData = null) =>
        new()
        {
            Uri = "eml://witsml20/well(sample-001)",
            ContentType = "application/x-witsml+xml;version=2.0",
            Name = "Sample Well",
            ChannelSubscribable = true,
            CustomData = (IReadOnlyDictionary<string, string>?)customData
                         ?? new Dictionary<string, string>(),
            ResourceType = "DataObject",
            HasChildren = 2,
            Uuid = uuid,
            LastChanged = 1_690_000_000L,
            ObjectNotifiable = false,
        };

    /// <summary>Builds a binary GetResourcesResponse frame for one resource.</summary>
    internal static ReadOnlyMemory<byte> BuildBinaryResponseFrame(
        DiscoveredResource resource, long messageId, bool finalPart)
    {
        var w = new AvroWriter();
        // Header: protocol=3, messageType=2, correlationId=0, messageId, flags
        w.WriteInt(EtpProtocol.Discovery);
        w.WriteInt(EtpDiscoveryMessageType.GetResourcesResponse);
        w.WriteLong(0L);
        w.WriteLong(messageId);
        w.WriteInt(finalPart ? EtpMessageFlags.FinalPart : 0);

        // Body: Resource record in ETP v1.1 Avro schema field order
        w.WriteString(resource.Uri);
        w.WriteString(resource.ContentType);
        w.WriteString(resource.Name);
        w.WriteBool(resource.ChannelSubscribable);

        // customData map
        if (resource.CustomData.Count == 0)
        {
            w.WriteMapStart(0);
        }
        else
        {
            w.WriteMapStart(resource.CustomData.Count);
            foreach (var kv in resource.CustomData)
            {
                w.WriteString(kv.Key);
                w.WriteString(kv.Value);
            }
            w.WriteMapEnd();
        }

        w.WriteString(resource.ResourceType);
        w.WriteInt(resource.HasChildren);

        // uuid union ["null", "string"]
        if (resource.Uuid is null)
        {
            w.WriteLong(0L); // null discriminator
        }
        else
        {
            w.WriteLong(1L); // string discriminator
            w.WriteString(resource.Uuid);
        }

        w.WriteLong(resource.LastChanged);
        w.WriteBool(resource.ObjectNotifiable);

        return w.ToArray();
    }

    /// <summary>Builds a JSON GetResourcesResponse frame for one resource.</summary>
    private static ReadOnlyMemory<byte> BuildJsonResponseFrame(
        string resourceUri,
        string contentType,
        string name,
        bool channelSubscribable,
        string resourceType,
        int hasChildren,
        string? uuid,
        long lastChanged,
        bool objectNotifiable,
        long messageId,
        bool finalPart)
    {
        var uuidValue = uuid is null
            ? "null"
            : $"{{\"string\":\"{uuid}\"}}";

        var json = $$"""
            [
              {
                "protocol": {{EtpProtocol.Discovery}},
                "messageType": {{EtpDiscoveryMessageType.GetResourcesResponse}},
                "correlationId": 0,
                "messageId": {{messageId}},
                "messageFlags": {{(finalPart ? EtpMessageFlags.FinalPart : 0)}}
              },
              {
                "resource": {
                  "uri": "{{resourceUri}}",
                  "contentType": "{{contentType}}",
                  "name": "{{name}}",
                  "channelSubscribable": {{(channelSubscribable ? "true" : "false")}},
                  "customData": {},
                  "resourceType": "{{resourceType}}",
                  "hasChildren": {{hasChildren}},
                  "uuid": {{uuidValue}},
                  "lastChanged": {{lastChanged}},
                  "objectNotifiable": {{(objectNotifiable ? "true" : "false")}}
                }
              }
            ]
            """;

        return Encoding.UTF8.GetBytes(json);
    }
}
