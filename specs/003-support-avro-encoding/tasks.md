---
description: "Task list for Support Avro Encoding"
---

# Tasks: Support Avro Encoding

**Input**: Design documents from `/specs/003-support-avro-encoding/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED for this protocol-facing change. Include unit tests plus integration coverage for binary and JSON wire behavior.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., `[US1]`, `[US2]`, `[US3]`)
- Include exact file paths in descriptions

## Path Conventions

- **Library**: `src/EtpClient/`
- **Unit tests**: `tests/EtpClient.UnitTests/`
- **Integration tests**: `tests/EtpClient.IntegrationTests/`
- **Sample app**: `samples/EtpClient.SampleConsole/`
- **Feature docs**: `specs/003-support-avro-encoding/`

---

## Phase 1: Setup

**Purpose**: Prepare the repository for encoding-related implementation and test coverage.

- [x] T001 Create encoding-focused unit and integration test files under `tests/EtpClient.UnitTests/Connection/`, `tests/EtpClient.UnitTests/Protocol/`, and `tests/EtpClient.IntegrationTests/Connection/`
- [x] T002 Create or update feature-facing documentation placeholders in `specs/003-support-avro-encoding/contracts/etp-client-encoding-api.md` and `specs/003-support-avro-encoding/quickstart.md` to align with implementation checkpoints
- [x] T003 [P] Review and record the current binary-only handshake behavior against `docs/ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md` in `specs/003-support-avro-encoding/research.md`

**Checkpoint**: The repo has a clear implementation target, current behavior is documented, and test file locations are established.

---

## Phase 2: Foundational

**Purpose**: Establish the shared models, transport abstractions, and codec seams required by all user stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T004 Create the public encoding selection model in `src/EtpClient/Models/EtpMessageEncoding.cs`
- [x] T005 Update `src/EtpClient/Models/EtpConnectionOptions.cs` to include validated encoding selection with documented default behavior
- [x] T006 [P] Extend transport frame abstractions in `src/EtpClient/Connection/IWebSocketTransport.cs` and `src/EtpClient/Connection/ClientWebSocketTransport.cs` to support the selected binary or text frame mode
- [x] T007 [P] Create shared codec abstractions in `src/EtpClient/Protocol/IEtpMessageEncoder.cs`, `src/EtpClient/Protocol/IEtpMessageDecoder.cs`, or equivalent shared protocol files for session message serialization and parsing
- [x] T008 Create codec-selection and session-plumbing seams in `src/EtpClient/Connection/EtpSessionManager.cs` and `src/EtpClient/EtpClient.cs`
- [x] T009 [P] Extend diagnostics and failure context in `src/EtpClient/Diagnostics/EtpClientLog.cs`, `src/EtpClient/Models/EtpConnectionFailureCategory.cs`, and `src/EtpClient/Models/EtpConnectionResult.cs` for encoding-aware behavior
- [x] T010 Record the governing ETP encoding clauses, default behavior, and compatibility assumptions in `specs/003-support-avro-encoding/plan.md` and `specs/003-support-avro-encoding/contracts/etp-client-encoding-api.md`

**Checkpoint**: Encoding can be selected publicly, transport can carry the required frame mode, and the session flow has a shared abstraction boundary for binary and JSON codecs.

---

## Phase 3: User Story 1 - Choose Message Encoding (Priority: P1) 🎯 MVP

**Goal**: Let callers explicitly choose binary or JSON encoding and successfully establish the supported Protocol 0 session flow in either mode.

**Independent Test**: Configure the client once for binary and once for JSON, run the session-establishment flow in isolation, and verify each run uses the selected encoding and succeeds when the endpoint/fixture supports that mode.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T011 [P] [US1] Add unit tests for encoding option defaults and validation in `tests/EtpClient.UnitTests/Models/EtpConnectionOptionsEncodingTests.cs`
- [x] T012 [P] [US1] Add unit tests for binary-session codec behavior in `tests/EtpClient.UnitTests/Protocol/BinarySessionCodecTests.cs`
- [x] T013 [P] [US1] Add unit tests for JSON-session codec behavior in `tests/EtpClient.UnitTests/Protocol/JsonSessionCodecTests.cs`
- [x] T014 [P] [US1] Add integration tests for successful binary and JSON session establishment in `tests/EtpClient.IntegrationTests/Connection/ConnectAsyncEncodingTests.cs`

### Implementation for User Story 1

- [x] T015 [P] [US1] Implement binary session codec wiring using the new abstractions in `src/EtpClient/Protocol/RequestSessionMessage.cs`, `src/EtpClient/Protocol/OpenSessionMessage.cs`, and related protocol files
- [x] T016 [P] [US1] Implement JSON session codec support for `RequestSession`, `OpenSession`, and `ProtocolException` in new or updated files under `src/EtpClient/Protocol/`
- [x] T017 [US1] Update `src/EtpClient/Connection/EtpSessionManager.cs` to select the configured codec and frame mode for outgoing and incoming session messages
- [x] T018 [US1] Update `src/EtpClient/EtpClient.cs` and `src/EtpClient/Models/EtpConnectionOptions.cs` so callers can use one documented option to choose encoding without changing the connect flow
- [x] T019 [US1] Document the binary/JSON selection flow and default in `specs/003-support-avro-encoding/quickstart.md` and `specs/003-support-avro-encoding/contracts/etp-client-encoding-api.md`

**Checkpoint**: Callers can choose binary or JSON and establish the currently supported session flow in either mode.

---

## Phase 4: User Story 2 - Get Clear Failure Behavior (Priority: P2)

**Goal**: Make encoding-related failures observable and distinguishable from authentication, transport, and general protocol failures.

**Independent Test**: Run the client with an unsupported or mismatched encoding in isolation and verify that the resulting failure is secret-safe, actionable, and distinct from other connection-failure categories.

### Tests for User Story 2 (REQUIRED) ⚠️

- [x] T020 [P] [US2] Add unit tests for encoding-aware failure mapping in `tests/EtpClient.UnitTests/Connection/EtpSessionManagerEncodingFailureTests.cs`
- [x] T021 [P] [US2] Add unit tests for secret-safe encoding diagnostics in `tests/EtpClient.UnitTests/Diagnostics/EtpClientLogEncodingTests.cs`
- [x] T022 [P] [US2] Add integration tests for rejected or mismatched encoding behavior in `tests/EtpClient.IntegrationTests/Connection/ConnectAsyncEncodingFailureTests.cs`

### Implementation for User Story 2

- [x] T023 [P] [US2] Implement encoding-mismatch detection and failure classification in `src/EtpClient/Connection/EtpSessionManager.cs` and `src/EtpClient/Models/EtpConnectionFailureCategory.cs`
- [x] T024 [P] [US2] Extend exception/result context for selected encoding and failure reporting in `src/EtpClient/Models/EtpConnectionResult.cs` and `src/EtpClient/Models/EtpConnectionOptions.cs`
- [x] T025 [US2] Add encoding-aware structured diagnostics in `src/EtpClient/Diagnostics/EtpClientLog.cs`
- [x] T026 [US2] Update feature docs for unsupported or rejected encoding outcomes in `specs/003-support-avro-encoding/quickstart.md` and `specs/003-support-avro-encoding/contracts/etp-client-encoding-api.md`

**Checkpoint**: Encoding-related failures are distinguishable, secret-safe, and consistently surfaced to callers.

---

## Phase 5: User Story 3 - Use Encoding Choice Consistently (Priority: P3)

**Goal**: Make the encoding option consistent across the client API, sample usage, and ongoing session behavior.

**Independent Test**: Review and execute the sample and automated tests to confirm the same selected encoding governs the entire session flow and is exposed clearly through the public API and sample usage.

### Tests for User Story 3 (REQUIRED) ⚠️

- [x] T027 [P] [US3] Add unit tests for session-wide encoding consistency in `tests/EtpClient.UnitTests/Connection/EtpSessionManagerEncodingConsistencyTests.cs`
- [x] T028 [P] [US3] Add sample-app tests for binding and using the encoding option in `tests/EtpClient.SampleConsole.Tests/SampleConsoleEncodingOptionTests.cs`
- [x] T029 [P] [US3] Add live or opt-in integration tests for encoding selection against configured endpoints in `tests/EtpClient.IntegrationTests/Connection/LiveConnectAsyncEncodingTests.cs`

### Implementation for User Story 3

- [x] T030 [P] [US3] Update session message handling to enforce one selected encoding for the full session in `src/EtpClient/Connection/EtpSessionManager.cs`
- [x] T031 [P] [US3] Update sample configuration and runtime flow for encoding selection in `samples/EtpClient.SampleConsole/SampleConsoleOptions.cs`, `samples/EtpClient.SampleConsole/Program.cs`, and `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`
- [x] T032 [US3] Update sample output and quickstart usage to show the encoding choice clearly in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs` and `specs/003-support-avro-encoding/quickstart.md`
- [x] T033 [US3] Align the public contract and feature docs with the implemented consistent-encoding behavior in `specs/003-support-avro-encoding/contracts/etp-client-encoding-api.md` and `specs/003-support-avro-encoding/data-model.md`

