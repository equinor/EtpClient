# Quickstart: Fix Streaming List

## Goal

Verify that the explorer displays a fixed, alphabetically ordered channel list during streaming and updates each row in place rather than appending new lines.

## Prerequisites

- .NET 10 SDK installed.
- `samples/EtpExplorer` builds successfully.
- Explorer configuration is already set up through the existing user-secrets flow.
- `tests/EtpExplorer.Tests` is runnable from the repository root.

## Manual Validation Flow

1. Build and run the explorer.
2. Connect using the existing explorer configuration flow.
3. Browse and add at least three streamable endpoints whose channel names are not already alphabetically ordered in the selection set.
4. Start streaming.
5. Confirm that the stream view immediately shows exactly one row per selected endpoint, sorted alphabetically by channel name.
6. Before any data arrives for a row, confirm its status field shows `Waiting for data`.
7. Let live data arrive for multiple channels and confirm the existing rows update index/value in place rather than appending new lines.
8. Trigger or observe a status-change or remove event and confirm the existing row remains visible while only its dedicated status field changes.
9. Stop streaming and confirm the explorer returns to the connected interactive state cleanly.

## Automated Validation Flow

1. Run the explorer test project.
2. Confirm coverage includes:
   - fixed row count at stream start
   - alphabetical row ordering
   - `Waiting for data` initial state
   - repeated in-place updates for the same channel
   - dedicated status-field updates for status-change and remove events
   - stream stop returning the app to non-streaming state

## Expected Outcome

- Streaming begins with a stable list instead of scrolling line-by-line event output.
- The user can monitor latest values without losing row identity.
- Removed channels remain visible and clearly marked until streaming ends.
- Existing connection, browse, and selection workflows remain unchanged outside the active stream view.

## Acceptance Validation for SC-001

SC-001 requires a selection set of size $n$ to render exactly $n$ persistent rows with no additional rows appended during updates.

**Steps**:

1. Start the explorer and connect normally.
2. Add exactly 5 endpoints to the selection set.
3. Start streaming.
4. Confirm that exactly 5 rows appear immediately.
5. Allow several updates to arrive for at least 2 of those channels.
6. Confirm the row count remains 5 throughout the session.
7. Stop streaming.

**Pass criterion**: The streaming view never exceeds 5 rows and the existing rows update in place while data arrives.
