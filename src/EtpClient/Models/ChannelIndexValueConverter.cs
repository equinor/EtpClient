namespace EtpClient.Models;

/// <summary>Categorizes how a raw primary index value was interpreted.</summary>
public enum ChannelIndexKind
{
    /// <summary>The raw value was interpreted as a UTC-based timestamp.</summary>
    Time,

    /// <summary>The raw value was interpreted as a scaled physical depth.</summary>
    Depth,

    /// <summary>
    /// The raw value could not be interpreted as time or depth
    /// (unsupported index type, missing metadata, or inconsistent state).
    /// </summary>
    Fallback,
}

/// <summary>
/// Represents the meaning of one raw primary index value after applying channel metadata.
/// Exactly one of <see cref="UtcTimestamp"/>, <see cref="ScaledDepthValue"/>,
/// or <see cref="FallbackValue"/> will be populated based on <see cref="Kind"/>.
/// </summary>
public sealed class InterpretedChannelIndex
{
    /// <summary>The original raw <see langword="long"/> value from the wire.</summary>
    public required long RawValue { get; init; }

    /// <summary>How the raw value was interpreted.</summary>
    public required ChannelIndexKind Kind { get; init; }

    /// <summary>
    /// UTC timestamp for time-indexed channels.
    /// Non-null when <see cref="Kind"/> is <see cref="ChannelIndexKind.Time"/>.
    /// </summary>
    public DateTimeOffset? UtcTimestamp { get; init; }

    /// <summary>
    /// Scaled decimal depth value for depth-indexed channels.
    /// Non-null when <see cref="Kind"/> is <see cref="ChannelIndexKind.Depth"/>.
    /// </summary>
    public decimal? ScaledDepthValue { get; init; }

    /// <summary>
    /// Unit of measure for the depth or fallback value, taken from <see cref="ChannelDefinition.IndexUom"/>.
    /// May be empty.
    /// </summary>
    public string DisplayUnit { get; init; } = string.Empty;

    /// <summary>
    /// String representation used when the index cannot be meaningfully converted as time or depth.
    /// Non-null when <see cref="Kind"/> is <see cref="ChannelIndexKind.Fallback"/>.
    /// </summary>
    public string? FallbackValue { get; init; }
}

/// <summary>
/// Converts raw primary index values from ETP Protocol 1 channel data into
/// typed <see cref="InterpretedChannelIndex"/> instances using channel metadata.
///
/// <para>
/// This is a library-level helper; final string formatting (local time, locale-aware
/// decimal separators) is left to the caller or the sample output layer.
/// </para>
/// </summary>
public static class ChannelIndexValueConverter
{
    private static readonly DateTimeOffset UnixEpoch = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Interprets a raw primary index value using the metadata from <paramref name="channel"/>.
    /// </summary>
    /// <param name="rawValue">The raw <c>long</c> index value from <see cref="ChannelDataItem.Indexes"/>.</param>
    /// <param name="channel">The channel definition that provides index metadata.</param>
    /// <returns>An <see cref="InterpretedChannelIndex"/> describing the interpreted value.</returns>
    public static InterpretedChannelIndex Interpret(long rawValue, ChannelDefinition channel)
    {
        return channel.IndexType switch
        {
            "Time" => InterpretAsTime(rawValue, channel),
            "Depth" => InterpretAsDepth(rawValue, channel),
            _ => InterpretAsFallback(rawValue, channel),
        };
    }

    private static InterpretedChannelIndex InterpretAsTime(long rawValue, ChannelDefinition channel)
    {
        // ETP v1.1: raw time index is a microsecond offset from timeDatum (or Unix epoch).
        var datum = UnixEpoch;

        if (!string.IsNullOrEmpty(channel.IndexTimeDatum))
        {
            if (DateTimeOffset.TryParse(
                    channel.IndexTimeDatum,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                datum = parsed;
            }
            // If the timeDatum string is malformed, fall back to Unix epoch silently.
        }

        // Convert microsecond offset to DateTimeOffset
        var utc = datum.AddMicroseconds(rawValue);

        return new InterpretedChannelIndex
        {
            RawValue = rawValue,
            Kind = ChannelIndexKind.Time,
            UtcTimestamp = utc,
            DisplayUnit = channel.IndexUom,
        };
    }

    private static InterpretedChannelIndex InterpretAsDepth(long rawValue, ChannelDefinition channel)
    {
        // ETP v1.1: depth index scaled by 10^scale.
        // scale=5 → divide by 100000; scale=3 → divide by 1000; scale=0 → no scaling.
        decimal scaled;
        if (channel.IndexScale == 0)
        {
            scaled = (decimal)rawValue;
        }
        else
        {
            var divisor = (decimal)Math.Pow(10, channel.IndexScale);
            scaled = (decimal)rawValue / divisor;
        }

        return new InterpretedChannelIndex
        {
            RawValue = rawValue,
            Kind = ChannelIndexKind.Depth,
            ScaledDepthValue = scaled,
            DisplayUnit = channel.IndexUom,
        };
    }

    private static InterpretedChannelIndex InterpretAsFallback(long rawValue, ChannelDefinition channel) =>
        new()
        {
            RawValue = rawValue,
            Kind = ChannelIndexKind.Fallback,
            FallbackValue = rawValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DisplayUnit = channel.IndexUom,
        };
}