**Checkpoint**: The encoding option behaves consistently across the client API, sample app, and session flow.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize cross-story cleanup, compatibility notes, and end-to-end validation.

- [x] T034 [P] Update inline API documentation and examples in `src/EtpClient/EtpClient.cs`, `src/EtpClient/Models/EtpConnectionOptions.cs`, and related public model files
- [x] T035 [P] Update repository guidance for the new encoding feature in `.github/copilot-instructions.md`
- [x] T036 [P] Reconcile launch/test helpers for encoding-focused debugging in `.vscode/launch.json` and `.vscode/tasks.json`
- [x] T037 Run targeted validation with `dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj`, `dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj`, and `dotnet test tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj`
- [x] T038 Run the quickstart validation flow for both binary and JSON selections in `specs/003-support-avro-encoding/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories.
- **User Stories (Phases 3-5)**: Depend on Foundational completion.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational; defines the MVP with caller-selectable binary and JSON session establishment.
- **User Story 2 (P2)**: Starts after Foundational and builds on the encoding-aware session path from US1, but remains independently testable through rejection and mismatch scenarios.
- **User Story 3 (P3)**: Starts after Foundational and builds on the selected-encoding session path from US1, but remains independently testable through sample usage and session-consistency checks.

### Within Each User Story

- Tests MUST be written and fail before implementation.
- Public option/model updates precede session manager and sample wiring.
- Codec work precedes higher-level session orchestration.
- Diagnostics and documentation follow the core implemented behavior.

### Parallel Opportunities

- `T003` can run in parallel with `T001` and `T002` during Setup.
- `T006`, `T007`, and `T009` can run in parallel during Foundational once `T004` and `T005` define the public model surface.
- Within US1, `T011` through `T014` can run in parallel before implementation.
- Within US1, `T015` and `T016` can run in parallel before `T017` integrates codec selection into the session flow.
- Within US2, `T020` through `T022` can run in parallel.
- Within US3, `T027` through `T029` can run in parallel.
- In Polish, `T034`, `T035`, and `T036` can run in parallel before final validation.

---

## Parallel Example: User Story 1

```bash
# Launch User Story 1 tests together:
Task: "Add unit tests for encoding option defaults and validation in tests/EtpClient.UnitTests/Models/EtpConnectionOptionsEncodingTests.cs"
Task: "Add unit tests for binary-session codec behavior in tests/EtpClient.UnitTests/Protocol/BinarySessionCodecTests.cs"
Task: "Add unit tests for JSON-session codec behavior in tests/EtpClient.UnitTests/Protocol/JsonSessionCodecTests.cs"
Task: "Add integration tests for successful binary and JSON session establishment in tests/EtpClient.IntegrationTests/Connection/ConnectAsyncEncodingTests.cs"

