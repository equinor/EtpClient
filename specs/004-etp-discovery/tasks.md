---
description: "Task list for ETP Discovery Traversal"
---

# Tasks: ETP Discovery Traversal

**Branch**: `004-add-etp-discovery`  
**Input**: Design documents from `specs/004-etp-discovery/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED for protocol-facing work. Include unit, integration, and sample tests for discovery request/response behavior, traversal aggregation, and secret-safe failures.

**Organization**: Tasks are grouped by user story to preserve independent implementation and verification where practical.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g. `[US1]`, `[US2]`, `[US3]`)
- Include exact file paths in each task description

## Path Conventions

- **Library**: `src/EtpClient/`
- **Unit tests**: `tests/EtpClient.UnitTests/`
- **Integration tests**: `tests/EtpClient.IntegrationTests/`
- **Sample app**: `samples/EtpClient.SampleConsole/`
- **Sample tests**: `tests/EtpClient.SampleConsole.Tests/`
- **Feature docs**: `specs/004-etp-discovery/`

---

## Phase 1: Setup

**Purpose**: Create the discovery-specific file scaffolding needed for implementation and test-first development.

- [x] T001 Create discovery model and protocol source files in `src/EtpClient/Models/DiscoveryModels.cs`, `src/EtpClient/Protocol/GetResourcesMessage.cs`, and `src/EtpClient/Protocol/GetResourcesResponseMessage.cs`
- [x] T002 [P] Create discovery library test files in `tests/EtpClient.UnitTests/Protocol/DiscoverySessionCodecTests.cs`, `tests/EtpClient.UnitTests/Connection/EtpSessionManagerDiscoveryTests.cs`, and `tests/EtpClient.UnitTests/Models/EtpConnectionOptionsDiscoveryTests.cs`
- [x] T003 [P] Create discovery integration and sample test files in `tests/EtpClient.IntegrationTests/Connection/DiscoveryRootTraversalTests.cs`, `tests/EtpClient.IntegrationTests/Connection/DiscoveryTraversalTests.cs`, `tests/EtpClient.IntegrationTests/Connection/DiscoveryFailureTests.cs`, `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerDiscoveryTests.cs`, and `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterDiscoveryTests.cs`

**Checkpoint**: Discovery source and test locations exist and are ready for test-first implementation.

---

## Phase 2: Foundational

**Purpose**: Establish shared discovery primitives that block all user stories.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [x] T004 [P] Add typed discovery request, resource, result, and failure models in `src/EtpClient/Models/DiscoveryModels.cs`
- [x] T005 [P] Extend default requested-protocol negotiation to advertise Discovery in `src/EtpClient/Models/EtpConnectionOptions.cs` and cover it in `tests/EtpClient.UnitTests/Models/EtpConnectionOptionsDiscoveryTests.cs`
- [x] T006 [P] Extend Discovery message constants and codec contract members in `src/EtpClient/Protocol/EtpMessageHeader.cs` and `src/EtpClient/Protocol/IEtpSessionCodec.cs`
- [x] T007 Implement shared discovery diagnostics and response-dispatch plumbing in `src/EtpClient/Diagnostics/EtpClientLog.cs` and `src/EtpClient/Connection/EtpSessionManager.cs`
- [x] T008 Record negotiated-protocol assumptions and governing Discovery clauses in `specs/004-etp-discovery/research.md` and `specs/004-etp-discovery/contracts/etp-client-discovery-api.md`

**Checkpoint**: Shared discovery models, negotiation, logging, and codec seams are ready for story work.

---

## Phase 3: User Story 1 - Enumerate Discovery Roots (Priority: P1) 🎯 MVP

**Goal**: Let callers request `eml://` and receive the server's top-level traversal roots through the public client API and sample app.

