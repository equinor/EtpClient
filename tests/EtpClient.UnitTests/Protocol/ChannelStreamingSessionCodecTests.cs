using EtpClient.Models;
using EtpClient.Protocol;

namespace EtpClient.UnitTests.Protocol;

/// <summary>
/// Unit tests for binary and JSON codec methods covering Protocol 1 ChannelStreaming messages:
/// ChannelDescribe, ChannelMetadata, ChannelStreamingStart, ChannelStreamingStop,
/// ChannelData, ChannelDataChange, ChannelStatusChange, ChannelRemove, ChannelRangeRequest.
/// Referenced by T009 [US1], T017 [US2], T025 [US3].
/// Tests are written before implementation (test-first / TDD).
/// </summary>
public sealed class ChannelStreamingSessionCodecTests
{
    [Fact]
    public void Binary_EncodeChannelStreamingProtocolStart_HeaderHasProtocol1AndType0()
    {
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeChannelStreamingProtocolStart(maxMessageRate: 1, maxDataItems: 2048, messageId: 1L);
        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.Start, header.MessageType);
        Assert.Equal(1L, header.MessageId);

        var reader = new AvroReader(frame);
        _ = EtpMessageHeader.ReadFrom(reader);
        Assert.Equal(1, reader.ReadInt());
        Assert.Equal(2048, reader.ReadInt());
    }

    [Fact]
    public void Json_EncodeChannelStreamingProtocolStart_ProducesValidJsonWithProtocol1()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeChannelStreamingProtocolStart(maxMessageRate: 1, maxDataItems: 1024, messageId: 2L);

        var json = System.Text.Encoding.UTF8.GetString(frame.Span);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(EtpProtocol.ChannelStreaming, root[0].GetProperty("protocol").GetInt32());
        Assert.Equal(EtpChannelStreamingMessageType.Start, root[0].GetProperty("messageType").GetInt32());
        Assert.Equal(1, root[1].GetProperty("maxMessageRate").GetInt32());
        Assert.Equal(1024, root[1].GetProperty("maxDataItems").GetInt32());
    }

    // ── T009 [US1]: ChannelDescribe encode ──────────────────────────────────

    [Fact]
    public void Binary_EncodeChannelDescribe_HeaderHasProtocol1AndType1()
    {
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeChannelDescribe(["eml://witsml20/well"], messageId: 2L);
        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelDescribe, header.MessageType);
        Assert.Equal(2L, header.MessageId);
        Assert.NotEqual(0, header.MessageFlags & EtpMessageFlags.FinalPart);
    }

    [Fact]
    public void Binary_EncodeChannelDescribe_UrisAreEncodedInBody()
    {
        var codec = new BinaryEtpSessionCodec();
        var uris = new[] { "eml://witsml14/well(abc001)", "eml://witsml20/wellbore(xyz)" };
        var frame = codec.EncodeChannelDescribe(uris, messageId: 3L);

        var r = new AvroReader(frame);
        _ = EtpMessageHeader.ReadFrom(r); // skip header
        // array of strings: count, string, string, end(0)
        var count = r.ReadBlockCount();
        Assert.Equal(2, count);
        Assert.Equal(uris[0], r.ReadString());
        Assert.Equal(uris[1], r.ReadString());
        Assert.Equal(0L, r.ReadBlockCount()); // terminator
    }

    [Fact]
    public void Json_EncodeChannelDescribe_ProducesTwoElementJsonArrayWithProtocol1()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeChannelDescribe(["eml://witsml20/well"], messageId: 5L);

        var json = System.Text.Encoding.UTF8.GetString(frame.Span);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(System.Text.Json.JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(EtpProtocol.ChannelStreaming, root[0].GetProperty("protocol").GetInt32());
        Assert.Equal(EtpChannelStreamingMessageType.ChannelDescribe, root[0].GetProperty("messageType").GetInt32());
        Assert.Equal(5L, root[0].GetProperty("messageId").GetInt64());
    }

    [Fact]
    public void Json_EncodeChannelDescribe_UrisArePresentInBody()
    {
        var codec = new JsonEtpSessionCodec();
        var uris = new[] { "eml://witsml14/wellbore(001)", "eml://witsml14/log(456)" };
        var frame = codec.EncodeChannelDescribe(uris, messageId: 4L);

        var json = System.Text.Encoding.UTF8.GetString(frame.Span);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var body = doc.RootElement[1];

        var urisEl = body.GetProperty("uris");
        Assert.Equal(2, urisEl.GetArrayLength());
        Assert.Equal(uris[0], urisEl[0].GetString());
        Assert.Equal(uris[1], urisEl[1].GetString());
    }

    // ── T009 [US1]: ChannelMetadata decode ───────────────────────────────────

    [Fact]
    public void Binary_DecodeChannelMetadata_PopulatesChannelDefinitions()
    {
        var channel = CreateSampleChannelDefinition(channelId: 1L, channelUri: "eml://witsml14/well(abc)/log(L1)/channel(RPM)");
        var frame = BuildBinaryChannelMetadataFrame([channel], messageId: 2L, correlationId: 2L, finalPart: true);
        var codec = new BinaryEtpSessionCodec();

        var (header, channels) = codec.DecodeChannelMetadata(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelMetadata, header.MessageType);
        Assert.Single(channels);
        Assert.Equal(channel.ChannelId, channels[0].ChannelId);
        Assert.Equal(channel.ChannelUri, channels[0].ChannelUri);
        Assert.Equal(channel.ChannelName, channels[0].ChannelName);
        Assert.Equal(channel.DataType, channels[0].DataType);
        Assert.Equal(channel.Uom, channels[0].Uom);
        Assert.Equal(channel.Status, channels[0].Status);
    }

    [Fact]
    public void Binary_DecodeChannelMetadata_FinalPartFlag_IsDetected()
    {
        var channel = CreateSampleChannelDefinition(channelId: 1L);
        var finalFrame = BuildBinaryChannelMetadataFrame([channel], messageId: 2L, correlationId: 2L, finalPart: true);
        var nonFinalFrame = BuildBinaryChannelMetadataFrame([channel], messageId: 2L, correlationId: 2L, finalPart: false);

        var codec = new BinaryEtpSessionCodec();
        var (finalHeader, _) = codec.DecodeChannelMetadata(finalFrame);
        var (nonFinalHeader, _) = codec.DecodeChannelMetadata(nonFinalFrame);

        Assert.NotEqual(0, finalHeader.MessageFlags & EtpMessageFlags.FinalPart);
        Assert.Equal(0, nonFinalHeader.MessageFlags & EtpMessageFlags.FinalPart);
    }

    [Fact]
    public void Json_DecodeChannelMetadata_PopulatesChannelDefinitions()
    {
        var channel = CreateSampleChannelDefinition(channelId: 2L, channelUri: "eml://witsml14/well(abc)/log(L1)/channel(WOB)");
        var frame = BuildJsonChannelMetadataFrame([channel], messageId: 3L, correlationId: 3L, finalPart: true);
        var codec = new JsonEtpSessionCodec();

        var (header, channels) = codec.DecodeChannelMetadata(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelMetadata, header.MessageType);
        Assert.Single(channels);
        Assert.Equal(channel.ChannelId, channels[0].ChannelId);
        Assert.Equal(channel.ChannelUri, channels[0].ChannelUri);
        Assert.Equal(channel.ChannelName, channels[0].ChannelName);
    }

    // ── T017 [US2]: ChannelStreamingStart encode ────────────────────────────

    [Fact]
    public void Binary_EncodeChannelStreamingStart_HeaderHasProtocol1AndType4()
    {
        var codec = new BinaryEtpSessionCodec();
        var subscriptions = new[] { new ChannelSubscriptionInfo(1L, startLatest: true, receiveChangeNotifications: false) };
        var frame = codec.EncodeChannelStreamingStart(subscriptions, messageId: 4L);
        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelStreamingStart, header.MessageType);
    }

    [Fact]
    public void Binary_EncodeChannelStreamingStop_HeaderHasProtocol1AndType5()
    {
        var codec = new BinaryEtpSessionCodec();
        var frame = codec.EncodeChannelStreamingStop([1L, 2L], messageId: 5L);
        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelStreamingStop, header.MessageType);
    }

    [Fact]
    public void Json_EncodeChannelStreamingStart_ProducesValidJsonWithProtocol1()
    {
        var codec = new JsonEtpSessionCodec();
        var subscriptions = new[] { new ChannelSubscriptionInfo(3L, startLatest: true, receiveChangeNotifications: true) };
        var frame = codec.EncodeChannelStreamingStart(subscriptions, messageId: 6L);

        var json = System.Text.Encoding.UTF8.GetString(frame.Span);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(EtpProtocol.ChannelStreaming, root[0].GetProperty("protocol").GetInt32());
        Assert.Equal(EtpChannelStreamingMessageType.ChannelStreamingStart, root[0].GetProperty("messageType").GetInt32());
    }

    [Fact]
    public void Json_EncodeChannelStreamingStop_ProducesValidJsonWithProtocol1()
    {
        var codec = new JsonEtpSessionCodec();
        var frame = codec.EncodeChannelStreamingStop([7L, 8L], messageId: 7L);

        var json = System.Text.Encoding.UTF8.GetString(frame.Span);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(EtpProtocol.ChannelStreaming, root[0].GetProperty("protocol").GetInt32());
        Assert.Equal(EtpChannelStreamingMessageType.ChannelStreamingStop, root[0].GetProperty("messageType").GetInt32());
    }

    // ── T017 [US2]: ChannelData decode ───────────────────────────────────────

    [Fact]
    public void Binary_DecodeChannelData_PopulatesDataItems()
    {
        var item = new ChannelDataItemWire(Indexes: [1000L], ChannelId: 1L, ValueAsDouble: 3.14);
        var frame = BuildBinaryChannelDataFrame([item], messageId: 8L, correlationId: 0L, finalPart: true);
        var codec = new BinaryEtpSessionCodec();

        var (header, items) = codec.DecodeChannelData(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelData, header.MessageType);
        Assert.Single(items);
        Assert.Equal(1L, items[0].ChannelId);
        Assert.Equal(1000L, items[0].Indexes[0]);
    }

    [Fact]
    public void Binary_DecodeChannelData_WithValueAttributes_DecodesWithoutMisalignment()
    {
        var frame = BuildBinaryChannelDataFrameWithAttributes(
            indexes: [1000L],
            channelId: 1L,
            value: 3.14,
            attributeId: 42,
            attributeValue: "quality");
        var codec = new BinaryEtpSessionCodec();

        var (_, items) = codec.DecodeChannelData(frame);

        Assert.Single(items);
        Assert.Equal(1L, items[0].ChannelId);
        Assert.Equal(1000L, items[0].Indexes[0]);
        Assert.Equal(3.14, Assert.IsType<double>(items[0].Value), 6);
    }

    [Fact]
    public void Binary_DecodeChannelData_WithLegacyNamedValueAttributes_DecodesWithoutMisalignment()
    {
        var frame = BuildBinaryChannelDataFrameWithLegacyAttributes(
            indexes: [1001L],
            channelId: 3L,
            value: 4.14,
            attributeName: "quality",
            attributeValue: "good");
        var codec = new BinaryEtpSessionCodec();

        var (_, items) = codec.DecodeChannelData(frame);

        Assert.Single(items);
        Assert.Equal(3L, items[0].ChannelId);
        Assert.Equal(1001L, items[0].Indexes[0]);
        Assert.Equal(4.14, Assert.IsType<double>(items[0].Value), 6);
    }

    [Fact]
    public void Binary_DecodeChannelData_VectorValue_ReturnsDoubleArray()
    {
        var frame = BuildBinaryChannelDataFrameWithVector(
            indexes: [2000L],
            channelId: 2L,
            values: [1.5, 2.5]);
        var codec = new BinaryEtpSessionCodec();

        var (_, items) = codec.DecodeChannelData(frame);

        var vector = Assert.IsType<double[]>(Assert.Single(items).Value);
        Assert.Equal([1.5, 2.5], vector);
    }

    [Fact]
    public void Json_DecodeChannelData_PopulatesDataItems()
    {
        var item = new ChannelDataItemWire(Indexes: [2000L], ChannelId: 2L, ValueAsDouble: 42.0);
        var frame = BuildJsonChannelDataFrame([item], messageId: 9L, correlationId: 0L, finalPart: true);
        var codec = new JsonEtpSessionCodec();

        var (header, items) = codec.DecodeChannelData(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelData, header.MessageType);
        Assert.Single(items);
        Assert.Equal(2L, items[0].ChannelId);
    }

    // ── T017 [US2]: ChannelRemove decode ─────────────────────────────────────

    [Fact]
    public void Binary_DecodeChannelRemove_PopulatesChannelIdAndReason()
    {
        var frame = BuildBinaryChannelRemoveFrame(channelId: 5L, removeReason: "Server closed channel", messageId: 10L);
        var codec = new BinaryEtpSessionCodec();

        var (header, channelId, reason) = codec.DecodeChannelRemove(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelRemove, header.MessageType);
        Assert.Equal(5L, channelId);
        Assert.Equal("Server closed channel", reason);
    }

    [Fact]
    public void Binary_DecodeChannelRemove_NullReason_IsNull()
    {
        var frame = BuildBinaryChannelRemoveFrame(channelId: 3L, removeReason: null, messageId: 11L);
        var codec = new BinaryEtpSessionCodec();

        var (_, channelId, reason) = codec.DecodeChannelRemove(frame);

        Assert.Equal(3L, channelId);
        Assert.Null(reason);
    }

    [Fact]
    public void Json_DecodeChannelRemove_PopulatesChannelIdAndReason()
    {
        var frame = BuildJsonChannelRemoveFrame(channelId: 7L, removeReason: "Shutdown", messageId: 12L);
        var codec = new JsonEtpSessionCodec();

        var (header, channelId, reason) = codec.DecodeChannelRemove(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelRemove, header.MessageType);
        Assert.Equal(7L, channelId);
        Assert.Equal("Shutdown", reason);
    }

    // ── T025 [US3]: ChannelRangeRequest encode ───────────────────────────────

    [Fact]
    public void Binary_EncodeChannelRangeRequest_HeaderHasProtocol1AndType9()
    {
        var codec = new BinaryEtpSessionCodec();
        var ranges = new[] { new ChannelRangeInfoWire(channelIds: [1L, 2L], startIndex: 1000L, endIndex: 2000L) };
        var frame = codec.EncodeChannelRangeRequest(ranges, messageId: 20L);
        var header = codec.DecodeHeader(frame);

        Assert.Equal(EtpProtocol.ChannelStreaming, header.Protocol);
        Assert.Equal(EtpChannelStreamingMessageType.ChannelRangeRequest, header.MessageType);
    }

    [Fact]
    public void Json_EncodeChannelRangeRequest_ProducesValidJsonWithChannelRangeInfo()
    {
        var codec = new JsonEtpSessionCodec();
        var ranges = new[] { new ChannelRangeInfoWire(channelIds: [3L], startIndex: 500L, endIndex: 1000L) };
        var frame = codec.EncodeChannelRangeRequest(ranges, messageId: 21L);

        var json = System.Text.Encoding.UTF8.GetString(frame.Span);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(EtpProtocol.ChannelStreaming, root[0].GetProperty("protocol").GetInt32());
        Assert.Equal(EtpChannelStreamingMessageType.ChannelRangeRequest, root[0].GetProperty("messageType").GetInt32());

        var channelRanges = root[1].GetProperty("channelRanges");
        Assert.Equal(1, channelRanges.GetArrayLength());
        Assert.Equal(500L, channelRanges[0].GetProperty("startIndex").GetInt64());
        Assert.Equal(1000L, channelRanges[0].GetProperty("endIndex").GetInt64());
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static ChannelDefinition CreateSampleChannelDefinition(long channelId, string? channelUri = null) =>
        new()
        {
            ChannelId = channelId,
            ChannelUri = channelUri ?? $"eml://witsml14/well(abc)/log(L1)/channel({channelId})",
            ChannelName = $"Channel{channelId}",
            DataType = "double",
            Uom = "rpm",
            IndexType = "Time",
            IndexUom = "ms",
            IndexDirection = "Increasing",
            Description = $"Test channel {channelId}",
            Status = "Active",
            Source = "test",
            MeasureClass = "angular velocity",
        };

    /// <summary>Builds a binary ChannelMetadata frame for testing.</summary>
    private static ReadOnlyMemory<byte> BuildBinaryChannelMetadataFrame(
        IReadOnlyList<ChannelDefinition> channels,
        long messageId,
        long correlationId,
        bool finalPart)
    {
        var w = new AvroWriter();
        var header = new EtpMessageHeader(
            Protocol: EtpProtocol.ChannelStreaming,
            MessageType: EtpChannelStreamingMessageType.ChannelMetadata,
            CorrelationId: correlationId,
            MessageId: messageId,
            MessageFlags: finalPart ? EtpMessageFlags.FinalPart : 0);
        header.WriteTo(w);

        // channels array
        w.WriteArrayStart(channels.Count);
        foreach (var ch in channels)
        {
            WriteChannelMetadataRecord(w, ch);
        }
        w.WriteArrayEnd();

        return w.ToArray();
    }

    private static void WriteChannelMetadataRecord(AvroWriter w, ChannelDefinition ch)
    {
        w.WriteString(ch.ChannelUri);
        w.WriteLong(ch.ChannelId);
        // indexes array (one primary index)
        w.WriteArrayStart(1);
        WriteIndexMetadataRecord(w, ch);
        w.WriteArrayEnd();
        w.WriteString(ch.ChannelName);
        w.WriteString(ch.DataType);
        w.WriteString(ch.Uom);
        // startIndex: null
        w.WriteLong(0L); // union index 0 = null
        // endIndex: null
        w.WriteLong(0L); // union index 0 = null
        w.WriteString(ch.Description);
        // status: enum — Active=0, Inactive=1, Closed=2
        w.WriteInt(ch.Status == "Active" ? 0 : ch.Status == "Inactive" ? 1 : 2);
        // contentType: null
        w.WriteLong(0L); // union index 0 = null
        w.WriteString(ch.Source);
        w.WriteString(ch.MeasureClass);
        // uuid: null
        w.WriteLong(0L); // union index 0 = null
        // customData map: empty
        w.WriteMapEnd();
        // domainObject: null
        w.WriteLong(0L); // union index 0 = null
    }

    private static void WriteIndexMetadataRecord(AvroWriter w, ChannelDefinition ch)
    {
        // indexType enum: Time=0, Depth=1
        w.WriteInt(ch.IndexType == "Time" ? 0 : 1);
        w.WriteString(ch.IndexUom);
        // depthDatum: null
        w.WriteLong(0L);
        // direction enum: Increasing=0, Decreasing=1
        w.WriteInt(ch.IndexDirection == "Increasing" ? 0 : 1);
        // mnemonic: null
        w.WriteLong(0L);
        // description: null
        w.WriteLong(0L);
        // uri: null
        w.WriteLong(0L);
        // customData: empty map
        w.WriteMapEnd();
        // scale
        w.WriteInt(3);
        // timeDatum: null
        w.WriteLong(0L);
    }

    private static ReadOnlyMemory<byte> BuildJsonChannelMetadataFrame(
        IReadOnlyList<ChannelDefinition> channels,
        long messageId,
        long correlationId,
        bool finalPart)
    {
        var channelsArray = new System.Text.Json.Nodes.JsonArray();
        foreach (var ch in channels)
        {
            channelsArray.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["channelUri"] = ch.ChannelUri,
                ["channelId"] = ch.ChannelId,
                ["indexes"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
                {
                    ["indexType"] = ch.IndexType,
                    ["uom"] = ch.IndexUom,
                    ["depthDatum"] = null,
                    ["direction"] = ch.IndexDirection,
                    ["mnemonic"] = null,
                    ["description"] = null,
                    ["uri"] = null,
                    ["customData"] = new System.Text.Json.Nodes.JsonObject(),
                    ["scale"] = 3,
                    ["timeDatum"] = null,
                }),
                ["channelName"] = ch.ChannelName,
                ["dataType"] = ch.DataType,
                ["uom"] = ch.Uom,
                ["startIndex"] = null,
                ["endIndex"] = null,
                ["description"] = ch.Description,
                ["status"] = ch.Status,
                ["contentType"] = null,
                ["source"] = ch.Source,
                ["measureClass"] = ch.MeasureClass,
                ["uuid"] = null,
                ["customData"] = new System.Text.Json.Nodes.JsonObject(),
                ["domainObject"] = null,
            });
        }

        var msg = new System.Text.Json.Nodes.JsonArray
        {
            new System.Text.Json.Nodes.JsonObject
            {
                ["protocol"] = EtpProtocol.ChannelStreaming,
                ["messageType"] = EtpChannelStreamingMessageType.ChannelMetadata,
                ["correlationId"] = correlationId,
                ["messageId"] = messageId,
                ["messageFlags"] = finalPart ? EtpMessageFlags.FinalPart : 0,
            },
            new System.Text.Json.Nodes.JsonObject
            {
                ["channels"] = channelsArray,
            },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    // ── US2 frame helpers ─────────────────────────────────────────────────────

    /// <summary>Wire representation of a DataItem for test frame building.</summary>
    internal sealed record ChannelDataItemWire(long[] Indexes, long ChannelId, double ValueAsDouble);

    private static ReadOnlyMemory<byte> BuildBinaryChannelDataFrame(
        IReadOnlyList<ChannelDataItemWire> items,
        long messageId,
        long correlationId,
        bool finalPart)
    {
        var w = new AvroWriter();
        var header = new EtpMessageHeader(
            Protocol: EtpProtocol.ChannelStreaming,
            MessageType: EtpChannelStreamingMessageType.ChannelData,
            CorrelationId: correlationId,
            MessageId: messageId,
            MessageFlags: finalPart ? EtpMessageFlags.FinalPart : 0);
        header.WriteTo(w);

        w.WriteArrayStart(items.Count);
        foreach (var item in items)
        {
            // indexes array
            w.WriteArrayStart(item.Indexes.Length);
            foreach (var idx in item.Indexes) w.WriteLong(idx);
            w.WriteArrayEnd();
            // channelId
            w.WriteLong(item.ChannelId);
            // value: DataValue union — index 1 = double
            w.WriteLong(1L);
            w.WriteDouble(item.ValueAsDouble);
            // valueAttributes: empty array
            w.WriteArrayEnd();
        }
        w.WriteArrayEnd();

        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildBinaryChannelDataFrameWithAttributes(
        long[] indexes,
        long channelId,
        double value,
        int attributeId,
        string attributeValue)
    {
        var w = new AvroWriter();
        var header = new EtpMessageHeader(
            Protocol: EtpProtocol.ChannelStreaming,
            MessageType: EtpChannelStreamingMessageType.ChannelData,
            CorrelationId: 0L,
            MessageId: 99L,
            MessageFlags: EtpMessageFlags.FinalPart);
        header.WriteTo(w);

        w.WriteArrayStart(1);
        w.WriteArrayStart(indexes.Length);
        foreach (var index in indexes) w.WriteLong(index);
        w.WriteArrayEnd();
        w.WriteLong(channelId);
        w.WriteLong(1L);
        w.WriteDouble(value);
        w.WriteArrayStart(1);
        w.WriteInt(attributeId);
        w.WriteLong(5L);
        w.WriteString(attributeValue);
        w.WriteArrayEnd();
        w.WriteArrayEnd();

        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildBinaryChannelDataFrameWithLegacyAttributes(
        long[] indexes,
        long channelId,
        double value,
        string attributeName,
        string attributeValue)
    {
        var w = new AvroWriter();
        var header = new EtpMessageHeader(
            Protocol: EtpProtocol.ChannelStreaming,
            MessageType: EtpChannelStreamingMessageType.ChannelData,
            CorrelationId: 0L,
            MessageId: 101L,
            MessageFlags: EtpMessageFlags.FinalPart);
        header.WriteTo(w);

        w.WriteArrayStart(1);
        w.WriteArrayStart(indexes.Length);
        foreach (var index in indexes) w.WriteLong(index);
        w.WriteArrayEnd();
        w.WriteLong(channelId);
        w.WriteLong(1L);
        w.WriteDouble(value);
        w.WriteArrayStart(1);
        w.WriteString(attributeName);
        w.WriteLong(5L);
        w.WriteString(attributeValue);
        w.WriteArrayEnd();
        w.WriteArrayEnd();

        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildBinaryChannelDataFrameWithVector(
        long[] indexes,
        long channelId,
        double[] values)
    {
        var w = new AvroWriter();
        var header = new EtpMessageHeader(
            Protocol: EtpProtocol.ChannelStreaming,
            MessageType: EtpChannelStreamingMessageType.ChannelData,
            CorrelationId: 0L,
            MessageId: 100L,
            MessageFlags: EtpMessageFlags.FinalPart);
        header.WriteTo(w);

        w.WriteArrayStart(1);
        w.WriteArrayStart(indexes.Length);
        foreach (var index in indexes) w.WriteLong(index);
        w.WriteArrayEnd();
        w.WriteLong(channelId);
        w.WriteLong(6L);
        w.WriteArrayStart(values.Length);
        foreach (var item in values) w.WriteDouble(item);
        w.WriteArrayEnd();
        w.WriteArrayEnd();
        w.WriteArrayEnd();

        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildJsonChannelDataFrame(
        IReadOnlyList<ChannelDataItemWire> items,
        long messageId,
        long correlationId,
        bool finalPart)
    {
        var dataArray = new System.Text.Json.Nodes.JsonArray();
        foreach (var item in items)
        {
            var indexesArray = new System.Text.Json.Nodes.JsonArray();
            foreach (var idx in item.Indexes) indexesArray.Add(idx);

            dataArray.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["indexes"] = indexesArray,
                ["channelId"] = item.ChannelId,
                ["value"] = new System.Text.Json.Nodes.JsonObject { ["double"] = item.ValueAsDouble },
                ["valueAttributes"] = new System.Text.Json.Nodes.JsonArray(),
            });
        }

        var msg = new System.Text.Json.Nodes.JsonArray
        {
            new System.Text.Json.Nodes.JsonObject
            {
                ["protocol"] = EtpProtocol.ChannelStreaming,
                ["messageType"] = EtpChannelStreamingMessageType.ChannelData,
                ["correlationId"] = correlationId,
                ["messageId"] = messageId,
                ["messageFlags"] = finalPart ? EtpMessageFlags.FinalPart : 0,
            },
            new System.Text.Json.Nodes.JsonObject { ["data"] = dataArray },
        };

        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }

    private static ReadOnlyMemory<byte> BuildBinaryChannelRemoveFrame(long channelId, string? removeReason, long messageId)
    {
        var w = new AvroWriter();
        var header = new EtpMessageHeader(
            Protocol: EtpProtocol.ChannelStreaming,
            MessageType: EtpChannelStreamingMessageType.ChannelRemove,
            CorrelationId: 0L,
            MessageId: messageId,
            MessageFlags: EtpMessageFlags.FinalPart);
        header.WriteTo(w);
        w.WriteLong(channelId);
        if (removeReason is null)
            w.WriteLong(0L); // union: null
        else
        {
            w.WriteLong(1L); // union: string
            w.WriteString(removeReason);
        }
        return w.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildJsonChannelRemoveFrame(long channelId, string? removeReason, long messageId)
    {
        var msg = new System.Text.Json.Nodes.JsonArray
        {
            new System.Text.Json.Nodes.JsonObject
            {
                ["protocol"] = EtpProtocol.ChannelStreaming,
                ["messageType"] = EtpChannelStreamingMessageType.ChannelRemove,
                ["correlationId"] = 0L,
                ["messageId"] = messageId,
                ["messageFlags"] = EtpMessageFlags.FinalPart,
            },
            new System.Text.Json.Nodes.JsonObject
            {
                ["channelId"] = channelId,
                ["removeReason"] = removeReason,
            },
        };
        return System.Text.Encoding.UTF8.GetBytes(msg.ToJsonString());
    }
}
