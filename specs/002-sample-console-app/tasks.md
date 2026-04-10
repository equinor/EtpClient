---
description: "Task list for Sample Console Application"
---

# Tasks: Sample Console Application

**Input**: Design documents from `/specs/002-sample-console-app/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Sample-specific automated tests are required for configuration validation, success/failure presentation, and clean shutdown. Existing library protocol tests remain the source of wire-level coverage.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., [US1], [US2], [US3])
- Include exact file paths in descriptions

## Path Conventions

- **Library**: `src/EtpClient/`
- **Sample app**: `samples/EtpClient.SampleConsole/`
- **Sample tests**: `tests/EtpClient.SampleConsole.Tests/`
- **Feature docs**: `specs/002-sample-console-app/`

---

## Phase 1: Setup

**Purpose**: Create the sample project structure and wire it into the existing solution and central package management.

- [x] T001 Create `samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj` and add it to `EtpClient.slnx`
- [x] T002 Create `tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj` and add it to `EtpClient.slnx`
- [x] T003 [P] Add sample-project package versions to `Directory.Packages.props`
- [x] T004 [P] Add project references and initial package references in `samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj` and `tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj`
- [x] T005 [P] Create initial sample configuration files in `samples/EtpClient.SampleConsole/appsettings.json` and `samples/EtpClient.SampleConsole/appsettings.Development.json`

**Checkpoint**: Solution contains the sample app and sample test project with resolvable dependencies.

---

## Phase 2: Foundational

**Purpose**: Establish the shared sample infrastructure that every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T006 Create `samples/EtpClient.SampleConsole/SampleConsoleOptions.cs` for secret-backed configuration binding and validation input
- [x] T007 [P] Create `samples/EtpClient.SampleConsole/SampleRunOutcome.cs` and `samples/EtpClient.SampleConsole/SampleExitCode.cs` for app-level result modeling
- [x] T008 [P] Create `samples/EtpClient.SampleConsole/SampleOutputWriter.cs` for secret-safe success/failure console rendering
- [x] T009 Create `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs` to orchestrate configuration validation, `EtpClient` usage, and clean shutdown
- [x] T010 [P] Create test support fakes/helpers in `tests/EtpClient.SampleConsole.Tests/Fakes/` and `tests/EtpClient.SampleConsole.Tests/TestSupport/`
- [x] T011 Record the stable secret key contract and sample project paths in `specs/002-sample-console-app/quickstart.md` and `specs/002-sample-console-app/contracts/sample-console-cli.md`

**Checkpoint**: Sample-specific configuration, result modeling, output rendering, and test helpers are in place.

---

## Phase 3: User Story 1 - Run a Working Example (Priority: P1) 🎯 MVP

**Goal**: Deliver a runnable console app that loads configuration, connects with the public `EtpClient` API, and prints a success summary.

**Independent Test**: With valid configuration values present, `dotnet run --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj` completes a session and reports the negotiated session details.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T012 [P] [US1] Add sample runner success-path unit tests in `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerSuccessTests.cs`
- [x] T013 [P] [US1] Add host/application integration tests for a valid configured run in `tests/EtpClient.SampleConsole.Tests/SampleConsoleProgramSuccessTests.cs`

### Implementation for User Story 1

- [x] T014 [P] [US1] Implement host/bootstrap and explicit user-secrets loading in `samples/EtpClient.SampleConsole/Program.cs`
- [x] T015 [P] [US1] Implement successful connection execution using the public library surface in `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`
- [x] T016 [US1] Wire success-output formatting and session-detail presentation in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs` and `samples/EtpClient.SampleConsole/Program.cs`
- [x] T017 [US1] Ensure the sample project exposes the documented user-secrets contract through `samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj` and `samples/EtpClient.SampleConsole/appsettings.json`

**Checkpoint**: User Story 1 is runnable and demonstrates the happy-path library usage flow.

---

## Phase 4: User Story 2 - Understand Required Inputs and Failures (Priority: P2)

**Goal**: Make missing input and failure categories obvious without exposing secrets.

**Independent Test**: Missing or malformed inputs fail before a connection attempt, and library failures are rendered as distinct validation, authentication, transport, protocol, or cancellation outcomes.

### Tests for User Story 2 (REQUIRED) ⚠️

- [x] T018 [P] [US2] Add configuration validation tests in `tests/EtpClient.SampleConsole.Tests/SampleConsoleOptionsValidationTests.cs`
- [x] T019 [P] [US2] Add failure rendering and secret-safety tests in `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterFailureTests.cs`
- [x] T020 [P] [US2] Add application-flow tests for invalid configuration and connection failure handling in `tests/EtpClient.SampleConsole.Tests/SampleConsoleProgramFailureTests.cs`

### Implementation for User Story 2