# Launch codec implementation tasks together after tests exist:
Task: "Implement binary session codec wiring using the new abstractions in src/EtpClient/Protocol/RequestSessionMessage.cs, src/EtpClient/Protocol/OpenSessionMessage.cs, and related protocol files"
Task: "Implement JSON session codec support for RequestSession, OpenSession, and ProtocolException in new or updated files under src/EtpClient/Protocol/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate binary and JSON session establishment independently.
5. Stop and review before adding richer failure behavior and sample-wide consistency updates.

### Incremental Delivery

1. Deliver the public encoding-selection feature and dual-mode session establishment (US1).
2. Add clear encoding-related failure behavior (US2).
3. Add sample and session-wide consistency behavior (US3).
4. Finish with documentation alignment and full validation.

### Parallel Team Strategy

1. One developer can own public models and connection abstractions in Phase 2 while another prepares unit/integration test scaffolding.
2. Once Foundational work is complete, different developers can take US1 codec implementation, US2 failure behavior, and US3 sample/consistency work in parallel.
3. Final polish focuses on shared docs, launch helpers, and validation commands.

---

## Notes

- [P] tasks operate on separate files and avoid blocking dependencies.
- Each user story is independently testable once Foundational work is complete.
- The selected encoding must remain fixed for the duration of a session attempt.
- Backward-compatible default behavior is part of the acceptance surface and must be tested explicitly.
