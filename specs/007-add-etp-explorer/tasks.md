---
description: "Task list for Add ETP Explorer"
---

# Tasks: Add ETP Explorer

**Input**: Design documents from `/specs/007-add-etp-explorer/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED. Include explorer application tests for startup validation, browsing, selection management, streaming lifecycle, output attribution, and clean shutdown.

**Organization**: Tasks are grouped by user story to preserve independently verifiable increments where practical.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g. `[US1]`, `[US2]`, `[US3]`)
- Include exact file paths in each task description

## Path Conventions

- **Library**: `src/EtpClient/`
- **Sample apps**: `samples/EtpClient.SampleConsole/`, `samples/EtpExplorer/`
- **Library tests**: `tests/EtpClient.UnitTests/`, `tests/EtpClient.IntegrationTests/`
- **Explorer tests**: `tests/EtpExplorer.Tests/`
- **Feature docs**: `specs/007-add-etp-explorer/`

---

## Phase 1: Setup

**Purpose**: Create the new explorer app and test-project scaffolding.

- [X] T001 Create the explorer sample project and test project in `samples/EtpExplorer/EtpExplorer.csproj` and `tests/EtpExplorer.Tests/EtpExplorer.Tests.csproj`
- [X] T002 Add explorer package and solution wiring in `Directory.Packages.props` and `EtpClient.slnx`
- [X] T003 [P] Create baseline explorer configuration files in `samples/EtpExplorer/appsettings.json` and `samples/EtpExplorer/appsettings.Development.json`
- [X] T004 [P] Create explorer test support scaffolding in `tests/EtpExplorer.Tests/TestSupport/TestConsoleCapture.cs` and `tests/EtpExplorer.Tests/TestSupport/FakeExplorerClient.cs`

**Checkpoint**: The repository can build the new explorer and its dedicated test project.

---

## Phase 2: Foundational

**Purpose**: Establish shared startup, state, UI, and orchestration seams required by all stories.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T005 Implement the explorer entry point and host wiring in `samples/EtpExplorer/Program.cs`
- [X] T006 [P] Implement secret-safe startup configuration validation in `samples/EtpExplorer/ExplorerOptions.cs`
- [X] T007 [P] Implement shared explorer state and endpoint models in `samples/EtpExplorer/ExplorerModels.cs`
- [X] T008 [P] Add an app-local ETP client seam and production adapter in `samples/EtpExplorer/IExplorerClient.cs` and `samples/EtpExplorer/EtpClientAdapter.cs`
- [X] T009 [P] Add the Spectre.Console UI abstraction and production implementation shell in `samples/EtpExplorer/IExplorerUi.cs` and `samples/EtpExplorer/SpectreExplorerUi.cs`
- [X] T010 Implement the central workflow controller for menu state, session lifecycle, and cancellation in `samples/EtpExplorer/ExplorerApp.cs`
- [X] T011 [P] Add foundational startup and validation tests in `tests/EtpExplorer.Tests/ExplorerOptionsTests.cs` and `tests/EtpExplorer.Tests/ExplorerProgramTests.cs`

**Checkpoint**: The explorer can start, validate secrets-backed configuration, and expose stable seams for browse, selection, and streaming workflows.

---

## Phase 3: User Story 1 - Select a Root Node and Browse Its Tree (Priority: P1) 🎯 MVP

**Goal**: Let the user choose one of the discovered root nodes after connect and browse the selected branch through a tree-based interactive console flow.

**Independent Test**: Run the explorer against a server or fake client that returns multiple root nodes, verify that the user is prompted to choose one root node such as `witsml14` or `witsml20`, then browse child resources in that selected tree with clear navigation context and no-content feedback.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] [US1] Add browse workflow tests for root-node discovery, root selection, child navigation, and no-content handling in `tests/EtpExplorer.Tests/ExplorerBrowseWorkflowTests.cs`
- [X] T013 [P] [US1] Add browse rendering tests for root-node prompts, tree context, status messaging, and navigation breadcrumbs in `tests/EtpExplorer.Tests/ExplorerBrowseRenderingTests.cs`

### Implementation for User Story 1

- [X] T014 [P] [US1] Implement root-node discovery mapping and browseable resource formatting in `samples/EtpExplorer/ExplorerBrowseService.cs`
- [X] T015 [P] [US1] Implement root selection, tree-navigation actions, and navigation-stack state transitions in `samples/EtpExplorer/ExplorerApp.cs`
- [X] T016 [US1] Implement Spectre.Console root-node prompts, tree browsing views, breadcrumbs, and empty-result feedback in `samples/EtpExplorer/SpectreExplorerUi.cs`
- [X] T017 [US1] Wire the initial connected root-selection flow and selected-tree browse menu in `samples/EtpExplorer/Program.cs` and `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: The explorer can connect, prompt for an available root node, and browse the selected tree without requiring hard-coded channel identifiers.

