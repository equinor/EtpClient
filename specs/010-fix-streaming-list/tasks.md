# Tasks: Fix Streaming List

**Input**: Design documents from `specs/010-fix-streaming-list/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/explorer-fixed-stream-view.md`, `quickstart.md`

**Tests**: Automated explorer tests are required by the specification for fixed-row initialization, alphabetical ordering, waiting state, in-place updates, lifecycle status handling, and clean stream stop behavior.

**Organization**: Tasks are grouped by user story so each story can be implemented and verified independently once the foundational streaming snapshot seam is in place.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the sample-local models and test seam for fixed snapshot-based stream rendering.

- [X] T001 [P] Add fixed stream snapshot models and row status types in `samples/EtpExplorer/ExplorerModels.cs`
- [X] T002 [P] Add fixed stream snapshot capture helpers in `tests/EtpExplorer.Tests/TestSupport/FakeExplorerUi.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Replace append-only stream rendering with a shared snapshot/render seam that all user stories build on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Update the explorer UI contract to render full stream snapshots in `samples/EtpExplorer/IExplorerUi.cs`
- [X] T004 [P] Refactor event formatting to produce row-update inputs instead of append-only rendered rows in `samples/EtpExplorer/StreamEventFormatter.cs`
- [X] T005 [P] Add base stream snapshot creation and row lookup helpers in `samples/EtpExplorer/ExplorerStreamingService.cs`
- [X] T006 [P] Add a stable snapshot render path for the stream view in `samples/EtpExplorer/SpectreExplorerUi.cs`
- [X] T007 Refactor the streaming loop to publish snapshots through the new UI seam in `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: Snapshot-based streaming infrastructure is ready; user story work can now proceed.

---

## Phase 3: User Story 1 - Keep the Stream View Stable (Priority: P1) 🎯 MVP

**Goal**: Start streaming with one persistent row per selected endpoint and keep the list fixed and alphabetically ordered.

**Independent Test**: Select multiple endpoints, start streaming, and verify that the stream view shows exactly one row per selected endpoint, sorted alphabetically by channel name, without appending extra rows as updates arrive.

### Tests for User Story 1

- [X] T008 [P] [US1] Add fixed-row initialization and alphabetical ordering tests in `tests/EtpExplorer.Tests/ExplorerStreamRenderingTests.cs`
- [X] T009 [P] [US1] Add stream-start workflow tests for one-row-per-selection behavior in `tests/EtpExplorer.Tests/ExplorerStreamingWorkflowTests.cs`

### Implementation for User Story 1

- [X] T010 [US1] Implement initial snapshot creation with one row per selected endpoint and alphabetical ordering in `samples/EtpExplorer/ExplorerStreamingService.cs`
- [X] T011 [US1] Render the initial fixed stream table without append-only output in `samples/EtpExplorer/SpectreExplorerUi.cs`

**Checkpoint**: User Story 1 delivers a stable streaming list that can be demonstrated independently.

---

## Phase 4: User Story 2 - Read the Latest Values Quickly (Priority: P2)

**Goal**: Show `Waiting for data` before the first event and replace each row’s latest index/value in place as new data arrives.

**Independent Test**: Start streaming, confirm rows begin in `Waiting for data`, then send repeated updates for one or more channels and verify that the same rows update their displayed index and value instead of creating new rows.

### Tests for User Story 2

- [X] T012 [P] [US2] Add waiting-state and latest index/value rendering tests in `tests/EtpExplorer.Tests/ExplorerStreamRenderingTests.cs`
- [X] T013 [P] [US2] Add repeated-update workflow tests for in-place row replacement in `tests/EtpExplorer.Tests/ExplorerStreamingWorkflowTests.cs`

### Implementation for User Story 2

- [X] T014 [US2] Implement row updates for latest index/value and waiting-to-live transitions in `samples/EtpExplorer/ExplorerStreamingService.cs`
- [X] T015 [US2] Render `Waiting for data` and latest measurement cells in the fixed stream table in `samples/EtpExplorer/SpectreExplorerUi.cs`

**Checkpoint**: User Stories 1 and 2 now provide a readable monitoring view with stable rows and current values.

---

## Phase 5: User Story 3 - Preserve Context During Stream Lifecycle Changes (Priority: P3)

