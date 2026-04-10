using EtpClient.Models;

namespace EtpClient.Protocol;

/// <summary>
/// Decodes ETP Protocol 1 ChannelData, ChannelDataChange, ChannelStatusChange,
/// and ChannelRemove messages (producer → consumer) from Avro binary.
/// </summary>
internal static class ChannelDataMessage
{
    private static readonly string[] ChannelStatuses = ["Active", "Inactive", "Closed"];

    /// <summary>
    /// Decodes a ChannelData frame using Avro binary encoding.
    /// Body schema: { data: array&lt;DataItem&gt; }
    /// DataItem fields: indexes(array&lt;long&gt;), channelId(long), value(DataValue), valueAttributes(array&lt;DataAttribute&gt;)
    /// </summary>
    public static (EtpMessageHeader Header, IReadOnlyList<ChannelDataItem> Items) DecodeFrame(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);

        var items = new List<ChannelDataItem>();
        long count;
        while ((count = r.ReadBlockCount()) != 0)
        {
            for (long i = 0; i < count; i++)
            {
                // indexes: array<long>
                var indexes = new List<long>();
                long ic;
                while ((ic = r.ReadBlockCount()) != 0)
                    for (long j = 0; j < ic; j++)
                        indexes.Add(r.ReadLong());

                var channelId = r.ReadLong();

                // value: DataValue union
                // Union indices per ETP spec: 0=null, 1=double, 2=float, 3=int, 4=long, 5=string, 6=ArrayOfDouble, 7=boolean, 8=bytes
                var valIdx = r.ReadLong();
                object? value = valIdx switch
                {
                    0 => null,
                    1 => (object)r.ReadDouble(),
                    2 => (object)r.ReadFloat(),
                    3 => (object)r.ReadInt(),
                    4 => (object)r.ReadLong(),
                    5 => (object)r.ReadString(),
                    6 => (object)SkipAndReturnNull(r, 8 * (int)r.ReadLong()), // ArrayOfDouble: count + doubles
                    7 => (object)r.ReadBool(),
                    8 => (object)r.ReadBytes(),
                    _ => SkipUnknownUnionAndReturnNull(r),
                };

                // valueAttributes: array<DataAttribute> — skip all
                SkipDataAttributeArray(r);

                items.Add(new ChannelDataItem
                {
                    Indexes = indexes,
                    ChannelId = channelId,
                    Value = value,
                });
            }
        }

        return (header, items);
    }

    /// <summary>Decodes a ChannelDataChange frame using Avro binary encoding.</summary>
    public static (EtpMessageHeader Header, long ChannelId, long StartIndex, long EndIndex) DecodeChannelDataChange(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);
        // Body fields: channelId(long), startIndex(long), endIndex(long), data(array<DataItem>) — we skip data
        var channelId  = r.ReadLong();
        var startIndex = r.ReadLong();
        var endIndex   = r.ReadLong();
        return (header, channelId, startIndex, endIndex);
    }

    /// <summary>Decodes a ChannelStatusChange frame using Avro binary encoding.</summary>
    public static (EtpMessageHeader Header, long ChannelId, string NewStatus) DecodeChannelStatusChange(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);
        // Body fields: channelId(long), status(ChannelStatuses enum)
        var channelId = r.ReadLong();
        var statusIdx = (int)r.ReadLong();
        var newStatus = statusIdx >= 0 && statusIdx < ChannelStatuses.Length
            ? ChannelStatuses[statusIdx] : "Active";
        return (header, channelId, newStatus);
    }

    /// <summary>Decodes a ChannelRemove frame using Avro binary encoding.</summary>
    public static (EtpMessageHeader Header, long ChannelId, string? Reason) DecodeChannelRemove(
        ReadOnlyMemory<byte> frame)
    {
        var r = new AvroReader(frame);
        var header = EtpMessageHeader.ReadFrom(r);
        // Body fields: channelId(long), removeReason(null|string union)
        var channelId = r.ReadLong();
        string? reason = null;
        var reasonUnion = r.ReadLong();
        if (reasonUnion == 1L) reason = r.ReadString();
        return (header, channelId, reason);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static void SkipDataAttributeArray(AvroReader r)
    {
        long count;
        while ((count = r.ReadBlockCount()) != 0)
        {
            for (long i = 0; i < count; i++)
            {
                r.SkipString();        // name
                r.SkipDataValue();     // value
            }
        }
    }

    private static object? SkipAndReturnNull(AvroReader r, int bytesToSkip) => null;

    private static object? SkipUnknownUnionAndReturnNull(AvroReader r) => null;
}
