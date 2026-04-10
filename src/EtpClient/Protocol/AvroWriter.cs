using System.Text;

namespace EtpClient.Protocol;

/// <summary>
/// Minimal Avro binary encoder for the specific types required by ETP Protocol 0.
/// Implements the Avro binary encoding specification (zigzag variable-length integers,
/// length-prefixed strings/bytes, fixed-length bytes, bool, array blocks, map blocks).
/// </summary>
internal sealed class AvroWriter
{
    private readonly List<byte> _buf = new(256);

    // ── int ───────────────────────────────────────────────────────────────────

    /// <summary>Writes an Avro int using zigzag + base-128 variable-length encoding.</summary>
    public void WriteInt(int value)
    {
        // Zigzag encode: maps signed to unsigned so small negative numbers encode small
        uint encoded = (uint)((value << 1) ^ (value >> 31));
        WriteVarUInt(encoded);
    }

    // ── long ──────────────────────────────────────────────────────────────────

    /// <summary>Writes an Avro long using zigzag + base-128 variable-length encoding.</summary>
    public void WriteLong(long value)
    {
        ulong encoded = (ulong)((value << 1) ^ (value >> 63));
        WriteVarULong(encoded);
    }

    // ── string ────────────────────────────────────────────────────────────────

    /// <summary>Writes an Avro string: long(byte_count) + UTF-8 bytes.</summary>
    public void WriteString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteLong(bytes.Length);
        _buf.AddRange(bytes);
    }

    // ── bytes ─────────────────────────────────────────────────────────────────

    /// <summary>Writes Avro bytes: long(byte_count) + raw bytes.</summary>
    public void WriteBytes(byte[] value)
    {
        WriteLong(value.Length);
        _buf.AddRange(value);
    }

    // ── fixed ─────────────────────────────────────────────────────────────────

    /// <summary>Writes a fixed-length byte array with no length prefix.</summary>
    public void WriteFixed(byte[] value) => _buf.AddRange(value);

    // ── bool ──────────────────────────────────────────────────────────────────

    /// <summary>Writes an Avro boolean as a single byte (0 or 1).</summary>
    public void WriteBool(bool value) => _buf.Add(value ? (byte)1 : (byte)0);

    // ── array ─────────────────────────────────────────────────────────────────

    /// <summary>Writes the opening block of an Avro array with <paramref name="count"/> items.</summary>
    public void WriteArrayStart(long count) => WriteLong(count);

    /// <summary>Writes the terminating block of an Avro array (0 count).</summary>
    public void WriteArrayEnd() => WriteLong(0L);

    // ── map ───────────────────────────────────────────────────────────────────

    /// <summary>Writes the opening block of an Avro map with <paramref name="count"/> key-value pairs.</summary>
    public void WriteMapStart(long count) => WriteLong(count);

    /// <summary>Writes the terminating block of an Avro map (0 count).</summary>
    public void WriteMapEnd() => WriteLong(0L);

    // ── float / double ────────────────────────────────────────────────────────────────

    /// <summary>Writes an Avro double (8 bytes, little-endian IEEE 754).</summary>
    public void WriteDouble(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        _buf.AddRange(bytes);
    }

    /// <summary>Writes an Avro float (4 bytes, little-endian IEEE 754).</summary>
    public void WriteFloat(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        _buf.AddRange(bytes);
    }

    // ── output ────────────────────────────────────────────────────────────────

    /// <summary>Returns the encoded bytes as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    public ReadOnlyMemory<byte> ToArray() => _buf.ToArray();

    // ── internal helpers ─────────────────────────────────────────────────────

    private void WriteVarUInt(uint value)
    {
        while (value > 0x7F)
        {
            _buf.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        _buf.Add((byte)value);
    }

    private void WriteVarULong(ulong value)
    {
        while (value > 0x7F)
        {
            _buf.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        _buf.Add((byte)value);
    }
}
