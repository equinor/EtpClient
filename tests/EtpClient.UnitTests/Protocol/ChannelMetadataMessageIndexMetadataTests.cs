using EtpClient.Models;
using EtpClient.Protocol;

namespace EtpClient.UnitTests.Protocol;

/// <summary>
/// Unit tests verifying that binary and JSON channel metadata decoding preserves
/// primary-index metadata fields: scale, timeDatum, depthDatum, mnemonic, description.
/// T008 [Foundational], T010 [US1], T017 [US2].
/// </summary>
public sealed class ChannelMetadataMessageIndexMetadataTests
{
    // ── T008 [Foundational]: Binary codec preserves scale ────────────────────

    [Fact]
    public void Binary_DecodeChannelMetadata_PreservesIndexScale()
    {
        var frame = BuildBinaryChannelMetadataFrame(scale: 5, timeDatum: null, depthDatum: null);
        var (_, channels) = ChannelMetadataMessage.DecodeFrame(frame);

        Assert.Single(channels);
        Assert.Equal(5, channels[0].IndexScale);
    }

    [Fact]
    public void Binary_DecodeChannelMetadata_PreservesTimeDatum_WhenPresent()
    {
        var frame = BuildBinaryChannelMetadataFrame(scale: 0, timeDatum: "1970-01-01T00:00:00Z", depthDatum: null);
        var (_, channels) = ChannelMetadataMessage.DecodeFrame(frame);

        Assert.Equal("1970-01-01T00:00:00Z", channels[0].IndexTimeDatum);
    }

    [Fact]
    public void Binary_DecodeChannelMetadata_TimeDatumIsNull_WhenAbsent()
    {
        var frame = BuildBinaryChannelMetadataFrame(scale: 0, timeDatum: null, depthDatum: null);
        var (_, channels) = ChannelMetadataMessage.DecodeFrame(frame);

        Assert.Null(channels[0].IndexTimeDatum);
    }

    [Fact]
    public void Binary_DecodeChannelMetadata_PreservesDepthDatum_WhenPresent()
    {
        var frame = BuildBinaryChannelMetadataFrame(scale: 3, timeDatum: null, depthDatum: "MSL");
        var (_, channels) = ChannelMetadataMessage.DecodeFrame(frame);

        Assert.Equal("MSL", channels[0].IndexDepthDatum);
    }

    // ── T010 [US1]: Binary codec round-trips time channel metadata ───────────

    [Fact]
    public void Binary_DecodeChannelMetadata_TimeChannel_ScaleAvailableForInterpretation()
    {
        var frame = BuildBinaryChannelMetadataFrame(
            indexType: 0, scale: 0, timeDatum: null, depthDatum: null);
        var (_, channels) = ChannelMetadataMessage.DecodeFrame(frame);

        Assert.Equal("Time", channels[0].IndexType);
        Assert.Equal(0, channels[0].IndexScale);
    }

    // ── T017 [US2]: Binary codec round-trips depth channel metadata ──────────

    [Fact]
    public void Binary_DecodeChannelMetadata_DepthChannel_ScalePreserved()
    {
        var frame = BuildBinaryChannelMetadataFrame(
            indexType: 1, scale: 5, timeDatum: null, depthDatum: "MSL");
        var (_, channels) = ChannelMetadataMessage.DecodeFrame(frame);

        Assert.Equal("Depth", channels[0].IndexType);
        Assert.Equal(5, channels[0].IndexScale);
        Assert.Equal("MSL", channels[0].IndexDepthDatum);
    }

    // ── JSON codec: preserves the same fields ────────────────────────────────

    [Fact]
    public void Json_DecodeChannelMetadata_PreservesIndexScale()
    {
        var frame = BuildJsonChannelMetadataFrame(scale: 3, timeDatum: null, depthDatum: null);
        var codec = new JsonEtpSessionCodec();
        var (_, channels) = codec.DecodeChannelMetadata(frame);

        Assert.Single(channels);
        Assert.Equal(3, channels[0].IndexScale);
    }

    [Fact]
    public void Json_DecodeChannelMetadata_PreservesTimeDatum_WhenPresent()
    {
        var frame = BuildJsonChannelMetadataFrame(scale: 0, timeDatum: "2000-01-01T00:00:00Z", depthDatum: null);
        var codec = new JsonEtpSessionCodec();
        var (_, channels) = codec.DecodeChannelMetadata(frame);

        Assert.Equal("2000-01-01T00:00:00Z", channels[0].IndexTimeDatum);
    }