- [x] T021 [P] [US2] Implement options validation and user-facing validation messages in `samples/EtpClient.SampleConsole/SampleConsoleOptions.cs` and `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`
- [x] T022 [P] [US2] Implement failure-category mapping and non-zero exit-code behavior in `samples/EtpClient.SampleConsole/SampleRunOutcome.cs`, `samples/EtpClient.SampleConsole/SampleExitCode.cs`, and `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`
- [x] T023 [US2] Implement secret-safe failure presentation in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`
- [x] T024 [US2] Integrate validation-first startup and failure handling into `samples/EtpClient.SampleConsole/Program.cs`

**Checkpoint**: User Story 2 provides clear, secret-safe guidance for incorrect configuration and connection failures.

---

## Phase 5: User Story 3 - Use the Sample as an Integration Starting Point (Priority: P3)

**Goal**: Make the sample easy to reuse as reference code and ensure it closes cleanly on completion or cancellation.

**Independent Test**: Reviewing the sample source shows a clear configuration→connect→report→shutdown flow, and automated tests confirm cancellation and shutdown leave the sample in a non-connected final state.

### Tests for User Story 3 (REQUIRED) ⚠️

- [x] T025 [P] [US3] Add cancellation and clean-disposal tests in `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerShutdownTests.cs`
- [x] T026 [P] [US3] Add end-to-end tests for graceful shutdown and final-state reporting in `tests/EtpClient.SampleConsole.Tests/SampleConsoleProgramShutdownTests.cs`

### Implementation for User Story 3

- [x] T027 [P] [US3] Implement cancellation-aware shutdown and `CloseAsync`/`DisposeAsync` handling in `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`
- [x] T028 [P] [US3] Keep the top-level sample flow readable and reference-oriented in `samples/EtpClient.SampleConsole/Program.cs`
- [x] T029 [US3] Implement optional session-detail toggling and final-state reporting in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs` and `samples/EtpClient.SampleConsole/appsettings.json`

**Checkpoint**: User Story 3 leaves the sample cleanly shut down and readable as reference code.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, documentation alignment, and validation across all stories.

- [x] T030 [P] Update sample run instructions and secret setup examples in `specs/002-sample-console-app/quickstart.md`
- [x] T031 [P] Update the CLI/output contract in `specs/002-sample-console-app/contracts/sample-console-cli.md` to match the implemented app behavior
- [x] T032 [P] Add a repository-facing sample entry reference in `.github/copilot-instructions.md` if the implemented structure or commands differ from the plan
- [x] T033 Run full validation with `dotnet build`, `dotnet test`, and the sample quickstart flow

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup; blocks all user stories.
- **User Stories (Phases 3-5)**: Depend on Foundational completion.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational; defines the MVP.
- **User Story 2 (P2)**: Starts after Foundational and integrates with the runnable sample from US1, but remains independently testable through invalid-input and failure scenarios.
- **User Story 3 (P3)**: Starts after Foundational and builds on the sample execution path from US1, but remains independently testable through cancellation and shutdown behavior.

### Within Each User Story

- Tests MUST be written and fail before implementation.
- Options/result models and runner logic precede program wiring.
- Output shaping follows core behavior.
- Story-specific documentation alignment follows completed behavior.

### Parallel Opportunities

- `T003`, `T004`, and `T005` can run in parallel once the two new projects are created.
- `T007`, `T008`, and `T010` can run in parallel after `T006` starts the sample app foundation.
- Within each user story, the `[P]` test tasks can run together before implementation.
- `T014` and `T015` can proceed in parallel once the foundational runner and options abstractions exist.
- `T021` and `T022` can proceed in parallel after failure-path tests are in place.
- `T030`, `T031`, and `T032` can run in parallel during polish.

---

## Parallel Example: User Story 1

```bash
# Launch User Story 1 tests together:
Task: "Add sample runner success-path unit tests in tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerSuccessTests.cs"
Task: "Add host/application integration tests for a valid configured run in tests/EtpClient.SampleConsole.Tests/SampleConsoleProgramSuccessTests.cs"

# Launch User Story 1 implementation tasks together after tests exist:
Task: "Implement host/bootstrap and explicit user-secrets loading in samples/EtpClient.SampleConsole/Program.cs"
Task: "Implement successful connection execution using the public library surface in samples/EtpClient.SampleConsole/SampleConsoleRunner.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Validate the sample with the quickstart flow
5. Stop and review before adding broader failure and shutdown polish

### Incremental Delivery

1. Deliver the runnable happy-path sample (US1)
2. Add validation and failure clarity (US2)
3. Add shutdown/reference-code polish (US3)
4. Finish with documentation and full validation

### Parallel Team Strategy

1. One developer establishes project/setup and solution wiring
2. One developer prepares sample test infrastructure while another builds sample models/output helpers
3. After foundation, different developers can take US1 happy path, US2 failure path, and US3 shutdown path in parallel

---

## Notes

- [P] tasks operate on separate files and avoid blocking dependencies.
- Each story remains demonstrable on its own once foundational work is complete.
- The sample app must use the public `EtpClient` surface and must not introduce duplicate protocol logic.
- User secrets remain the documented local-development input source.
