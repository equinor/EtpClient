# Feature Specification: Fix Streaming List

**Feature Branch**: `010-fix-streaming-list`  
**Created**: 2026-04-14  
**Status**: Draft  
**Input**: User description: "When starting to stream data using the exploration app, the list of channels should stay fixed and just update index and values in place"

## Clarifications

### Session 2026-04-14

- Q: What should happen to a row when a channel is removed or stops producing data? → A: Keep the row visible for the rest of the streaming session and mark it as ended/removed.
- Q: How should rows be ordered during streaming? → A: Sort rows alphabetically by channel name before showing the fixed list.
- Q: What should a row show before the first event arrives? → A: Show `Waiting for data` until the first event arrives.
- Q: How should stream lifecycle state be shown in each row? → A: Give each row a dedicated status field.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Keep the Stream View Stable (Priority: P1)

As a user who starts live streaming in the explorer, I want the selected channel list to remain fixed on screen so I can keep my attention on the same rows instead of chasing new lines as data arrives.

**Why this priority**: A stable stream view is the core usability improvement. If the row list keeps shifting or growing during live updates, the stream becomes hard to read and defeats the purpose of monitoring multiple channels at once.

**Independent Test**: Can be fully tested by selecting multiple endpoints, starting streaming, and confirming that the stream view shows one persistent row per selected channel instead of appending a new row for every incoming update.

**Acceptance Scenarios**:

1. **Given** the user has selected multiple endpoints for streaming, **When** streaming starts, **Then** the explorer displays a fixed list containing those selected channels sorted alphabetically by channel name.
2. **Given** the stream is active and repeated data arrives for an already visible channel, **When** the explorer refreshes the display, **Then** the existing row for that channel is updated in place rather than adding another row.
3. **Given** the stream is active for several channels, **When** updates arrive at different times, **Then** the row set remains stable and does not grow beyond the channels included in the active stream.

---

### User Story 2 - Read the Latest Values Quickly (Priority: P2)

As a user monitoring live data, I want each channel row to show the latest index and latest value for that channel so I can understand current conditions at a glance.

**Why this priority**: Once the layout is stable, the next most important outcome is that each row clearly reflects the freshest data available for that channel.

**Independent Test**: Can be fully tested by starting streaming for one or more channels, sending multiple updates for each channel, and confirming that the visible index and value for each row always reflect the newest event received for that channel.

**Acceptance Scenarios**:

1. **Given** a channel row is visible in the active stream view, **When** the first live data event for that channel arrives, **Then** the row shows the event's primary index and value.
2. **Given** a channel row already shows prior data, **When** a newer event for that channel arrives, **Then** the same row replaces the previous index and value with the latest ones.
3. **Given** some streamed channels have received data and others have not, **When** the user views the fixed list, **Then** each row clearly distinguishes between channels with current values and channels showing `Waiting for data`.

---

### User Story 3 - Preserve Context During Stream Lifecycle Changes (Priority: P3)

As a user monitoring a live stream, I want the fixed channel list to stay understandable when channels stop, change status, or the user stops streaming so I do not lose context about what happened.

**Why this priority**: Stable rows only help if the stream lifecycle remains legible when a channel ends or changes state.

**Independent Test**: Can be fully tested by starting a stream, receiving status-change and remove events, then stopping the stream and confirming that the user can still understand which channel each update applied to and that the stream exits cleanly.

**Acceptance Scenarios**:

1. **Given** a fixed stream list is active, **When** a channel status-change event arrives, **Then** the existing row updates its dedicated status field without reordering or duplicating the list.
2. **Given** a fixed stream list is active, **When** a channel remove event arrives, **Then** the existing row remains visible, updates its dedicated status field to an ended or removed state, and the rest of the list remains stable.
3. **Given** the user stops streaming, **When** the stream session ends, **Then** the explorer exits the active stream view without leaving behind a misleading partially updated list.

### Edge Cases

