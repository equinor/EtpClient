# Feature Specification: Format Channel Index Output

**Feature Branch**: `006-format-channel-indexes`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "Improve the sample output of index values by converting the raw values to either timestamps (DateTime/DateTimeOffset) for Time indexed channels and proper depth values for Depth indexed channels"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read Time-Indexed Samples Clearly (Priority: P1)

As a developer using the sample console to inspect channel data, I want time-based indexes to be shown as human-readable timestamps so I can quickly understand when each sample was recorded without manually decoding raw index values.

Example: a time index value of `1775845444000000` represents a UTC epoch value and should be displayed to the user in local time as a readable timestamp.

**Why this priority**: Time-indexed channels are the most common way to inspect streamed and ranged measurements, and raw numeric index output makes the sample harder to validate or demonstrate.

**Independent Test**: Can be fully tested by running the sample against a time-indexed channel and verifying that each printed index is shown as a readable timestamp that preserves the server-provided temporal meaning.

**Acceptance Scenarios**:

1. **Given** channel data from a time-indexed channel, **When** the sample prints the received measurements, **Then** each index is shown as a readable timestamp instead of an uninterpreted raw number.
2. **Given** time-indexed channel data that includes an explicit offset or timezone meaning, **When** the sample prints the measurements, **Then** the output preserves that temporal context in the displayed timestamp.
3. **Given** time-indexed channel data that cannot be interpreted confidently, **When** the sample prints the measurements, **Then** the output makes the fallback representation obvious rather than presenting misleading time information.
4. **Given** a time-indexed value of `1775845444000000` that represents a UTC epoch value, **When** the sample prints the measurement, **Then** the output shows the corresponding local-time timestamp in a human-readable format.

---

### User Story 2 - Read Depth-Indexed Samples Clearly (Priority: P2)

As a developer using the sample console to inspect channel data, I want depth-based indexes to be shown as proper depth values so I can understand the physical position of each sample without manually translating raw index values.

Example: a depth index value of `403675000` should be shown as `4036,75` in the sample output.

**Why this priority**: Depth-indexed channels are a core ETP use case, and demonstrating them clearly is essential for verifying channel range results and streamed data interpretation.

**Independent Test**: Can be fully tested by running the sample against a depth-indexed channel and verifying that each printed index is shown as a depth value that matches the channel's depth semantics.

**Acceptance Scenarios**:

1. **Given** channel data from a depth-indexed channel, **When** the sample prints the received measurements, **Then** each index is shown as a depth value that a developer can read directly.
2. **Given** a depth-indexed channel whose measurements include decimal precision, **When** the sample prints the received measurements, **Then** the displayed depth preserves the meaningful precision supplied by the data.
3. **Given** a depth-indexed channel whose depth basis is known from the channel metadata, **When** the sample prints the received measurements, **Then** the output uses that metadata consistently for every displayed index in the same result set.
4. **Given** a depth-indexed value of `403675000`, **When** the sample prints the measurement, **Then** the output shows `4036,75` as the human-readable depth.

---

### User Story 3 - Keep Non-Time and Non-Depth Output Trustworthy (Priority: P3)

As a developer using the sample console across different channel types, I want the sample to avoid over-formatting indexes it does not understand so I can trust that displayed values are meaningful and not guessed.

**Why this priority**: Incorrectly beautifying unsupported index types would make the sample misleading, which is worse than leaving a raw value in place.

**Independent Test**: Can be fully tested by running the sample against channel data whose index type is not recognized as time or depth and verifying that the output remains clear, stable, and non-misleading.

**Acceptance Scenarios**:

1. **Given** channel data whose index type is neither time nor depth, **When** the sample prints the measurements, **Then** the sample preserves a clear fallback representation rather than forcing timestamp or depth formatting.
2. **Given** channel metadata is missing or incomplete, **When** the sample prints the measurements, **Then** the output remains readable and does not claim a time or depth interpretation it cannot support.

### Edge Cases

- A channel reports time-oriented indexes, but an individual index value is missing, malformed, or outside the supported display range.
- A depth-indexed channel includes negative values, zero, or unusually large values that are still valid for the data set.
- A result set mixes channels with different index meanings in the same sample run.
- The sample receives indexes for a channel before the metadata needed to classify the index meaning is available.
- The channel uses an index meaning other than time or depth and should remain in fallback form.
- Multiple values in the same output set require consistent formatting even when precision or textual width differs.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The sample console MUST determine whether a channel index should be presented as time, depth, or fallback output using the channel information already available during channel description, live streaming, or range retrieval flows.
- **FR-002**: For time-indexed channels, the sample console MUST display each index as a human-readable timestamp instead of an uninterpreted raw numeric value.
- **FR-003**: For time-indexed channels, the sample console MUST preserve the temporal meaning of the source index, including any offset or timezone context that is available from the data or channel metadata, and present UTC epoch examples such as `1775845444000000` in local time.
- **FR-004**: For depth-indexed channels, the sample console MUST display each index as a human-readable depth value instead of an uninterpreted raw numeric value.
- **FR-005**: For depth-indexed channels, the sample console MUST preserve meaningful numeric precision supplied by the source data and avoid truncating significant depth information, including values such as `403675000` that should display as `4036,75m`.
- **FR-006**: The sample console MUST apply the same index-presentation rules to both live-streamed output and historical range output.
- **FR-007**: When the sample console cannot confidently classify or convert an index value, it MUST fall back to a clear non-misleading representation rather than displaying an incorrect timestamp or depth value.
- **FR-008**: The sample console MUST keep index formatting consistent for all values in the same channel result set.
- **FR-009**: The feature MUST include automated coverage for time-index formatting, depth-index formatting, and fallback behavior for unsupported or incomplete metadata scenarios.

### Key Entities *(include if feature involves data)*

- **Channel Index Meaning**: The interpretation of a channel's index axis, such as time, depth, or another unsupported type, derived from the channel information available to the sample.
- **Formatted Sample Index**: The user-visible representation of an individual channel index value as printed by the sample console.
- **Channel Output Set**: The collection of printed measurements for a described, streamed, or ranged channel result, which must use one consistent index-presentation rule.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, 100% of covered time-indexed sample outputs present readable timestamps instead of raw numeric indexes.
- **SC-002**: In acceptance testing, 100% of covered depth-indexed sample outputs present readable depth values without losing meaningful precision.
- **SC-003**: In acceptance testing, 100% of covered live-stream and range-output scenarios use the same index-presentation rules for the same channel type.
- **SC-004**: In acceptance testing, 100% of covered unsupported or incomplete-metadata scenarios fall back to a clear non-misleading representation.
- **SC-005**: A developer reviewing sample output can distinguish time-indexed, depth-indexed, and fallback-formatted channels without referring to raw protocol payloads.

## Assumptions

- Scope is limited to the sample console's presentation of already-received channel data; no protocol negotiation changes are required.
- Channel description or data messages already provide enough information to classify the important covered index meanings for this feature.
- Time and depth are the only index meanings that require specialized formatting in this feature; other index meanings remain on the fallback path.
- The sample output examples currently in scope are a UTC epoch time value of `1775845444000000`, which should display in local time, and a depth value of `403675000`, which should display as `4036,75m`.
- Existing channel streaming and range workflows remain unchanged apart from the way indexes are presented to the user.
- Automated tests will cover both formatted output and fallback behavior for representative sample data.
