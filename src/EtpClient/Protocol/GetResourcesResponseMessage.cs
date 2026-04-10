using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// ETP Protocol 3 GetResourcesResponse message (messageType=2).
/// The store sends one message per resource in response to a GetResources request.
/// The final (or only) message has the FinalPart flag set in the header.
/// Reference: ETP v1.1 specification §3.4.4.1.
/// Schema: { "resource": Resource } — the <see cref="DiscoveredResource"/> model.
/// </summary>
internal static class GetResourcesResponseMessage
{
    /// <summary>
    /// Decodes a full binary frame (header + single resource body).
    /// </summary>
    /// <param name="frame">Raw WebSocket binary frame.</param>
    /// <returns>The decoded header and the populated <see cref="DiscoveredResource"/>.</returns>
    public static (EtpMessageHeader Header, DiscoveredResource Resource) DecodeFrame(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);
        var resource = DecodeResource(r);
        return (header, resource);
    }

    /// <summary>
    /// Decodes one <c>Resource</c> Avro record in the exact field order defined by the
    /// ETP v1.1 schema for <c>Energistics.Datatypes.Object.Resource</c>.
    /// </summary>
    internal static DiscoveredResource DecodeResource(AvroReader r)
    {
        // Field order per ETP v1.1 schema:
        //   uri, contentType, name, channelSubscribable, customData,
        //   resourceType, hasChildren, uuid (nullable union), lastChanged, objectNotifiable
        var uri = r.ReadString();
        var contentType = r.ReadString();
        var name = r.ReadString();
        var channelSubscribable = r.ReadBool();

        // customData: map<string, string>
        var customData = new Dictionary<string, string>();
        long blockCount;
        while ((blockCount = r.ReadBlockCount()) > 0)
        {
            for (var i = 0; i < blockCount; i++)
            {
                var key = r.ReadString();
                var value = r.ReadString();
                customData[key] = value;
            }
        }

        var resourceType = r.ReadString();
        var hasChildren = r.ReadInt();

        // uuid: union ["null", "string"]
        var uuidUnionIndex = r.ReadLong();
        var uuid = uuidUnionIndex == 1 ? r.ReadString() : null;

        var lastChanged = r.ReadLong();
        var objectNotifiable = r.ReadBool();

        return new DiscoveredResource
        {
            Uri = uri,
            ContentType = contentType,
            Name = name,
            ChannelSubscribable = channelSubscribable,
            CustomData = customData,
            ResourceType = resourceType,
            HasChildren = hasChildren,
            Uuid = uuid,
            LastChanged = lastChanged,
            ObjectNotifiable = objectNotifiable,
        };
    }
}
