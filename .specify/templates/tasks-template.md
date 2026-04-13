---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `specs/[###-feature-name]/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are REQUIRED for protocol-facing work. Include unit tests and contract or integration tests for wire behavior.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact repository-relative file paths in descriptions

## Path Conventions

- **C# client library**: `src/EtpClient/`, `tests/EtpClient.UnitTests/`, `tests/EtpClient.IntegrationTests/`, `tests/EtpClient.ContractTests/`
- Adjust exact project names based on the selected plan structure, but preserve the split between unit, integration, and contract coverage.
- Never use machine-local absolute filesystem paths in generated tasks.

<!-- 
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.
  
  The /speckit.tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/
  
  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment
  
  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create the library and test project structure per implementation plan
- [ ] T002 Initialize the C# solution, library project, and test projects with required dependencies
- [ ] T003 [P] Configure formatting, analyzers, and build validation

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

Examples of foundational tasks (adjust based on your project):

- [ ] T004 Establish protocol models and shared message handling primitives
- [ ] T005 [P] Implement explicit authentication configuration and secret-safe handling
- [ ] T006 [P] Establish connection lifecycle, cancellation, and disposal primitives
- [ ] T007 Create shared subscription abstractions and data models
- [ ] T008 Configure diagnostics, structured logging hooks, and exception mapping
- [ ] T009 Record the relevant ETP clauses and compatibility assumptions in feature docs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T010 [P] [US1] Unit tests for parsing and state transitions in tests/EtpClient.UnitTests/[Feature]Tests.cs
- [ ] T011 [P] [US1] Contract or integration tests for protocol flow in tests/EtpClient.ContractTests/[Feature]ContractTests.cs

### Implementation for User Story 1

- [ ] T012 [P] [US1] Add protocol models or message handlers in src/EtpClient/Protocol/
- [ ] T013 [P] [US1] Add connection or subscription primitives in src/EtpClient/Connection/ or src/EtpClient/Subscriptions/
- [ ] T014 [US1] Implement the feature in the appropriate library surface (depends on T012, T013)
- [ ] T015 [US1] Add validation, failure mapping, and cancellation behavior
- [ ] T016 [US1] Add diagnostics for connection and subscription lifecycle events
- [ ] T017 [US1] Document public API usage and protocol assumptions

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 2 (REQUIRED) ⚠️

- [ ] T018 [P] [US2] Unit tests in tests/EtpClient.UnitTests/[Feature]Tests.cs
- [ ] T019 [P] [US2] Contract or integration tests in tests/EtpClient.ContractTests/[Feature]ContractTests.cs

### Implementation for User Story 2

- [ ] T020 [P] [US2] Add or extend protocol and domain models in src/EtpClient/Protocol/
- [ ] T021 [US2] Implement feature behavior in src/EtpClient/
- [ ] T022 [US2] Add diagnostics and failure handling for this flow
- [ ] T023 [US2] Integrate with User Story 1 components while preserving independent testability

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 3 (REQUIRED) ⚠️

- [ ] T024 [P] [US3] Unit tests in tests/EtpClient.UnitTests/[Feature]Tests.cs
- [ ] T025 [P] [US3] Contract or integration tests in tests/EtpClient.ContractTests/[Feature]ContractTests.cs

### Implementation for User Story 3

- [ ] T026 [P] [US3] Add the required models or handlers in src/EtpClient/
- [ ] T027 [US3] Implement feature behavior in src/EtpClient/
- [ ] T028 [US3] Add diagnostics, compatibility notes, and tests for this flow

**Checkpoint**: All user stories should now be independently functional

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] Documentation updates in docs/
- [ ] TXXX Code cleanup and refactoring
- [ ] TXXX Performance optimization across all stories
- [ ] TXXX [P] Additional unit test coverage in tests/EtpClient.UnitTests/
- [ ] TXXX Security hardening
- [ ] TXXX Run quickstart.md validation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - May integrate with US1/US2 but should be independently testable

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models and protocol handlers before higher-level feature logic
- Core implementation before diagnostics and integration wiring
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Unit tests for parsing and state transitions in tests/EtpClient.UnitTests/[Feature]Tests.cs"
Task: "Contract or integration tests for protocol flow in tests/EtpClient.ContractTests/[Feature]ContractTests.cs"

# Launch all models for User Story 1 together:
Task: "Add protocol models in src/EtpClient/Protocol/"
Task: "Add subscription primitives in src/EtpClient/Subscriptions/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