**Independent Test**: Connect to a Discovery-capable test server, call discovery for `eml://`, and verify the library returns the expected top-level resources while the sample app prints those roots clearly.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T009 [P] [US1] Add binary and JSON codec tests for root `GetResources` request and response frames in `tests/EtpClient.UnitTests/Protocol/DiscoverySessionCodecTests.cs`
- [x] T010 [P] [US1] Add integration tests for successful root traversal from `eml://` in `tests/EtpClient.IntegrationTests/Connection/DiscoveryRootTraversalTests.cs`
- [x] T011 [P] [US1] Add sample runner and output tests for top-level URI discovery rendering in `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerDiscoveryTests.cs` and `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterDiscoveryTests.cs`

### Implementation for User Story 1

- [x] T012 [P] [US1] Implement Protocol 3 `GetResources` request encoding in `src/EtpClient/Protocol/GetResourcesMessage.cs`, `src/EtpClient/Protocol/BinaryEtpSessionCodec.cs`, and `src/EtpClient/Protocol/JsonEtpSessionCodec.cs`
- [x] T013 [P] [US1] Implement root `GetResourcesResponse` decoding and typed resource mapping in `src/EtpClient/Protocol/GetResourcesResponseMessage.cs` and `src/EtpClient/Models/DiscoveryModels.cs`
- [x] T014 [US1] Add a public `DiscoverResourcesAsync` workflow in `src/EtpClient/EtpClient.cs` and `src/EtpClient/Connection/EtpSessionManager.cs`
- [x] T015 [US1] Extend the sample connector discovery flow in `samples/EtpClient.SampleConsole/IEtpConnector.cs`, `samples/EtpClient.SampleConsole/EtpConnector.cs`, and `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`
- [x] T016 [US1] Print discovered top-level URIs and streamability hints in `samples/EtpClient.SampleConsole/SampleRunOutcome.cs` and `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`

**Checkpoint**: User Story 1 delivers root discovery through the library and sample app.

---

## Phase 4: User Story 2 - Traverse Child Resources (Priority: P2)

**Goal**: Allow callers to request children for any discovered URI and receive one logical result even when the server replies with acknowledgements or multiple response parts.

**Independent Test**: Starting from a discovered folder URI, request its children and verify that the library returns ordered child resources, empty acknowledgements become empty results, and multipart responses are aggregated.

### Tests for User Story 2 (REQUIRED) ⚠️

- [x] T017 [P] [US2] Add unit tests for multipart aggregation and `Acknowledge`-as-empty handling in `tests/EtpClient.UnitTests/Connection/EtpSessionManagerDiscoveryTests.cs`
- [x] T018 [P] [US2] Add integration tests for child traversal and empty-child acknowledgements in `tests/EtpClient.IntegrationTests/Connection/DiscoveryTraversalTests.cs`

### Implementation for User Story 2

- [x] T019 [P] [US2] Extend discovery result models with requested-URI, ordered-resource, and empty-acknowledgement state in `src/EtpClient/Models/DiscoveryModels.cs`
- [x] T020 [P] [US2] Implement multipart response aggregation and `Acknowledge` handling in `src/EtpClient/Protocol/BinaryEtpSessionCodec.cs`, `src/EtpClient/Protocol/JsonEtpSessionCodec.cs`, and `src/EtpClient/Connection/EtpSessionManager.cs`
- [x] T021 [US2] Support traversal for arbitrary discovered URIs through `src/EtpClient/EtpClient.cs` and `src/EtpClient/Connection/EtpSessionManager.cs`
- [x] T022 [US2] Update traversal usage guidance for child discovery and empty results in `specs/004-etp-discovery/quickstart.md`

**Checkpoint**: User Story 2 supports deeper traversal and correct empty/multipart semantics.

---

## Phase 5: User Story 3 - Identify Streaming Candidates (Priority: P3)

**Goal**: Expose enough typed metadata and failure detail for callers to distinguish traversable containers from stream-relevant resources without raw protocol parsing.

**Independent Test**: Discover resources from a fixture that mixes folders and leaf nodes, then verify the client exposes `hasChildren`, `channelSubscribable`, `resourceType`, and secret-safe failure information for invalid or denied requests.