- The user starts streaming with only one selected endpoint.
- A selected channel has not yet produced any live data after streaming starts.
- Multiple updates for the same channel arrive in rapid succession.
- A status-change or remove event arrives before the first data event for a channel.
- Two selected channels have similar names and must still remain distinguishable in the fixed list.
- The user stops streaming while one or more channels are mid-update.
- The server ends streaming for one channel while other selected channels continue producing data.
- The fixed list contains channels whose latest values are blank, unavailable, or non-numeric.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The explorer MUST present one persistent stream row for each endpoint included when the user starts a streaming session.
- **FR-002**: The explorer MUST keep the set of visible stream rows fixed for the duration of the active streaming session unless the stream itself ends.
- **FR-003**: The explorer MUST sort the fixed stream rows alphabetically by channel name when the streaming session starts and MUST preserve that order for the rest of the active session.
- **FR-004**: The explorer MUST update an existing row in place when a new event arrives for a channel already represented in the fixed stream list.
- **FR-005**: The explorer MUST show the latest available primary index for each streamed channel in that channel's existing row.
- **FR-006**: The explorer MUST show the latest available value for each streamed channel in that channel's existing row.
- **FR-007**: The explorer MUST show `Waiting for data` for selected channels that are part of the fixed stream list but have not yet received any data.
- **FR-008**: Each stream row MUST include a dedicated status field that remains separate from the displayed index and value.
- **FR-009**: The explorer MUST preserve channel identity in the fixed list so users can tell which row corresponds to each selected endpoint throughout streaming.
- **FR-010**: The explorer MUST handle non-data stream events for a channel by updating that channel's dedicated status field rather than adding unrelated rows.
- **FR-011**: When a remove event is received for a streamed channel, the explorer MUST keep that channel's row visible for the rest of the active streaming session and mark its dedicated status field as ended or removed.
- **FR-012**: The explorer MUST keep the fixed stream list readable when updates arrive for different channels at different rates.
- **FR-013**: The explorer MUST stop updating the fixed stream list once the active streaming session has ended.
- **FR-014**: The feature MUST include automated coverage for stream startup, alphabetical row ordering, repeated in-place updates, `Waiting for data` empty-row state, dedicated row status updates, mixed channel update timing, and stream lifecycle events that affect existing rows, including removed channels that remain visible.
- **FR-015**: The explorer MUST display the primary index for each streamed channel in a human-readable format: time indexes as a local-time timestamp, depth indexes as a scaled physical depth with unit suffix.
- **FR-016**: Numeric channel values displayed in the stream view MUST be formatted with at most two decimal places.

### Key Entities *(include if feature involves data)*

- **Streaming Session**: One active live-monitoring run started from the explorer for the currently selected endpoints.
- **Selected Endpoint**: A channel chosen by the user before streaming begins and therefore entitled to one fixed row in the stream view.
- **Stream Row**: The persistent on-screen representation of one selected endpoint during an active streaming session.
- **Latest Channel Snapshot**: The most recent index, value, and stream-state information known for one streamed channel.
- **Stream Lifecycle Event**: A data, status-change, or remove event that updates the information shown for an existing stream row.
- **Ended Stream Row**: A persistent stream row whose channel has received a remove event and is still displayed with an ended or removed state until the streaming session ends.
- **Row Status Field**: A dedicated per-row field that communicates whether a streamed channel is waiting, live, changed, ended, or otherwise lifecycle-annotated without replacing the row's index or value cells.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, starting a stream for any selection set of size $n$ displays exactly $n$ persistent channel rows and does not append extra rows as updates arrive.
- **SC-002**: In acceptance testing, 100% of repeated update scenarios replace the visible index and value in the correct existing row for that channel.
- **SC-003**: In acceptance testing, users can identify the latest value for any streamed channel in under 5 seconds while at least 5 channels are updating concurrently.
- **SC-004**: In acceptance testing, 100% of covered lifecycle-event scenarios keep the channel list stable and attributable to the correct rows.
- **SC-005**: In acceptance testing, stopping a stream ends further row updates immediately and returns the explorer to a non-streaming state without stale ongoing changes.

## Assumptions

- The explorer already has a streaming mode that starts from the current selection set.
- The selected endpoints at stream start determine the initial set of channels the user expects to monitor.
- Scope is limited to the exploration app's streaming presentation behavior, not to transport, protocol, or server-side streaming semantics.
- The fixed list is intended to improve readability of live monitoring rather than to add historical replay or charting behavior.
- Existing connection, discovery, and endpoint-selection workflows remain unchanged unless needed to support the fixed stream display.
- The feature will include the automated tests needed to verify stable rows, in-place updates, empty initial state handling, and lifecycle-event behavior.
