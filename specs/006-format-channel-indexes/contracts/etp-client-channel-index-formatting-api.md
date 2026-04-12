# Contract: ETP Client Channel Index Interpretation and Sample Output

## Purpose

Defines the additive public contract for preserving channel index metadata, interpreting raw primary index values, and rendering them consistently in the sample console for both live and historical channel output.

## Public API Contract

The feature does not replace existing Protocol 1 methods. It extends the channel metadata contract and adds reusable interpretation behavior for callers that want to convert raw index values.

### Required behavior

- `DescribeChannelsAsync` continues to return raw channel definitions and raw start/end indexes.
- `StartChannelStreamingAsync` and `RequestChannelRangeAsync` continue to return `ChannelDataItem` instances with raw `Indexes` values.
- The library preserves enough primary-index metadata from `ChannelMetadata` to interpret raw primary index values later.
- The library exposes reusable interpretation helpers that accept a `ChannelDefinition` plus a raw primary index value and return a typed time, depth, or fallback interpretation.
- Existing callers that rely on raw `long` index values remain compatible.

## ChannelDefinition Metadata Contract

### Required preserved fields

The public channel description contract must preserve the primary-index metadata needed for interpretation, including:

- index type
- index unit of measure
- index direction
- index scale for depth channels
- optional time datum for time channels
- optional depth datum for depth channels

### Semantics

- Time index metadata follows ETP `IndexMetadataRecord` semantics.
- Depth index metadata follows ETP `IndexMetadataRecord` semantics.
- Missing or unsupported metadata must not be invented; callers must be able to detect fallback conditions.

## Index Interpretation Contract

### Time interpretation

- A raw time index is interpreted as a microsecond offset.
- If a channel provides `timeDatum`, the raw value is added to that UTC datum.
- If `timeDatum` is absent, the raw value is added to the Unix epoch in UTC.
- The sample app renders the resulting instant in local time.

### Depth interpretation

- A raw depth index is interpreted by applying the producer-provided power-of-ten scale.
- The resulting scaled value uses the channel's depth UOM.
- Precision implied by the scale is preserved for display.

### Fallback interpretation

- When the channel metadata is missing, unsupported, or inconsistent, the helper returns a fallback interpretation rather than a misleading time or depth value.

## Sample Output Contract

### Live output

- Each live `ChannelDataItem` line includes the interpreted primary index, channel name, and value.
- Time-indexed channels display a local-time timestamp.
- Depth-indexed channels display a scaled depth value.
- Unsupported channels display a clear fallback representation.

### Range output

- Historical range output uses the same index interpretation rules as live output.
- Range summaries may remain, but the sample must also show the formatted sample lines or otherwise expose the interpreted index values visibly.

## Example Contract Scenarios

- Raw time index `1775845444000000` with no `timeDatum` is interpreted as a UTC-epoch-based value and rendered in local time.
- Raw depth index `403675000` with the matching preserved depth scale is interpreted as `4036,75` in a locale that uses comma decimals, with the configured depth UOM added by the sample output.
- A non-time and non-depth index stays in fallback form.

## Failure and Compatibility Contract

- Interpretation failures are local formatting/metadata outcomes, not connection failures.
- No credentials, auth headers, or other secrets are introduced into interpretation or output paths.
- Existing protocol message handling, async streaming semantics, and range correlation behavior remain unchanged.
