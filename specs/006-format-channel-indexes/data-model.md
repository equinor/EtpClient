# Data Model: Format Channel Index Output

## ChannelDefinition (extended)

- **Purpose**: Continues to represent one described Protocol 1 channel, now with enough primary-index metadata to interpret raw index values correctly.
- **Existing key fields used by this feature**:
  - `ChannelId`
  - `ChannelName`
  - `IndexType`
  - `IndexUom`
  - `IndexDirection`
- **New or newly preserved primary-index fields required by this feature**:
  - `IndexScale`: the power-of-ten scaling factor used for depth indexes.
  - `IndexTimeDatum`: optional UTC datum for time indexes; when absent, the Unix epoch is assumed.
  - `IndexDepthDatum`: optional depth datum identifier or URI.
  - `IndexMnemonic`: optional mnemonic for the primary index.
  - `IndexDescription`: optional human-readable description of the primary index.
- **Validation rules**:
  - `IndexType` remains the primary discriminator for time vs depth vs fallback behavior.
  - `IndexScale` is required for correct depth interpretation when `IndexType` is depth.
  - `IndexTimeDatum`, when present, must be preserved as a UTC ISO 8601 value from the wire metadata.
- **Relationships**:
  - One `ChannelDefinition` provides interpretation context for many `ChannelDataItem` instances.

## ChannelDataItem (unchanged raw payload)

- **Purpose**: Represents one decoded Protocol 1 sample while preserving the raw protocol index values.
- **Key fields**:
  - `Indexes`: ordered raw `long` values from the wire payload.
  - `ChannelId`
  - `Value`
- **Validation rules**:
  - The first entry in `Indexes` is treated as the primary index for this feature.
  - Raw values are preserved without presentation-side conversion.
- **Relationships**:
  - Each item can be interpreted only in combination with its channel's `ChannelDefinition`.

## InterpretedChannelIndex

- **Purpose**: Represents the meaning of one raw primary index after applying channel metadata.
- **Key fields**:
  - `RawValue`: original raw `long` value.
  - `Kind`: `Time`, `Depth`, or `Fallback`.
  - `UtcTimestamp`: optional interpreted UTC instant for time indexes.
  - `LocalTimestamp`: optional local-time view used by the sample.
  - `ScaledDepthValue`: optional decimal depth value after applying `IndexScale`.
  - `DisplayUnit`: optional unit suffix, typically the channel index UOM.
  - `FallbackValue`: string or numeric fallback representation when interpretation is not possible.
- **Validation rules**:
  - Exactly one of `UtcTimestamp`, `ScaledDepthValue`, or `FallbackValue` should be populated.
  - Time interpretation requires a valid datum or the default Unix epoch.
  - Depth interpretation requires a known scale and preserves meaningful precision.
- **State transitions**:
  - `Raw` → `Time`
  - `Raw` → `Depth`
  - `Raw` → `Fallback`

## FormattedSampleIndex

- **Purpose**: Final user-visible representation of a primary index as rendered by the sample app.
- **Key fields**:
  - `Text`
  - `Kind`
  - `RawValue`
- **Validation rules**:
  - Time values are rendered in local time for the current environment.
  - Depth values use the interpreted numeric value plus unit where appropriate.
  - Fallback values remain obvious as non-interpreted output.

## ChannelOutputSet

- **Purpose**: Represents the collection of printed samples for one channel within a live or range output pass.
- **Key fields**:
  - `ChannelDefinition`
  - `Samples`
  - `FormattingRule`
- **Validation rules**:
  - Every sample in the same output set uses the same primary-index interpretation rule.
  - The same channel metadata drives both live-stream and range output formatting.
