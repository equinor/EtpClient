using EtpClient.Models;

namespace EtpClient.UnitTests.Models;

/// <summary>
/// Unit tests for <see cref="ChannelIndexValueConverter"/>:
/// time interpretation, depth interpretation, and fallback behavior.
/// T008 [Foundational], T010 [US1], T017 [US2], T023 [US3].
/// Tests are written test-first (TDD).
/// </summary>
public sealed class ChannelIndexValueConverterTests
{
    // ── T008 [Foundational]: Basic construction and kind routing ─────────────

    [Fact]
    public void Interpret_TimeChannel_ReturnsKindTime()
    {
        var channel = TimeChannel();
        var result = ChannelIndexValueConverter.Interpret(1_000_000L, channel);
        Assert.Equal(ChannelIndexKind.Time, result.Kind);
    }

    [Fact]
    public void Interpret_DepthChannel_ReturnsKindDepth()
    {
        var channel = DepthChannel(scale: 5);
        var result = ChannelIndexValueConverter.Interpret(403_675_000L, channel);
        Assert.Equal(ChannelIndexKind.Depth, result.Kind);
    }

    [Fact]
    public void Interpret_UnknownIndexType_ReturnsKindFallback()
    {
        var channel = FallbackChannel();
        var result = ChannelIndexValueConverter.Interpret(42L, channel);
        Assert.Equal(ChannelIndexKind.Fallback, result.Kind);
    }

    [Fact]
    public void Interpret_PreservesRawValue()
    {
        var channel = TimeChannel();
        var result = ChannelIndexValueConverter.Interpret(1_775_845_444_000_000L, channel);
        Assert.Equal(1_775_845_444_000_000L, result.RawValue);
    }

    // ── T010 [US1]: Time index interpretation ────────────────────────────────

    [Fact]
    public void Interpret_TimeChannel_NoTimeDatum_UsesUnixEpoch()
    {
        // 1_000_000 microseconds = 1 second after Unix epoch
        var channel = TimeChannel(timeDatum: null);
        var result = ChannelIndexValueConverter.Interpret(1_000_000L, channel);

        Assert.NotNull(result.UtcTimestamp);
        Assert.Equal(new DateTimeOffset(1970, 1, 1, 0, 0, 1, TimeSpan.Zero), result.UtcTimestamp!.Value);
    }

    [Fact]
    public void Interpret_TimeChannel_WithTimeDatum_AddsOffsetToTimeDatum()
    {
        // timeDatum = 2020-01-01T00:00:00Z; raw = 2_000_000 µs = 2 s
        var datum = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var channel = TimeChannel(timeDatum: datum.ToString("O"));
        var result = ChannelIndexValueConverter.Interpret(2_000_000L, channel);

        var expected = datum.AddSeconds(2);
        Assert.NotNull(result.UtcTimestamp);
        Assert.Equal(expected, result.UtcTimestamp!.Value);
    }

    [Fact]
    public void Interpret_TimeChannel_SampleValue_UtcTimestampIsExpected()
    {
        // Spec example: raw = 1_775_845_444_000_000 µs from Unix epoch
        var channel = TimeChannel();
        var result = ChannelIndexValueConverter.Interpret(1_775_845_444_000_000L, channel);

        // 1_775_845_444_000_000 µs = 1_775_845_444 seconds
        var expected = DateTimeOffset.FromUnixTimeSeconds(1_775_845_444L);
        Assert.NotNull(result.UtcTimestamp);
        Assert.Equal(expected, result.UtcTimestamp!.Value);
    }

    [Fact]
    public void Interpret_TimeChannel_ScaledDepthValueIsNull()
    {
        var channel = TimeChannel();
        var result = ChannelIndexValueConverter.Interpret(1_000_000L, channel);
        Assert.Null(result.ScaledDepthValue);
    }

    // ── T017 [US2]: Depth index interpretation ────────────────────────────────

    [Fact]
    public void Interpret_DepthChannel_AppliesScale()
    {
        // scale=5 means raw/10^5. Raw=403_675_000 → 4036.75
        var channel = DepthChannel(scale: 5, indexUom: "m");
        var result = ChannelIndexValueConverter.Interpret(403_675_000L, channel);

        Assert.NotNull(result.ScaledDepthValue);
        Assert.Equal(4036.75m, result.ScaledDepthValue!.Value);
    }

