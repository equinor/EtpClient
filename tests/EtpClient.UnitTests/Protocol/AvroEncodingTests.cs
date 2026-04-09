using System.Text;
using EtpClient.Protocol;
using Xunit;

namespace EtpClient.UnitTests.Protocol;

public sealed class AvroEncodingTests
{
    // ── int (zigzag + varint) ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(64)]
    [InlineData(-64)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void RoundTrip_Int(int value)
    {
        var buf = Write(w => w.WriteInt(value));
        var reader = new AvroReader(buf);
        Assert.Equal(value, reader.ReadInt());
    }

    // ── long (zigzag + varint) ────────────────────────────────────────────────

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void RoundTrip_Long(long value)
    {
        var buf = Write(w => w.WriteLong(value));
        var reader = new AvroReader(buf);
        Assert.Equal(value, reader.ReadLong());
    }

    // ── string ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("EtpClient/1.0")]
    [InlineData("xml")]
    public void RoundTrip_String(string value)
    {
        var buf = Write(w => w.WriteString(value));
        var reader = new AvroReader(buf);
        Assert.Equal(value, reader.ReadString());
    }

    // ── bytes ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Bytes_Empty()
    {
        var buf = Write(w => w.WriteBytes([]));
        var r = new AvroReader(buf);
        Assert.Equal(Array.Empty<byte>(), r.ReadBytes());
    }

    [Fact]
    public void RoundTrip_Bytes_NonEmpty()
    {
        byte[] data = [0x01, 0xFF, 0xAB];
        var buf = Write(w => w.WriteBytes(data));
        var r = new AvroReader(buf);
        Assert.Equal(data, r.ReadBytes());
    }

    // ── fixed ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Fixed_16bytes()
    {
        var uuid = Guid.NewGuid().ToByteArray();
        var buf = Write(w => w.WriteFixed(uuid));
        var r = new AvroReader(buf);
        Assert.Equal(uuid, r.ReadFixed(16));
    }

    // ── bool ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoundTrip_Bool(bool value)
    {
        var buf = Write(w => w.WriteBool(value));
        var r = new AvroReader(buf);
        Assert.Equal(value, r.ReadBool());
    }

    // ── empty array block ─────────────────────────────────────────────────────

    [Fact]
    public void EmptyArray_WritesZeroBlock()
    {
        var buf = Write(w =>
        {
            w.WriteArrayStart(0);
            w.WriteArrayEnd();
        });
        var r = new AvroReader(buf);
        Assert.Equal(0, r.ReadBlockCount());   // first (and only) block has count 0
    }

    // ── non-empty array ───────────────────────────────────────────────────────

    [Fact]
    public void Array_WithItems_RoundTrip()
    {
        var items = new[] { "alpha", "beta", "gamma" };
        var buf = Write(w =>
        {
            w.WriteArrayStart(items.Length);
            foreach (var s in items) w.WriteString(s);
            w.WriteArrayEnd();
        });

        var r = new AvroReader(buf);
        var count = r.ReadBlockCount();
        Assert.Equal(items.Length, count);
        var decoded = new List<string>();
        for (var i = 0; i < count; i++) decoded.Add(r.ReadString());
        Assert.Equal(0, r.ReadBlockCount()); // terminating block
        Assert.Equal(items, decoded);
    }

    // ── empty map block ───────────────────────────────────────────────────────

    [Fact]
    public void EmptyMap_WritesZeroBlock()
    {
        var buf = Write(w =>
        {
            w.WriteMapStart(0);
            w.WriteMapEnd();
        });
        var r = new AvroReader(buf);
        Assert.Equal(0, r.ReadBlockCount());
    }

    // ── multiple primitives in one stream ─────────────────────────────────────

    [Fact]
    public void MultipleFields_RoundTrip()
    {
        var buf = Write(w =>
        {
            w.WriteInt(42);
            w.WriteLong(-99L);
            w.WriteString("test");
            w.WriteBool(true);
        });

        var r = new AvroReader(buf);
        Assert.Equal(42, r.ReadInt());
        Assert.Equal(-99L, r.ReadLong());
        Assert.Equal("test", r.ReadString());
        Assert.True(r.ReadBool());
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ReadOnlyMemory<byte> Write(Action<AvroWriter> action)
    {
        var writer = new AvroWriter();
        action(writer);
        return writer.ToArray();
    }
}
