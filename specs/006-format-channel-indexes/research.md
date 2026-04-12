# Research: Format Channel Index Output

## Decision 1: Use a hybrid interpretation model instead of changing `ChannelDataItem` into specialized subclasses

- **Decision**: Preserve raw `long` index values in `ChannelDataItem.Indexes`, keep protocol decoding unchanged at the data-item level, and interpret those raw values later using channel metadata.
- **Rationale**: `ChannelData` carries raw `<index,value>` tuples, and the current public model already exposes `Indexes` as `IReadOnlyList<long>`. Replacing that with `TimeChannelDataItem` or `DepthChannelDataItem` variants would ripple through the public contract, session manager, codecs, tests, and live-stream enumeration for a feature whose scope is sample output formatting.
- **Alternatives considered**:
  - Specialize `ChannelDataItem` into time/depth subclasses early in the streaming chain: rejected because it couples protocol transport models to presentation concerns and makes multi-index or unknown-index scenarios harder to represent.
  - Replace raw indexes with preformatted strings during decode: rejected because it discards the protocol value that callers may still need for range requests, assertions, or their own formatting.

## Decision 2: Extend `ChannelDefinition` with the primary-index metadata required for correct interpretation

- **Decision**: Add primary-index metadata fields to `ChannelDefinition` so both binary and JSON metadata decoding preserve the information required to interpret raw index values correctly.
- **Rationale**: ETP v1.1 `IndexMetadataRecord` defines `indexType`, `uom`, `depthDatum`, `scale`, and `timeDatum`. The current model keeps only `IndexType`, `IndexUom`, and `IndexDirection`, which is insufficient for correct conversions. Depth formatting needs `scale`, and time formatting needs `timeDatum` when present, otherwise the Unix epoch in UTC.
- **Alternatives considered**:
  - Infer conversions from `IndexType` and `IndexUom` alone: rejected because it cannot correctly transform scaled depth values or non-epoch time channels.
  - Store only the final formatted string in `ChannelDefinition`: rejected because formatting depends on the specific raw index value, current culture, and local timezone at display time.

## Decision 3: Put conversion logic in a reusable library helper, but keep final string formatting in the sample

- **Decision**: Introduce a reusable conversion utility in the library that converts a raw primary index and channel metadata into an interpreted time or depth value, while leaving final string rendering to the sample output path.
- **Rationale**: The conversion rules are domain logic and are reusable outside the sample app. By contrast, local-time rendering and localized decimal output are presentation concerns. This split keeps the library useful to callers while avoiding hard-coding locale-specific strings in the core client.
- **Alternatives considered**:
  - Keep all conversion and formatting logic inside `SampleOutputWriter`: rejected because the same rules could be useful to library consumers and would be duplicated if range/live output paths diverged.
  - Add helper methods directly on `EtpClient`: rejected because the behavior is value interpretation rather than session orchestration, so it fits better as a model/protocol utility than as client instance behavior.

## Decision 4: Format indexes at one output boundary for both live and range results

- **Decision**: Route both live-stream sample lines and historical range sample lines through one index-formatting path in the sample output layer.
- **Rationale**: The specification requires consistent presentation across live and range output. The current sample already prints live data item lines, but range output only prints summary information. A shared formatting path avoids one-off behavior and ensures the same channel metadata drives both representations.
- **Alternatives considered**:
  - Format live output only: rejected because it would violate the spec requirement to apply the same rules to historical range output.
  - Add separate live and range formatting implementations: rejected because it increases regression risk and makes culture/timezone behavior easier to drift.

## Decision 5: Use the ETP index semantics exactly as defined by `IndexMetadataRecord`

- **Decision**: Interpret raw time indexes as microsecond offsets from `timeDatum` when present, otherwise from the Unix epoch in UTC; interpret depth indexes by applying the power-of-ten `scale` before formatting with the producer-provided UOM.
- **Rationale**: ETP v1.1 `IndexMetadataRecord` explicitly defines the semantics for `timeDatum`, `uom`, and `scale`. This directly supports the concrete examples in the spec: `1775845444000000` should be treated as a UTC epoch-based microsecond value and displayed in local time, while `403675000` can only become `4036,75m` if the relevant depth scale is preserved and applied before display.
- **Alternatives considered**:
  - Treat time indexes as milliseconds because the sample test data currently uses `ms`: rejected because the protocol definition states microseconds for time or relative-time indexes.
  - Format depth by inserting a decimal separator heuristically: rejected because correct placement depends on `scale`, not on string length guessing.

## Decision 6: Keep the feature additive and non-breaking for existing callers

- **Decision**: Keep `ChannelDataItem.Indexes` as raw `long` values and add metadata/helper capabilities without changing existing describe, streaming, or range method signatures.
- **Rationale**: The feature improves interpretation and sample output without changing protocol flow, connection behavior, or async streaming semantics. Existing callers that already consume raw indexes should continue to work unchanged.
- **Alternatives considered**:
  - Change public method return shapes to surface preinterpreted index values only: rejected because it would be a breaking API change with little benefit for the spec.
  - Add a mandatory formatting mode to all public APIs: rejected because most callers likely want to control their own display behavior.
