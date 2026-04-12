using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Decodes ETP Protocol 1 ChannelMetadata messages (producer → consumer) from Avro binary.
/// Body schema: { channels: array&lt;ChannelMetadataRecord&gt; }
/// </summary>
internal static class ChannelMetadataMessage
{
    private static readonly string[] ChannelStatuses = ["Active", "Inactive", "Closed"];
    private static readonly string[] IndexTypes = ["Time", "Depth"];
    private static readonly string[] Directions = ["Increasing", "Decreasing"];

    /// <summary>Decodes a ChannelMetadata frame using Avro binary encoding.</summary>
    public static (EtpMessageHeader Header, IReadOnlyList<ChannelDefinition> Channels) DecodeFrame(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);

        // Body: { channels: array<ChannelMetadataRecord> }
        var channels = new List<ChannelDefinition>();
        long count;
        while ((count = r.ReadBlockCount()) != 0)
        {
            for (long i = 0; i < count; i++)
                channels.Add(ReadChannelMetadataRecord(r));
        }

        return (header, channels);
    }

    private static ChannelDefinition ReadChannelMetadataRecord(AvroReader r)
    {
        // Fields in Avro schema order (ETP v1.1 spec §3.3.16.2):
        // channelUri, channelId, indexes, channelName, dataType, uom,
        // startIndex (null|long), endIndex (null|long), description,
        // status (enum), contentType (null|string), source, measureClass,
        // uuid (null|string), customData (map<DataValue>), domainObject (null|DataObject)

        var channelUri  = r.ReadString();
        var channelId   = r.ReadLong();

        // indexes: array<IndexMetadataRecord>
        var (indexType, indexUom, indexDirection, indexScale, indexTimeDatum, indexDepthDatum, indexMnemonic, indexDescription)
            = ReadPrimaryIndexMetadata(r);

        var channelName = r.ReadString();
        var dataType    = r.ReadString();
        var uom         = r.ReadString();

        // startIndex: null|long union (0=null, 1=long)
        long? startIndex = null;
        var startUnion = r.ReadLong();
        if (startUnion == 1L) startIndex = r.ReadLong();

        // endIndex: null|long union
        long? endIndex = null;
        var endUnion = r.ReadLong();
        if (endUnion == 1L) endIndex = r.ReadLong();

        var description  = r.ReadString();
        var statusIdx    = (int)r.ReadLong();  // enum
        var status       = statusIdx >= 0 && statusIdx < ChannelStatuses.Length
                            ? ChannelStatuses[statusIdx] : "Active";

        // contentType: null|string union (0=null, 1=string)
        var contentType = string.Empty;
        var ctUnion = r.ReadLong();
        if (ctUnion == 1L) contentType = r.ReadString();

        var source       = r.ReadString();
        var measureClass = r.ReadString();

        // uuid: null|string union
        string? uuid = null;
        var uuidUnion = r.ReadLong();
        if (uuidUnion == 1L) uuid = r.ReadString();

        // customData: map<DataValue> — skip all entries (DataValue is opaque at this level)
        SkipStringDataValueMap(r);

        // domainObject: null|DataObject union — skip if present
        var domUnion = r.ReadLong();
        if (domUnion != 0L)
            SkipDataObject(r);

        return new ChannelDefinition
        {
            ChannelUri      = channelUri,
            ChannelId       = channelId,
            ChannelName     = channelName,
            DataType        = dataType,
            Uom             = uom,
            IndexType       = indexType,
            IndexUom        = indexUom,
            IndexDirection  = indexDirection,
            IndexScale      = indexScale,
            IndexTimeDatum  = indexTimeDatum,
            IndexDepthDatum = indexDepthDatum,
            IndexMnemonic   = indexMnemonic,
            IndexDescription = indexDescription,
            StartIndex      = startIndex,
            EndIndex        = endIndex,
            Description     = description,
            Status          = status,
            ContentType     = contentType,
            Source          = source,
            MeasureClass    = measureClass,
            Uuid            = uuid,
        };
    }

    /// <summary>
    /// Reads the indexes array and returns primary-index metadata including
    /// scale, timeDatum, depthDatum, mnemonic, and description.
    /// </summary>
    private static (string IndexType, string IndexUom, string IndexDirection,
                    int Scale, string? TimeDatum, string? DepthDatum,
                    string? Mnemonic, string? Description)
        ReadPrimaryIndexMetadata(AvroReader r)
    {
        var indexType      = "Time";
        var indexUom       = string.Empty;
        var indexDirection = "Increasing";
        var scale          = 0;
        string? timeDatum  = null;
        string? depthDatum = null;
        string? mnemonic   = null;
        string? description = null;

        var first = true;
        long count;
        while ((count = r.ReadBlockCount()) != 0)
        {
            for (long i = 0; i < count; i++)
            {
                // IndexMetadataRecord fields (ETP v1.1 spec §3.3.16.8):
                // indexType(enum), uom(string), depthDatum(null|string),
                // direction(enum), mnemonic(null|string), description(null|string),
                // uri(null|string), customData(map<DataValue>), scale(int), timeDatum(null|string)
                var itIdx = (int)r.ReadLong(); // enum indexType
                var uom   = r.ReadString();

                // depthDatum: null|string
                string? dd = null;
                var ddUnion = r.ReadLong();
                if (ddUnion != 0L) dd = r.ReadString();

                var dirIdx = (int)r.ReadLong(); // enum direction

                // mnemonic: null|string
                string? mn = null;
                var mnUnion = r.ReadLong();
                if (mnUnion != 0L) mn = r.ReadString();

                // description: null|string
                string? desc = null;
                var descUnion = r.ReadLong();
                if (descUnion != 0L) desc = r.ReadString();

                // uri: null|string
                var uriUnion = r.ReadLong();
                if (uriUnion != 0L) r.SkipString();

                // customData: map<DataValue>
                SkipStringDataValueMap(r);

                // scale: int
                var sc = r.ReadInt();

                // timeDatum: null|string
                string? td = null;
                var tdUnion = r.ReadLong();
                if (tdUnion != 0L) td = r.ReadString();

                if (first)
                {
                    indexType      = itIdx >= 0 && itIdx < IndexTypes.Length ? IndexTypes[itIdx] : "Time";
                    indexUom       = uom;
                    indexDirection = dirIdx >= 0 && dirIdx < Directions.Length ? Directions[dirIdx] : "Increasing";
                    scale          = sc;
                    timeDatum      = td;
                    depthDatum     = dd;
                    mnemonic       = mn;
                    description    = desc;
                    first = false;
                }
                // Subsequent indexes are ignored at this level
            }
        }
        return (indexType, indexUom, indexDirection, scale, timeDatum, depthDatum, mnemonic, description);
    }

    /// <summary>Skips a map with string keys and DataValue values.</summary>
    private static void SkipStringDataValueMap(AvroReader r)
        => r.SkipStringDataValueMap();

    /// <summary>Skips a DataObject (null already consumed as union index 1).</summary>
    private static void SkipDataObject(AvroReader r)
    {
        // DataObject fields per ETP spec:
        // resource: Resource (large struct), data: bytes
        // For simplicity, skip all DataObject content by reading it atomically —
        // DataObject is a bytes field; read and discard.
        r.SkipBytes();
    }
}