    [Fact]
    public void Interpret_DepthChannel_PreservesDisplayUnit()
    {
        var channel = DepthChannel(scale: 3, indexUom: "ft");
        var result = ChannelIndexValueConverter.Interpret(1_000L, channel);

        Assert.Equal("ft", result.DisplayUnit);
    }

    [Fact]
    public void Interpret_DepthChannel_ScaleZero_UsesFallback()
    {
        // scale=0 means raw/1 which is unscaled — still valid
        var channel = DepthChannel(scale: 0, indexUom: "m");
        var result = ChannelIndexValueConverter.Interpret(1_234L, channel);

        Assert.Equal(ChannelIndexKind.Depth, result.Kind);
        Assert.NotNull(result.ScaledDepthValue);
        Assert.Equal(1234m, result.ScaledDepthValue!.Value);
    }

    [Fact]
    public void Interpret_DepthChannel_UtcTimestampIsNull()
    {
        var channel = DepthChannel(scale: 3);
        var result = ChannelIndexValueConverter.Interpret(1_000L, channel);
        Assert.Null(result.UtcTimestamp);
    }

    // ── T023 [US3]: Fallback behavior ────────────────────────────────────────

    [Fact]
    public void Interpret_FallbackChannel_FallbackValueIsRawLong()
    {
        var channel = FallbackChannel();
        var result = ChannelIndexValueConverter.Interpret(42L, channel);

        Assert.NotNull(result.FallbackValue);
        Assert.Equal("42", result.FallbackValue);
    }

    [Fact]
    public void Interpret_FallbackChannel_UtcTimestampIsNull()
    {
        var channel = FallbackChannel();
        var result = ChannelIndexValueConverter.Interpret(42L, channel);
        Assert.Null(result.UtcTimestamp);
    }

    [Fact]
    public void Interpret_FallbackChannel_ScaledDepthValueIsNull()
    {
        var channel = FallbackChannel();
        var result = ChannelIndexValueConverter.Interpret(42L, channel);
        Assert.Null(result.ScaledDepthValue);
    }

    [Fact]
    public void Interpret_TimeChannelWithMalformedTimeDatum_FallsBackToEpoch()
    {
        var channel = TimeChannel(timeDatum: "not-a-date");
        var result = ChannelIndexValueConverter.Interpret(1_000_000L, channel);
        // Falls back to Unix epoch when timeDatum can't be parsed
        Assert.Equal(ChannelIndexKind.Time, result.Kind);
        Assert.NotNull(result.UtcTimestamp);
        Assert.Equal(new DateTimeOffset(1970, 1, 1, 0, 0, 1, TimeSpan.Zero), result.UtcTimestamp!.Value);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ChannelDefinition TimeChannel(string? timeDatum = null) =>
        new()
        {
            ChannelId = 1L,
            ChannelUri = "eml://test/channel(T)",
            ChannelName = "T",
            DataType = "double",
            Uom = "rpm",
            IndexType = "Time",
            IndexUom = "us",
            IndexDirection = "Increasing",
            IndexScale = 0,
            IndexTimeDatum = timeDatum,
            Description = "",
            Status = "Active",
            Source = "test",
            MeasureClass = "",
        };

    private static ChannelDefinition DepthChannel(int scale, string indexUom = "m") =>
        new()
        {
            ChannelId = 2L,
            ChannelUri = "eml://test/channel(D)",
            ChannelName = "D",
            DataType = "double",
            Uom = "m/s",
            IndexType = "Depth",
            IndexUom = indexUom,
            IndexDirection = "Increasing",
            IndexScale = scale,
            Description = "",
            Status = "Active",
            Source = "test",
            MeasureClass = "",
        };

    private static ChannelDefinition FallbackChannel() =>
        new()
        {
            ChannelId = 3L,
            ChannelUri = "eml://test/channel(F)",
            ChannelName = "F",
            DataType = "double",
            Uom = "m/s",
            IndexType = "Passthrough",
            IndexUom = "n/a",
            IndexDirection = "Increasing",
            IndexScale = 0,
            Description = "",
            Status = "Active",
            Source = "test",
            MeasureClass = "",
        };
}
