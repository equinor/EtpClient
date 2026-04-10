using System.Text;

namespace EtpClient.Protocol;

/// <summary>
/// Minimal Avro binary decoder matching <see cref="AvroWriter"/>.
/// Supports all primitive types needed by ETP Protocol 0 and includes
/// skip helpers for complex types not needed by the minimal connection slice.
/// </summary>
internal sealed class AvroReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _pos;

    public AvroReader(ReadOnlyMemory<byte> data) => _data = data;

    /// <summary>Gets the current read position for speculative parsing.</summary>
    public int Position => _pos;

    /// <summary>Restores the current read position after speculative parsing.</summary>
    public void Reset(int position)
    {
        if (position < 0 || position > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        _pos = position;
    }

    // ── int ───────────────────────────────────────────────────────────────────

    /// <summary>Reads an Avro int (zigzag + base-128 variable-length).</summary>
    public int ReadInt()
    {
        uint raw = ReadVarUInt();
        // Reverse zigzag: even → positive, odd → negative
        return (int)((raw >> 1) ^ -(raw & 1));
    }

    // ── long ──────────────────────────────────────────────────────────────────

    /// <summary>Reads an Avro long (zigzag + base-128 variable-length).</summary>
    public long ReadLong()
    {
        ulong raw = ReadVarULong();
        return (long)((raw >> 1) ^ (ulong)(-(long)(raw & 1)));
    }

    // ── float / double ────────────────────────────────────────────────────────

    /// <summary>Reads an Avro double (8 bytes, little-endian IEEE 754).</summary>
    public double ReadDouble()
    {
        EnsureAvailable(8);
        var bytes = _data.Span.Slice(_pos, 8).ToArray();
        _pos += 8;
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>Reads an Avro float (4 bytes, little-endian IEEE 754).</summary>
    public float ReadFloat()
    {
        EnsureAvailable(4);
        var bytes = _data.Span.Slice(_pos, 4).ToArray();
        _pos += 4;
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    // ── string ────────────────────────────────────────────────────────────────

    /// <summary>Reads an Avro string: reads byte count then UTF-8 bytes.</summary>
    public string ReadString()
    {
        var count = (int)ReadLong();
        if (count == 0) return string.Empty;
        EnsureAvailable(count);
        var span = _data.Span.Slice(_pos, count);
        _pos += count;
        return Encoding.UTF8.GetString(span);
    }

    // ── bytes ─────────────────────────────────────────────────────────────────

    /// <summary>Reads Avro bytes: reads byte count then raw bytes.</summary>
    public byte[] ReadBytes()
    {
        var count = (int)ReadLong();
        if (count == 0) return [];
        EnsureAvailable(count);
        var result = _data.Span.Slice(_pos, count).ToArray();
        _pos += count;
        return result;
    }

    // ── fixed ─────────────────────────────────────────────────────────────────

    /// <summary>Reads exactly <paramref name="length"/> bytes (no length prefix).</summary>
    public byte[] ReadFixed(int length)
    {
        EnsureAvailable(length);
        var result = _data.Span.Slice(_pos, length).ToArray();
        _pos += length;
        return result;
    }

    // ── bool ──────────────────────────────────────────────────────────────────

    /// <summary>Reads an Avro boolean (single byte).</summary>
    public bool ReadBool()
    {
        EnsureAvailable(1);
        var value = _data.Span[_pos] != 0;
        _pos++;
        return value;
    }

    /// <summary>Reads an Avro array of doubles wrapped in the ETP ArrayOfDouble record.</summary>
    public double[] ReadArrayOfDouble()
    {
        var values = new List<double>();
        long count;
        while ((count = ReadBlockCount()) != 0)
        {
            for (long i = 0; i < count; i++)
                values.Add(ReadDouble());
        }

        return values.ToArray();
    }

    // ── array / map blocks ────────────────────────────────────────────────────

    /// <summary>
    /// Reads the block count for an Avro array or map block.
    /// Returns 0 when the terminating block is reached.
    /// Negative count means a byte count follows (read and discard it).
    /// </summary>
    public long ReadBlockCount()
    {
        var count = ReadLong();
        if (count < 0)
        {
            ReadLong(); // byte count hint — discard
            count = -count;
        }
        return count;
    }

    // ── skip helpers ─────────────────────────────────────────────────────────

    /// <summary>Skips an Avro string value.</summary>
    public void SkipString()
    {
        var count = (int)ReadLong();
        EnsureAvailable(count);
        _pos += count;
    }

    /// <summary>Skips an Avro int value.</summary>
    public void SkipInt() => ReadInt();

    /// <summary>Skips an Avro long value.</summary>
    public void SkipLong() => ReadLong();

    /// <summary>Skips an Avro bool value.</summary>
    public void SkipBool() => _pos++;

    /// <summary>Skips Avro bytes value.</summary>
    public void SkipBytes()
    {
        var count = (int)ReadLong();
        EnsureAvailable(count);
        _pos += count;
    }

    /// <summary>Skips <paramref name="length"/> fixed bytes.</summary>
    public void SkipFixed(int length) => _pos += length;

    /// <summary>
    /// Skips an ETP DataValue union using the v1.1 core/schema ordering.
    /// This is used during session negotiation when skipping protocolCapabilities maps.
    /// </summary>
    public void SkipDataValue()
    {
        var index = ReadLong(); // union discriminator
        switch (index)
        {
            case 0:  // null — no bytes
                break;
            case 1:  // double
                EnsureAvailable(8);
                _pos += 8;
                break;
            case 2:  // float
                EnsureAvailable(4);
                _pos += 4;
                break;
            case 3:  // int
                SkipInt();
                break;
            case 4:  // long
                SkipLong();
                break;
            case 5:  // string
                SkipString();
                break;
            case 6:  // vector (ArrayOfDouble)
                ReadArrayOfDouble();
                break;
            case 7:  // boolean
                SkipBool();
                break;
            case 8:  // bytes
                SkipBytes();
                break;
            default:
                throw new InvalidOperationException($"Unsupported DataValue union index {index}.");
        }
    }

    /// <summary>Skips an entire Avro map of string → DataValue entries.</summary>
    public void SkipStringDataValueMap()
    {
        long count;
        while ((count = ReadBlockCount()) > 0)
        {
            for (var i = 0; i < count; i++)
            {
                SkipString();    // key
                SkipDataValue(); // value
            }
        }
    }

    /// <summary>Skips an entire Avro array of string items.</summary>
    public void SkipStringArray()
    {
        long count;
        while ((count = ReadBlockCount()) > 0)
            for (var i = 0; i < count; i++) SkipString();
    }

    // ── internal helpers ─────────────────────────────────────────────────────

    private uint ReadVarUInt()
    {
        uint result = 0;
        int shift = 0;
        byte b;
        var span = _data.Span;
        do
        {
            EnsureAvailable(1);
            b = span[_pos++];
            result |= (uint)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    private ulong ReadVarULong()
    {
        ulong result = 0;
        int shift = 0;
        byte b;
        var span = _data.Span;
        do
        {
            EnsureAvailable(1);
            b = span[_pos++];
            result |= (ulong)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    private void EnsureAvailable(int length)
    {
        if (length < 0 || _pos + length > _data.Length)
            throw new InvalidOperationException("Unexpected end of Avro payload while decoding ETP message.");
    }
}