### Tests for User Story 3 (REQUIRED) ⚠️

- [x] T023 [P] [US3] Add unit tests for stream-relevant metadata mapping and discovery failure translation in `tests/EtpClient.UnitTests/Protocol/DiscoverySessionCodecTests.cs` and `tests/EtpClient.UnitTests/Connection/EtpSessionManagerDiscoveryTests.cs`
- [x] T024 [P] [US3] Add integration tests for invalid URI, unsupported Discovery, and protocol-limit failures in `tests/EtpClient.IntegrationTests/Connection/DiscoveryFailureTests.cs`
- [x] T025 [P] [US3] Add sample tests for stream-candidate output and secret-safe discovery failures in `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerDiscoveryTests.cs` and `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterDiscoveryTests.cs`

### Implementation for User Story 3

- [x] T026 [P] [US3] Preserve `channelSubscribable`, `hasChildren`, `resourceType`, `uuid`, `objectNotifiable`, and `customData` in `src/EtpClient/Models/DiscoveryModels.cs` and `src/EtpClient/Protocol/GetResourcesResponseMessage.cs`
- [x] T027 [US3] Implement discovery-specific protocol failure mapping and diagnostics in `src/EtpClient/Connection/EtpSessionManager.cs` and `src/EtpClient/Diagnostics/EtpClientLog.cs`
- [x] T028 [US3] Surface stream-candidate metadata and secret-safe discovery failures in `samples/EtpClient.SampleConsole/SampleRunOutcome.cs` and `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`
- [x] T029 [US3] Update typed metadata and failure semantics in `specs/004-etp-discovery/data-model.md` and `specs/004-etp-discovery/contracts/etp-client-discovery-api.md`

**Checkpoint**: User Story 3 makes traversal results actionable for downstream streaming workflows.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Align documentation, repository guidance, and full validation across all discovery stories.

- [x] T030 [P] Update end-to-end verification notes and clause traceability in `specs/004-etp-discovery/research.md` and `specs/004-etp-discovery/quickstart.md`
- [x] T031 [P] Update repository guidance for the Discovery API and sample usage in `.github/copilot-instructions.md`
- [x] T032 Run discovery validation via `tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj`, `tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj`, and `tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup; blocks all story work.
- **User Story 1 (Phase 3)**: Depends on Foundational; defines the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational and builds on the shared discovery request path established in User Story 1.
- **User Story 3 (Phase 5)**: Depends on Foundational and builds on the discovery result/failure plumbing established in User Stories 1 and 2.
- **Polish (Phase 6)**: Depends on all implemented stories.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on other user stories; start here for the MVP.
- **User Story 2 (P2)**: Reuses the root-discovery transport and public API from User Story 1, but remains independently testable through child traversal scenarios.
- **User Story 3 (P3)**: Reuses discovery result handling from User Stories 1 and 2, but remains independently testable through metadata and failure scenarios.

### Within Each User Story

- Tests MUST be written and shown failing before implementation.
- Protocol models and codec changes come before session orchestration.
- Session orchestration comes before sample wiring.
- Documentation and quickstart updates follow completed behavior.

### Parallel Opportunities

- `T002` and `T003` can run in parallel after `T001` establishes the discovery file set.
- `T004`, `T005`, and `T006` can run in parallel in the foundational phase.
- `T009`, `T010`, and `T011` can run in parallel for User Story 1.
- `T012` and `T013` can run in parallel once User Story 1 tests exist.
- `T017` and `T018` can run in parallel for User Story 2.
- `T019` and `T020` can run in parallel once User Story 2 tests exist.
- `T023`, `T024`, and `T025` can run in parallel for User Story 3.
- `T030` and `T031` can run in parallel during polish.

---

## Parallel Example: User Story 1

```bash
# Launch User Story 1 tests together:
Task: "Add binary and JSON codec tests for root GetResources request and response frames in tests/EtpClient.UnitTests/Protocol/DiscoverySessionCodecTests.cs"
Task: "Add integration tests for successful root traversal from eml:// in tests/EtpClient.IntegrationTests/Connection/DiscoveryRootTraversalTests.cs"
Task: "Add sample runner and output tests for top-level URI discovery rendering in tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerDiscoveryTests.cs and tests/EtpClient.SampleConsole.Tests/SampleOutputWriterDiscoveryTests.cs"