---

## Phase 4: User Story 2 - Select Streamable Endpoints (Priority: P2)

**Goal**: Let the user resolve streamable channels from browsed resources and maintain a multi-endpoint selection set before streaming starts.

**Independent Test**: Resolve streamable endpoints for a browsed resource, add and remove endpoints from the selection set, and verify that duplicates are prevented and the review screen reflects the current selection accurately.

### Tests for User Story 2 (REQUIRED) ⚠️

- [X] T018 [P] [US2] Add endpoint-resolution and multi-select tests in `tests/EtpExplorer.Tests/ExplorerSelectionWorkflowTests.cs`
- [X] T019 [P] [US2] Add selection review, deselect, and clear-all tests in `tests/EtpExplorer.Tests/ExplorerSelectionReviewTests.cs`

### Implementation for User Story 2

- [X] T020 [P] [US2] Implement channel-describe endpoint resolution and streamable filtering in `samples/EtpExplorer/ExplorerEndpointResolver.cs`
- [X] T021 [P] [US2] Implement selection-set management, deduplication, and review models in `samples/EtpExplorer/SelectionSetService.cs` and `samples/EtpExplorer/ExplorerModels.cs`
- [X] T022 [US2] Implement Spectre.Console multi-selection, review, remove, and clear flows in `samples/EtpExplorer/SpectreExplorerUi.cs`
- [X] T023 [US2] Integrate endpoint resolution and selection management into the selected-tree browse workflow with no-selection and non-streamable feedback in `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: The explorer can accumulate and manage one or more streamable endpoints before any live streaming begins.

---

## Phase 5: User Story 3 - Start and Observe Streaming Output (Priority: P3)

**Goal**: Start live streaming for the selected endpoints, render attributed output, and stop cleanly back to the interactive state.

**Independent Test**: Seed a non-empty selection set, start streaming with fake or real channel events, and verify that per-endpoint output is rendered, partial failures are surfaced clearly, and stop/exit returns the explorer to a clean connected or closed state.

### Tests for User Story 3 (REQUIRED) ⚠️

- [X] T024 [P] [US3] Add live streaming lifecycle tests for start, stop, cancellation, and partial failures in `tests/EtpExplorer.Tests/ExplorerStreamingWorkflowTests.cs`
- [X] T025 [P] [US3] Add rendered output attribution tests for multi-endpoint streams in `tests/EtpExplorer.Tests/ExplorerStreamRenderingTests.cs`

### Implementation for User Story 3

- [X] T026 [P] [US3] Implement subscription creation and stream lifecycle handling in `samples/EtpExplorer/ExplorerStreamingService.cs`
- [X] T027 [P] [US3] Implement stream-event formatting and endpoint attribution in `samples/EtpExplorer/StreamEventFormatter.cs`
- [X] T028 [US3] Implement Spectre.Console live streaming views, stop controls, and partial-failure messaging in `samples/EtpExplorer/SpectreExplorerUi.cs`
- [X] T029 [US3] Integrate start-stream, stop-stream, cancellation, and return-to-menu behavior in `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: The explorer can stream selected endpoints, attribute each event to its source, and stop cleanly.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finish documentation, verification, and cross-story cleanup.