    [Fact]
    public void Json_DecodeChannelMetadata_TimeDatumIsNull_WhenAbsent()
    {
        var frame = BuildJsonChannelMetadataFrame(scale: 0, timeDatum: null, depthDatum: null);
        var codec = new JsonEtpSessionCodec();
        var (_, channels) = codec.DecodeChannelMetadata(frame);

        Assert.Null(channels[0].IndexTimeDatum);
    }

    [Fact]
    public void Json_DecodeChannelMetadata_PreservesDepthDatum_WhenPresent()
    {
        var frame = BuildJsonChannelMetadataFrame(scale: 5, timeDatum: null, depthDatum: "KB");
        var codec = new JsonEtpSessionCodec();
        var (_, channels) = codec.DecodeChannelMetadata(frame);

        Assert.Equal("KB", channels[0].IndexDepthDatum);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ReadOnlyMemory<byte> BuildBinaryChannelMetadataFrame(
        int indexType = 0, int scale = 0, string? timeDatum = null, string? depthDatum = null)
    {
        var w = new AvroWriter();
        // Header
        w.WriteInt(EtpProtocol.ChannelStreaming);
        w.WriteInt(EtpChannelStreamingMessageType.ChannelMetadata);
        w.WriteLong(1L);   // correlationId
        w.WriteLong(2L);   // messageId
        w.WriteInt(EtpMessageFlags.FinalPart);

        // Body: array of 1 ChannelMetadataRecord
        w.WriteArrayStart(1);
        w.WriteString("eml://test/channel(C)"); // channelUri
        w.WriteLong(1L);                          // channelId

        // indexes array: 1 IndexMetadataRecord
        w.WriteArrayStart(1);
        w.WriteInt(indexType);   // indexType enum
        w.WriteString("us");     // uom

        // depthDatum: null|string
        if (depthDatum is null) { w.WriteLong(0L); }
        else { w.WriteLong(1L); w.WriteString(depthDatum); }

        w.WriteInt(0);           // direction=Increasing
        w.WriteLong(0L);         // mnemonic=null
        w.WriteLong(0L);         // description=null
        w.WriteLong(0L);         // uri=null
        w.WriteMapEnd();         // customData=empty
        w.WriteInt(scale);       // scale

        // timeDatum: null|string
        if (timeDatum is null) { w.WriteLong(0L); }
        else { w.WriteLong(1L); w.WriteString(timeDatum); }

        w.WriteArrayEnd(); // end indexes

        w.WriteString("C");       // channelName
        w.WriteString("double");  // dataType
        w.WriteString("rpm");     // uom
        w.WriteLong(0L);          // startIndex=null
        w.WriteLong(0L);          // endIndex=null
        w.WriteString("");        // description
        w.WriteInt(0);            // status=Active
        w.WriteLong(0L);          // contentType=null
        w.WriteString("");        // source
        w.WriteString("");        // measureClass
        w.WriteLong(0L);          // uuid=null
        w.WriteMapEnd();          // customData=empty
        w.WriteLong(0L);          // domainObject=null

        w.WriteArrayEnd(); // end channels array

        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildJsonChannelMetadataFrame(
        int scale = 0, string? timeDatum = null, string? depthDatum = null)
    {
        var timeDatumJson = timeDatum is null ? "null" : $"\"{timeDatum}\"";
        var depthDatumJson = depthDatum is null ? "null" : $"\"{depthDatum}\"";

        var json = $$"""
            [
              {
                "protocol": 1,
                "messageType": 2,
                "correlationId": 1,
                "messageId": 2,
                "messageFlags": 2
              },
              {
                "channels": [
                  {
                    "channelUri": "eml://test/channel(C)",
                    "channelId": 1,
                    "channelName": "C",
                    "dataType": "double",
                    "uom": "rpm",
                    "startIndex": null,
                    "endIndex": null,
                    "description": "",
                    "status": "Active",
                    "source": "",
                    "measureClass": "",
                    "indexes": [
                      {
                        "indexType": "Time",
                        "uom": "us",
                        "direction": "Increasing",
                        "scale": {{scale}},
                        "timeDatum": {{timeDatumJson}},
                        "depthDatum": {{depthDatumJson}}
                      }
                    ]
                  }
                ]
              }
            ]
            """;

        return System.Text.Encoding.UTF8.GetBytes(json);
    }
}
