---
description: "Task list for Search Active Explorer Column"
---

# Tasks: Search Active Explorer Column

**Input**: Design documents from `specs/008-search-column-filter/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED because the spec explicitly requires automated coverage for plain-text matches, wildcard matches, no-result feedback, clearing behavior, active-column scoping, and selection behavior during filtered browsing.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g. `[US1]`, `[US2]`, `[US3]`)
- Include exact file paths in each task description

## Path Conventions

- **Explorer sample**: `samples/EtpExplorer/`
- **Explorer tests**: `tests/EtpExplorer.Tests/`
- **Feature docs**: `specs/008-search-column-filter/`

---

## Phase 1: Setup

**Purpose**: Add the shared browse-result and test-harness scaffolding needed for column-local search/filter behavior.

- [X] T001 Extend browse interaction payloads for search/filter commands in `samples/EtpExplorer/ExplorerModels.cs`
- [X] T002 [P] Extend fake browse-session snapshots and queued responses for search/filter scenarios in `tests/EtpExplorer.Tests/TestSupport/FakeExplorerUi.cs`

**Checkpoint**: The explorer browse workflow can represent search/filter interactions in production and fake UI flows.

---

## Phase 2: Foundational

**Purpose**: Establish the shared column-state, wildcard matching, and filtered-index handling that all stories depend on.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T003 Implement shared browse-column search/filter state and visible-resource derivation in `samples/EtpExplorer/ExplorerModels.cs`
- [X] T004 [P] Extend the browse UI contract for search/filter-aware workspace turns in `samples/EtpExplorer/IExplorerUi.cs`
- [X] T005 [P] Implement filtered-index reconciliation and status-message updates in `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: The browse workflow can carry column-local filter state, derive visible items, and translate filtered selections back to the underlying resources.

---

## Phase 3: User Story 1 - Find an Item in the Active Column (Priority: P1) 🎯 MVP

**Goal**: Let the user search the focused browse column, including `*` wildcard matching, to quickly find known items.

**Independent Test**: Open a populated browse column, enter plain-text and wildcard search terms, and verify that matching items become easy to locate while the rest of the explorer remains usable.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T006 [P] [US1] Add plain-text and wildcard search workflow tests in `tests/EtpExplorer.Tests/ExplorerColumnSearchWorkflowTests.cs`
- [X] T007 [P] [US1] Add active-search rendering and clear-term tests in `tests/EtpExplorer.Tests/ExplorerColumnSearchRenderingTests.cs`

### Implementation for User Story 1

- [X] T008 [P] [US1] Implement case-insensitive plain-text and `*` wildcard matching helpers for browse columns in `samples/EtpExplorer/ExplorerModels.cs`
- [X] T009 [US1] Implement focused-column search input, active-search indicators, and clear-term behavior in `samples/EtpExplorer/SpectreExplorerUi.cs`
- [X] T010 [US1] Integrate search actions and search-result status updates into the browse loop in `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: The user can search the active column with plain text or `*` wildcards and clear the term without leaving the browse flow.

---

## Phase 4: User Story 2 - Filter the Active Column to a Smaller Working Set (Priority: P2)

**Goal**: Let the user narrow the visible items in the active column to a smaller working set and continue navigating within the filtered results.

**Independent Test**: Apply and refine a filter in a populated active column, confirm that only matching items remain visible, and continue opening or selecting resources from the filtered list.

### Tests for User Story 2 (REQUIRED) ⚠️

- [X] T011 [P] [US2] Add filtered-list navigation and clear/restore workflow tests in `tests/EtpExplorer.Tests/ExplorerColumnFilterWorkflowTests.cs`
- [X] T012 [P] [US2] Add no-match and filtered-result feedback tests in `tests/EtpExplorer.Tests/ExplorerColumnFilterRenderingTests.cs`

### Implementation for User Story 2

- [X] T013 [P] [US2] Implement filtered-state transitions, match counts, and no-result state handling in `samples/EtpExplorer/ExplorerModels.cs`
- [X] T014 [US2] Implement filtered-list rendering and in-filter navigation behavior in `samples/EtpExplorer/SpectreExplorerUi.cs`
- [X] T015 [US2] Apply filtered open/select actions against the underlying browse resources in `samples/EtpExplorer/ExplorerApp.cs`

**Checkpoint**: The user can filter the active column down to matching items, get clear no-match feedback, and continue browsing within the filtered result set.

---

## Phase 5: User Story 3 - Preserve Context While Searching or Filtering (Priority: P3)

**Goal**: Keep search/filter scoped to the focused column while preserving multi-column context and deterministic selection behavior.

**Independent Test**: Navigate across multiple columns, apply search/filter in one column, and verify that other columns remain unchanged while selection is preserved, reassigned, or cleared according to the documented rules.

### Tests for User Story 3 (REQUIRED) ⚠️

- [X] T016 [P] [US3] Add active-column-only scoping and multi-column context tests in `tests/EtpExplorer.Tests/ExplorerColumnFilterContextTests.cs`
- [X] T017 [P] [US3] Add selection-preservation and reassignment tests during filtering in `tests/EtpExplorer.Tests/ExplorerColumnFilterSelectionTests.cs`

### Implementation for User Story 3

- [X] T018 [P] [US3] Implement per-column search-state persistence across focus changes and pane navigation in `samples/EtpExplorer/ExplorerApp.cs`
- [X] T019 [P] [US3] Implement selection preservation, reassignment, and cleared-selection rules for filtered columns in `samples/EtpExplorer/ExplorerModels.cs`
- [X] T020 [US3] Surface cross-column context preservation and selection-change feedback in `samples/EtpExplorer/SpectreExplorerUi.cs`

**Checkpoint**: Search/filter stays local to the active column, other columns keep their context, and selection behavior is deterministic when visibility changes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize documentation, validation, and cross-story cleanup.

- [X] T021 [P] Update search/filter manual validation steps and usage notes in `specs/008-search-column-filter/quickstart.md`
- [X] T022 [P] Reconcile the final UI behavior with the feature contract and data model in `specs/008-search-column-filter/contracts/explorer-column-search.md` and `specs/008-search-column-filter/data-model.md`
- [X] T023 Validate the completed search/filter flow against `specs/008-search-column-filter/quickstart.md` and apply any final wording or help-text fixes in `samples/EtpExplorer/SpectreExplorerUi.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion; delivers the MVP search experience.
- **User Story 2 (Phase 4)**: Depends on Foundational completion and builds on the search mechanics from User Story 1.
- **User Story 3 (Phase 5)**: Depends on Foundational completion and builds on the filter/state behavior established by User Stories 1 and 2.
- **Polish (Phase 6)**: Depends on the desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Independent after Phase 2.
- **User Story 2 (P2)**: Depends on the search/filter model from US1 but remains independently testable within a single active column.
- **User Story 3 (P3)**: Depends on the filter-state behavior from US1 and US2 to verify multi-column scoping and selection consistency.

