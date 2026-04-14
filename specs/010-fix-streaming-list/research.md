# Phase 0 Research: Fix Streaming List

## Decision 1: Replace append-only rendered events with a session-local fixed-row snapshot

- **Decision**: Build and maintain a fixed stream-row snapshot for the active streaming session instead of sending one rendered output record directly to the UI for every incoming event.
- **Rationale**: The current explorer flow appends one rendered event per incoming message, which is the opposite of the requested behavior. A fixed-row snapshot keyed by channel identity is the simplest way to preserve row count, support in-place updates, and keep the stream view readable.
- **Alternatives considered**:
  - Keep append-only rendering and try to post-process terminal output. Rejected because terminal lines already emitted cannot be reliably turned into a stable row model.
  - Rebuild rows only inside `SpectreExplorerUi` from raw events. Rejected because testability and lifecycle reasoning belong above the concrete console renderer.

## Decision 2: Establish row order once at stream start and preserve it for the session

- **Decision**: Build the initial row set from the selected endpoints, sort it alphabetically by channel name, and preserve that order until the stream ends.
- **Rationale**: The clarified spec explicitly requires alphabetical ordering plus a fixed list. Sorting once at stream start gives a deterministic, user-friendly layout without row churn during live updates.
- **Alternatives considered**:
  - Preserve original selection order. Rejected by clarification.
  - Re-sort on every update. Rejected because it would violate the fixed-list usability goal and create visual instability.
  - Sort by most recent activity. Rejected because it optimizes for activity heat, not readability.

## Decision 3: Model waiting/live/changed/ended state as a dedicated row field

- **Decision**: Keep status in a dedicated row field that is updated independently from the latest index and latest value.
- **Rationale**: The clarified spec requires a dedicated status field. This keeps value/index cells semantically clean, preserves the last known value even after lifecycle events, and makes UI assertions far easier in tests.
- **Alternatives considered**:
  - Encode status into `ValueText`. Rejected because it overloads one cell with two meanings and can destroy the last useful measurement.
  - Show status only in transient status messages. Rejected because row-local lifecycle context must remain attributable to the correct channel.

## Decision 4: Keep removed channels visible and freeze their last known measurement

- **Decision**: When a `ChannelRemove` event arrives, leave the row in the fixed list, retain the last known index/value, and change only the dedicated status field to an ended or removed state.
- **Rationale**: The clarified spec requires removed channels to remain visible for the remainder of the session. Preserving the last known measurement gives the user context for what ended without causing row disappearance or accidental misattribution.
- **Alternatives considered**:
  - Delete the row immediately. Rejected by clarification and because it would break the fixed-list contract.
  - Blank the row contents when removed. Rejected because it discards useful context and makes historical monitoring harder.

## Decision 5: Extend the existing explorer UI seam to render row snapshots, not single events

- **Decision**: Change the explorer UI contract from rendering one `RenderedStreamEvent` at a time to rendering the current fixed stream view snapshot in a live-updating table or equivalent stable row presentation.
- **Rationale**: The current `RenderStreamEvent` method is designed for append-only output. A snapshot-oriented seam keeps the streaming workflow testable through `FakeExplorerUi` and allows `SpectreExplorerUi` to redraw the current state in place.
- **Alternatives considered**:
  - Keep both row-snapshot and single-event rendering paths indefinitely. Rejected because the feature has one intended presentation mode and dual paths add unnecessary complexity.
  - Push row aggregation entirely into `FakeExplorerUi` and leave production UI unchanged. Rejected because the actual product behavior must change, not just the tests.

## Decision 6: Reuse the existing explorer test suite with rendering and workflow extensions

- **Decision**: Extend `ExplorerStreamRenderingTests`, `ExplorerStreamingWorkflowTests`, and `FakeExplorerUi` rather than creating a separate test harness.
- **Rationale**: The explorer already has deterministic seams for streaming lifecycle and rendered output. Reusing them keeps the feature aligned with the current test architecture and minimizes setup churn.
- **Alternatives considered**:
  - Add terminal integration tests against Spectre.Console. Rejected because they would be slower and more brittle than the current fake-UI tests.
  - Rely only on manual validation. Rejected by the constitution and because UI state regressions are easy to miss without automation.