**Goal**: Keep rows visible and attributable when status-change or remove events occur, using a dedicated status field and clean stream shutdown behavior.

**Independent Test**: Start a stream, emit status-change and remove events for known channels, and verify that the existing rows remain visible, update only their dedicated status field, and stop updating cleanly when the stream ends.

### Tests for User Story 3

- [X] T016 [P] [US3] Add dedicated status-field and remove-persistence rendering tests in `tests/EtpExplorer.Tests/ExplorerStreamRenderingTests.cs`
- [X] T017 [P] [US3] Add workflow tests for removed rows remaining visible until stop in `tests/EtpExplorer.Tests/ExplorerStreamingWorkflowTests.cs`

### Implementation for User Story 3

- [X] T018 [US3] Implement dedicated status-field updates and remove-row persistence in `samples/EtpExplorer/ExplorerStreamingService.cs`
- [X] T019 [US3] Stop snapshot updates on stream end and preserve return-to-connected behavior in `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: All user stories are functional, with stable stream rows that remain understandable through lifecycle changes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final user-facing documentation and validation across the completed stories.

- [X] T020 [P] Update explorer streaming usage guidance for the fixed-row view in `README.md`
- [X] T021 Run the fixed-stream-view validation steps in `specs/010-fix-streaming-list/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup**: No dependencies; can start immediately.
- **Phase 2: Foundational**: Depends on Phase 1; blocks all user stories.
- **Phase 3: User Story 1**: Depends on Phase 2; establishes the MVP stream view.
- **Phase 4: User Story 2**: Depends on Phase 2 and can be added after the shared snapshot seam exists.
- **Phase 5: User Story 3**: Depends on Phase 2 and builds on the same fixed-row snapshot workflow.
- **Phase 6: Polish**: Depends on the desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational and defines the fixed row list.
- **User Story 2 (P2)**: Starts after Foundational and extends the fixed row list with latest-value behavior.
- **User Story 3 (P3)**: Starts after Foundational and extends the fixed row list with lifecycle-state handling.

### Within Each User Story

- Add or update the story’s tests before implementation.
- Implement model/update logic before final UI rendering or workflow integration.
- Validate the story independently before moving to the next priority.

### Parallel Opportunities

- `T001` and `T002` can run in parallel.
- After `T003`, `T004`, `T005`, and `T006` can proceed in parallel.
- Within each story, the rendering test task and workflow test task can run in parallel.
- Documentation (`T020`) can run in parallel with final quickstart validation (`T021`) once implementation is complete.

---

## Parallel Example: User Story 1

```bash
# Run User Story 1 tests in parallel:
Task: "T008 [US1] Add fixed-row initialization and alphabetical ordering tests in tests/EtpExplorer.Tests/ExplorerStreamRenderingTests.cs"
Task: "T009 [US1] Add stream-start workflow tests for one-row-per-selection behavior in tests/EtpExplorer.Tests/ExplorerStreamingWorkflowTests.cs"

# After the tests exist, implementation can split by file:
Task: "T010 [US1] Implement initial snapshot creation with one row per selected endpoint and alphabetical ordering in samples/EtpExplorer/ExplorerStreamingService.cs"
Task: "T011 [US1] Render the initial fixed stream table without append-only output in samples/EtpExplorer/SpectreExplorerUi.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate that streaming starts with a fixed alphabetical row list and no appended extra rows.

### Incremental Delivery

1. Land the shared snapshot/render seam.
2. Deliver User Story 1 for fixed row stability.
3. Add User Story 2 for latest measurement updates and waiting state.
4. Add User Story 3 for status/remove lifecycle handling.
5. Finish with README and quickstart validation.

### Parallel Team Strategy

1. One developer completes Phase 1 and `T003`.
2. Then foundational tasks `T004`-`T006` can split across team members.
3. After Phase 2, one developer can take rendering tests while another takes workflow tests for a story before converging on the implementation tasks.

---

## Notes

- `[P]` tasks touch different files and can run in parallel after dependencies are satisfied.
- User story labels map each task directly back to the feature spec.
- `README.md` is included explicitly because the feature changes a user-visible sample workflow.
- The fixed stream list is sample-local; no `src/EtpClient` protocol or API tasks are planned for this feature.