# Data Model: Fix Streaming List

## ExplorerSessionState

- **Purpose**: Captures the user-visible session state for the explorer across connection, browsing, selection, and live streaming.
- **Relevant fields**:
  - `ConnectionState`
  - `SelectionSet`
  - `ActiveStreamChannels`
  - active streaming snapshot or equivalent fixed-row view state
  - `LastStatusMessage`
- **Relationships**:
  - Owns zero or one active `StreamViewSnapshot` while streaming is in progress.
- **Validation rules**:
  - The active stream snapshot exists only during `Streaming` state.
  - The stream snapshot must be built from the exact selection set active when streaming starts.

## StreamViewSnapshot

- **Purpose**: Represents the complete fixed list shown to the user during one active streaming session.
- **Fields**:
  - `Rows`: ordered collection of `StreamRowSnapshot`
  - `StartedAtUtc`
  - `IsActive`
  - optional summary metadata such as row count or last refresh time
- **Relationships**:
  - Belongs to one `ExplorerSessionState` in `Streaming` state.
  - Contains one `StreamRowSnapshot` per selected endpoint.
- **Validation rules**:
  - Row count must equal the number of selected endpoints at stream start.
  - Rows must be sorted alphabetically by channel name when created.
  - Row order must remain stable for the remainder of the active session.
- **State transitions**:
  - `Initialized` when streaming starts and rows are created with waiting status.
  - `Updating` as channel events modify existing rows in place.
  - `Completed` when streaming stops and the snapshot is no longer updated.

## StreamRowSnapshot

- **Purpose**: Represents one persistent row in the fixed streaming list.
- **Fields**:
  - `ChannelId`
  - `ChannelName`
  - `SourceResourceUri`
  - `PrimaryIndexText`
  - `ValueText`
  - `StatusText`
  - `LastEventKind`
  - `LastUpdatedAtUtc`
- **Relationships**:
  - Belongs to one `StreamViewSnapshot`.
  - Is derived from one `SelectedEndpoint` and updated by zero or more `StreamLifecycleEvent` instances.
- **Validation rules**:
  - `ChannelId` must uniquely identify the row within one active stream snapshot.
  - `ChannelName` must be stable for the duration of the session.
  - Before the first data event, the row must show `Waiting for data` and an empty/default latest measurement representation.
  - After a remove event, the row remains visible and its last known measurement is preserved.

## RowStatusField

- **Purpose**: Dedicated per-row lifecycle state shown separately from index and value.
- **Allowed states**:
  - `Waiting for data`
  - `Live`
  - `Changed`
  - `Status: <value>` or equivalent status-change representation
  - `Ended` / `Removed`
- **Relationships**:
  - Associated with exactly one `StreamRowSnapshot`.
- **Validation rules**:
  - Must never replace the row’s latest index/value cells.
  - Must be updated deterministically from the latest relevant event for that row.

## StreamLifecycleEvent

- **Purpose**: Logical event that updates an existing stream row.
- **Cases**:
  - `Data`
  - `DataChange`
  - `StatusChange`
  - `Remove`
- **Relationships**:
  - Targets one `StreamRowSnapshot` via channel identity.
- **Validation rules**:
  - An event for an unknown channel must not create a surprise extra row in the fixed list.
  - Events may update row status, latest index, latest value, or some combination depending on event kind.

## SelectedEndpoint

- **Purpose**: Existing explorer selection entity that seeds the initial stream rows.
- **Relevant fields**:
  - `SelectionKey`
  - `Endpoint.ChannelId`
  - `Endpoint.ChannelName`
  - `Endpoint.SourceResourceUri`
- **Relationships**:
  - Each selected endpoint maps to exactly one initial `StreamRowSnapshot` when streaming starts.
- **Validation rules**:
  - Duplicate selected endpoints must not create duplicate stream rows.