# Launch User Story 1 implementation tasks together after tests fail:
Task: "Implement Protocol 3 GetResources request encoding in src/EtpClient/Protocol/GetResourcesMessage.cs, src/EtpClient/Protocol/BinaryEtpSessionCodec.cs, and src/EtpClient/Protocol/JsonEtpSessionCodec.cs"
Task: "Implement root GetResourcesResponse decoding and typed resource mapping in src/EtpClient/Protocol/GetResourcesResponseMessage.cs and src/EtpClient/Models/DiscoveryModels.cs"
```

## Parallel Example: User Story 2

```bash
# Launch User Story 2 tests together:
Task: "Add unit tests for multipart aggregation and Acknowledge-as-empty handling in tests/EtpClient.UnitTests/Connection/EtpSessionManagerDiscoveryTests.cs"
Task: "Add integration tests for child traversal and empty-child acknowledgements in tests/EtpClient.IntegrationTests/Connection/DiscoveryTraversalTests.cs"

# Launch User Story 2 implementation tasks together after tests fail:
Task: "Extend discovery result models with requested-URI, ordered-resource, and empty-acknowledgement state in src/EtpClient/Models/DiscoveryModels.cs"
Task: "Implement multipart response aggregation and Acknowledge handling in src/EtpClient/Protocol/BinaryEtpSessionCodec.cs, src/EtpClient/Protocol/JsonEtpSessionCodec.cs, and src/EtpClient/Connection/EtpSessionManager.cs"
```

## Parallel Example: User Story 3

```bash
# Launch User Story 3 tests together:
Task: "Add unit tests for stream-relevant metadata mapping and discovery failure translation in tests/EtpClient.UnitTests/Protocol/DiscoverySessionCodecTests.cs and tests/EtpClient.UnitTests/Connection/EtpSessionManagerDiscoveryTests.cs"
Task: "Add integration tests for invalid URI, unsupported Discovery, and protocol-limit failures in tests/EtpClient.IntegrationTests/Connection/DiscoveryFailureTests.cs"
Task: "Add sample tests for stream-candidate output and secret-safe discovery failures in tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerDiscoveryTests.cs and tests/EtpClient.SampleConsole.Tests/SampleOutputWriterDiscoveryTests.cs"

# Launch User Story 3 implementation tasks together after tests fail:
Task: "Preserve channelSubscribable, hasChildren, resourceType, uuid, objectNotifiable, and customData in src/EtpClient/Models/DiscoveryModels.cs and src/EtpClient/Protocol/GetResourcesResponseMessage.cs"
Task: "Surface stream-candidate metadata and secret-safe discovery failures in samples/EtpClient.SampleConsole/SampleRunOutcome.cs and samples/EtpClient.SampleConsole/SampleOutputWriter.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate root discovery through the library and sample app.
5. Stop and review before expanding to deeper traversal.

### Incremental Delivery

1. Deliver root discovery from `eml://` through the public API and sample app.
2. Add child traversal, multipart aggregation, and empty acknowledgements.
3. Add stream-candidate metadata and discovery-specific failure behavior.
4. Finish with documentation alignment and full validation.

### Parallel Team Strategy

1. One developer handles foundational protocol/message work.
2. One developer prepares unit and integration coverage in parallel.
3. After User Story 1 protocol work lands, another developer can wire the sample app while deeper traversal and failure coverage proceed.

---

## Notes

- [P] tasks operate on different files and avoid incomplete-task dependencies.
- User Story 1 is the recommended MVP scope.
- Discovery work must remain secret-safe and preserve current session-establishment behavior.
- The sample app should demonstrate root discovery without re-implementing protocol logic outside the library.