### Within Each User Story

- Tests must be written and fail before implementation.
- Column-state/model logic should land before UI wiring that consumes it.
- UI behavior should be wired before final browse-workflow integration is considered complete.
- Each story should be validated independently before proceeding.

### Parallel Opportunities

- `T001` and `T002` can run in parallel during Setup.
- `T004` and `T005` can run in parallel after `T003` completes.
- In US1, `T006` and `T007` can run together, then `T008` can run in parallel with those tests before `T009` and `T010` integrate the behavior.
- In US2, `T011` and `T012` can run together, then `T013` can proceed before `T014` and `T015` integrate filtered navigation.
- In US3, `T016` and `T017` can run together, then `T018` and `T019` can run together before `T020` finalizes user-visible feedback.

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests together:
Task: "Add plain-text and wildcard search workflow tests in tests/EtpExplorer.Tests/ExplorerColumnSearchWorkflowTests.cs"
Task: "Add active-search rendering and clear-term tests in tests/EtpExplorer.Tests/ExplorerColumnSearchRenderingTests.cs"

# Launch US1 core logic after tests fail:
Task: "Implement case-insensitive plain-text and * wildcard matching helpers for browse columns in samples/EtpExplorer/ExplorerModels.cs"
```

---

## Parallel Example: User Story 2

```bash
# Launch US2 tests together:
Task: "Add filtered-list navigation and clear/restore workflow tests in tests/EtpExplorer.Tests/ExplorerColumnFilterWorkflowTests.cs"
Task: "Add no-match and filtered-result feedback tests in tests/EtpExplorer.Tests/ExplorerColumnFilterRenderingTests.cs"

# Launch US2 state logic after tests fail:
Task: "Implement filtered-state transitions, match counts, and no-result state handling in samples/EtpExplorer/ExplorerModels.cs"
```

---

## Parallel Example: User Story 3

```bash
# Launch US3 tests together:
Task: "Add active-column-only scoping and multi-column context tests in tests/EtpExplorer.Tests/ExplorerColumnFilterContextTests.cs"
Task: "Add selection-preservation and reassignment tests during filtering in tests/EtpExplorer.Tests/ExplorerColumnFilterSelectionTests.cs"

# Launch US3 core tasks together after tests fail:
Task: "Implement per-column search-state persistence across focus changes and pane navigation in samples/EtpExplorer/ExplorerApp.cs"
Task: "Implement selection preservation, reassignment, and cleared-selection rules for filtered columns in samples/EtpExplorer/ExplorerModels.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate plain-text and wildcard search independently in the focused column.
5. Demo the explorer with active-column search before adding deeper filter/context behavior.

### Incremental Delivery

1. Setup + Foundational add the shared search/filter state and UI contract.
2. User Story 1 delivers focused-column search and wildcard matching.
3. User Story 2 adds filtered working-set behavior and no-result handling.
4. User Story 3 adds multi-column context preservation and deterministic selection behavior.
5. Polish reconciles docs, quickstart validation, and final UI wording.

### Suggested MVP Scope

Deliver Phases 1 through 3 first. That yields a usable explorer enhancement where the user can search the active column, use `*` wildcards, and clear the term without disrupting the browse flow.

---

## Notes

- [P] tasks touch different files and do not depend on incomplete tasks in the same phase.
- Story labels trace each task back to the corresponding user story in `spec.md`.
- The new test files in `tests/EtpExplorer.Tests/` isolate story-specific behavior and avoid unnecessary file contention.
- The feature stays inside `samples/EtpExplorer/` and `tests/EtpExplorer.Tests/`; no `EtpClient` protocol changes are planned.