- [X] T030 [P] Update explorer setup and usage details in `samples/EtpExplorer/appsettings.json` and `specs/007-add-etp-explorer/quickstart.md`
- [X] T031 [P] Add final explorer test-project build and coverage wiring in `tests/EtpExplorer.Tests/EtpExplorer.Tests.csproj` and `EtpClient.slnx`
- [X] T032 Validate the quickstart flow and user-secrets instructions against `samples/EtpExplorer/EtpExplorer.csproj` and `specs/007-add-etp-explorer/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion; delivers the MVP root-selection and tree-browse experience.
- **User Story 2 (Phase 4)**: Depends on Foundational completion and integrates with the selected-tree browse flow from User Story 1.
- **User Story 3 (Phase 5)**: Depends on Foundational completion and consumes the selection set produced by User Story 2.
- **Polish (Phase 6)**: Depends on the desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Independent after Phase 2.
- **User Story 2 (P2)**: Builds on the root-selection and browsing surfaces from US1 for UI flow, but selection logic remains independently testable with resolved endpoint fixtures.
- **User Story 3 (P3)**: Builds on the selection set from US2 for user flow, but the streaming lifecycle remains independently testable with seeded selections.

### Within Each User Story

- Tests must be written and fail before implementation.
- Service and model tasks should precede UI integration work.
- Workflow integration comes after lower-level resolution/formatting logic.
- Each story should be validated independently before moving on.

### Parallel Opportunities

- `T003` and `T004` can run in parallel during Setup.
- `T006`, `T007`, `T008`, `T009`, and `T011` can run in parallel during Foundational work.
- In US1, `T012` and `T013` can run together, then `T014` and `T015` can run together.
- In US2, `T018` and `T019` can run together, then `T020` and `T021` can run together.
- In US3, `T024` and `T025` can run together, then `T026` and `T027` can run together.

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests together:
Task: "Add browse workflow tests for root discovery, child navigation, and no-content handling in tests/EtpExplorer.Tests/ExplorerBrowseWorkflowTests.cs"
Task: "Add browse rendering tests for resource lists, status messaging, and navigation context in tests/EtpExplorer.Tests/ExplorerBrowseRenderingTests.cs"

# Launch US1 service/state tasks together after tests fail:
Task: "Implement discovery result mapping and browseable resource formatting in samples/EtpExplorer/ExplorerBrowseService.cs"
Task: "Implement browse actions and navigation-stack state transitions in samples/EtpExplorer/ExplorerApp.cs"
```

---

## Parallel Example: User Story 2

```bash
# Launch US2 tests together:
Task: "Add endpoint-resolution and multi-select tests in tests/EtpExplorer.Tests/ExplorerSelectionWorkflowTests.cs"
Task: "Add selection review, deselect, and clear-all tests in tests/EtpExplorer.Tests/ExplorerSelectionReviewTests.cs"

# Launch US2 core tasks together after tests fail:
Task: "Implement channel-describe endpoint resolution and streamable filtering in samples/EtpExplorer/ExplorerEndpointResolver.cs"
Task: "Implement selection-set management, deduplication, and review models in samples/EtpExplorer/SelectionSetService.cs and samples/EtpExplorer/ExplorerModels.cs"
```

---

## Parallel Example: User Story 3

```bash
# Launch US3 tests together:
Task: "Add live streaming lifecycle tests for start, stop, cancellation, and partial failures in tests/EtpExplorer.Tests/ExplorerStreamingWorkflowTests.cs"
Task: "Add rendered output attribution tests for multi-endpoint streams in tests/EtpExplorer.Tests/ExplorerStreamRenderingTests.cs"

# Launch US3 core tasks together after tests fail:
Task: "Implement subscription creation and stream lifecycle handling in samples/EtpExplorer/ExplorerStreamingService.cs"
Task: "Implement stream-event formatting and endpoint attribution in samples/EtpExplorer/StreamEventFormatter.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate root-node selection and child-resource tree navigation independently.
5. Demo the explorer as a root-selection and discovery-first terminal application before adding selection and streaming.

### Incremental Delivery

1. Setup + Foundational establish the app shell, Spectre seams, and secret-safe startup.
2. User Story 1 adds root-node selection plus interactive tree browsing and delivers the first usable explorer slice.
3. User Story 2 adds multi-endpoint selection and review without changing the browse contract.
4. User Story 3 adds live streaming and endpoint-attributed rendering on top of the saved selection set.
5. Polish verifies the user-secrets quickstart and final solution wiring.

### Suggested MVP Scope

Deliver Phases 1 through 3 first. That yields a working explorer that connects, prompts for an available root node, browses the selected tree, and demonstrates the Spectre.Console-based navigation model before more stateful selection and streaming behavior is layered on.
