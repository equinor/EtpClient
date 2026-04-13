---
description: "Task list for Channel Streaming Support"
---

# Tasks: Channel Streaming Support

**Branch**: `005-add-channel-streaming`  
**Input**: Design documents from `specs/005-channel-streaming/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED for protocol-facing work. Include unit, integration, and sample tests for Protocol 1 describe, live streaming, range retrieval, multipart handling, and secret-safe failures.

**Organization**: Tasks are grouped by user story to preserve independently verifiable increments where practical.

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
- **Feature docs**: `specs/005-channel-streaming/`

---

## Phase 1: Setup

**Purpose**: Create the Protocol 1 source and test scaffolding needed for test-first implementation.

- [X] T001 Create ChannelStreaming source file scaffolding in `src/EtpClient/Models/ChannelStreamingModels.cs`, `src/EtpClient/Protocol/ChannelDescribeMessage.cs`, `src/EtpClient/Protocol/ChannelMetadataMessage.cs`, `src/EtpClient/Protocol/ChannelDataMessage.cs`, `src/EtpClient/Protocol/ChannelStreamingStartMessage.cs`, `src/EtpClient/Protocol/ChannelStreamingStopMessage.cs`, and `src/EtpClient/Protocol/ChannelRangeRequestMessage.cs`
- [X] T002 [P] Create ChannelStreaming unit test scaffolding in `tests/EtpClient.UnitTests/Protocol/ChannelStreamingSessionCodecTests.cs`, `tests/EtpClient.UnitTests/Connection/EtpSessionManagerChannelStreamingTests.cs`, and `tests/EtpClient.UnitTests/Models/ChannelStreamingModelsTests.cs`
- [X] T003 [P] Create ChannelStreaming integration and sample test scaffolding in `tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncTests.cs`, `tests/EtpClient.IntegrationTests/Connection/StartChannelStreamingAsyncTests.cs`, `tests/EtpClient.IntegrationTests/Connection/RequestChannelRangeAsyncTests.cs`, `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs`, and `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelStreamingTests.cs`

**Checkpoint**: Protocol 1 source and test locations exist and are ready for test-first work.

---

## Phase 2: Foundational

**Purpose**: Establish shared Protocol 1 primitives that block all user stories.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T004 [P] Add typed Protocol 1 request, description, event, range, and failure models in `src/EtpClient/Models/ChannelStreamingModels.cs`
- [X] T005 [P] Extend Protocol 1 message constants and codec contract members in `src/EtpClient/Protocol/EtpMessageHeader.cs` and `src/EtpClient/Protocol/IEtpSessionCodec.cs`
- [X] T006 Implement shared Protocol 1 response dispatch, correlation tracking, and lifecycle hooks in `src/EtpClient/Connection/EtpSessionManager.cs`
- [X] T007 [P] Add ChannelStreaming diagnostics and exception/log mapping in `src/EtpClient/Diagnostics/EtpClientLog.cs` and `src/EtpClient/Models/ChannelStreamingModels.cs`
- [ ] T008 [P] Record governing Protocol 1 clauses, SimpleStreamer assumptions, and public API expectations in `specs/005-channel-streaming/research.md`, `specs/005-channel-streaming/data-model.md`, and `specs/005-channel-streaming/contracts/etp-client-channel-streaming-api.md`

**Checkpoint**: Shared Protocol 1 models, codec seams, diagnostics, and session plumbing are ready for story work.

---

## Phase 3: User Story 1 - Describe Streamable Channels (Priority: P1) 🎯 MVP

**Goal**: Let callers describe Protocol 1 target URIs and receive complete typed channel definitions through the public client API and sample app.

**Independent Test**: Connect to a Protocol 1 producer, call channel description for a valid URI, and verify that the library returns the expected typed channel definitions while the sample app prints the discovered channels clearly.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T009 [P] [US1] Add binary and JSON codec tests for `ChannelDescribe` and multipart `ChannelMetadata` frames in `tests/EtpClient.UnitTests/Protocol/ChannelStreamingSessionCodecTests.cs`
- [X] T010 [P] [US1] Add integration tests for successful `DescribeChannelsAsync` flows and unsupported-URI rejection in `tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncTests.cs`
- [X] T011 [P] [US1] Add sample runner and output tests for channel-description rendering in `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs` and `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelStreamingTests.cs`

### Implementation for User Story 1

- [X] T012 [P] [US1] Implement Protocol 1 `ChannelDescribe` and `ChannelMetadata` encoding and decoding in `src/EtpClient/Protocol/ChannelDescribeMessage.cs`, `src/EtpClient/Protocol/ChannelMetadataMessage.cs`, `src/EtpClient/Protocol/BinaryEtpSessionCodec.cs`, and `src/EtpClient/Protocol/JsonEtpSessionCodec.cs`
- [X] T013 [P] [US1] Implement typed channel-definition mapping and multipart description aggregation in `src/EtpClient/Models/ChannelStreamingModels.cs` and `src/EtpClient/Connection/EtpSessionManager.cs`
- [X] T014 [US1] Add the public `DescribeChannelsAsync` workflow in `src/EtpClient/EtpClient.cs` and `src/EtpClient/Connection/EtpSessionManager.cs`
- [X] T015 [US1] Wire sample description inputs and connector calls in `samples/EtpClient.SampleConsole/SampleConsoleOptions.cs`, `samples/EtpClient.SampleConsole/IEtpConnector.cs`, `samples/EtpClient.SampleConsole/EtpConnector.cs`, and `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`
- [X] T016 [US1] Print described channel details and secret-safe describe failures in `samples/EtpClient.SampleConsole/SampleRunOutcome.cs` and `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`

**Checkpoint**: User Story 1 delivers typed channel description through the library and sample app.

---

## Phase 4: User Story 2 - Start and Control Live Streaming (Priority: P2)

**Goal**: Allow callers to start live Protocol 1 streaming, receive lifecycle events, and stop selected channels while keeping the session open.

**Independent Test**: Start a live stream for described channels, verify ordered live events and lifecycle notifications, then stop selected channels and confirm the session remains connected.

### Tests for User Story 2 (REQUIRED) ⚠️

- [X] T017 [P] [US2] Add unit tests for `ChannelStreamingStart`, `ChannelStreamingStop`, `ChannelData`, `ChannelDataChange`, `ChannelStatusChange`, and `ChannelRemove` handling in `tests/EtpClient.UnitTests/Protocol/ChannelStreamingSessionCodecTests.cs` and `tests/EtpClient.UnitTests/Connection/EtpSessionManagerChannelStreamingTests.cs`
- [X] T018 [P] [US2] Add integration tests for live stream start, selected-channel stop, and lifecycle-event delivery in `tests/EtpClient.IntegrationTests/Connection/StartChannelStreamingAsyncTests.cs`
- [X] T019 [P] [US2] Add sample runner and output tests for live-stream display and stop behavior in `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs` and `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelStreamingTests.cs`

### Implementation for User Story 2

- [X] T020 [P] [US2] Implement Protocol 1 start/stop/live-event message encoding and decoding in `src/EtpClient/Protocol/ChannelStreamingStartMessage.cs`, `src/EtpClient/Protocol/ChannelStreamingStopMessage.cs`, `src/EtpClient/Protocol/ChannelDataMessage.cs`, `src/EtpClient/Protocol/BinaryEtpSessionCodec.cs`, and `src/EtpClient/Protocol/JsonEtpSessionCodec.cs`
- [X] T021 [P] [US2] Add streaming subscription and live-event models in `src/EtpClient/Models/ChannelStreamingModels.cs`
- [X] T022 [US2] Implement the public live-stream lifecycle in `src/EtpClient/EtpClient.cs` and `src/EtpClient/Connection/EtpSessionManager.cs`
- [X] T023 [US2] Integrate live-stream start, stop, and event consumption into `samples/EtpClient.SampleConsole/IEtpConnector.cs`, `samples/EtpClient.SampleConsole/EtpConnector.cs`, `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`, and `samples/EtpClient.SampleConsole/SampleRunOutcome.cs`
- [X] T024 [US2] Add structured live-stream diagnostics and secret-safe failure handling in `src/EtpClient/Diagnostics/EtpClientLog.cs`, `src/EtpClient/Connection/EtpSessionManager.cs`, and `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`

**Checkpoint**: User Story 2 supports live streaming and selected-channel control without closing the session.

---

## Phase 5: User Story 3 - Request Historical Channel Ranges (Priority: P3)

**Goal**: Allow callers to request bounded historical Protocol 1 data, receive correlated aggregated results, and preserve reconnect-sensitive incomplete-response semantics.

**Independent Test**: Issue a range request for described channels, verify correlated data returns in index order across multipart responses, and confirm incomplete multipart results are not treated as complete after reconnect.

### Tests for User Story 3 (REQUIRED) ⚠️

- [X] T025 [P] [US3] Add unit tests for `ChannelRangeRequest` encoding, correlated `ChannelData` aggregation, and reconnect-incomplete behavior in `tests/EtpClient.UnitTests/Protocol/ChannelStreamingSessionCodecTests.cs` and `tests/EtpClient.UnitTests/Connection/EtpSessionManagerChannelStreamingTests.cs`
- [X] T026 [P] [US3] Add integration tests for successful range retrieval, multipart range responses, and reconnect-sensitive incomplete results in `tests/EtpClient.IntegrationTests/Connection/RequestChannelRangeAsyncTests.cs`

### Implementation for User Story 3

- [X] T027 [P] [US3] Implement `ChannelRangeRequest` encoding and correlated range-response decoding in `src/EtpClient/Protocol/ChannelRangeRequestMessage.cs`, `src/EtpClient/Protocol/ChannelDataMessage.cs`, `src/EtpClient/Protocol/BinaryEtpSessionCodec.cs`, and `src/EtpClient/Protocol/JsonEtpSessionCodec.cs`
- [X] T028 [P] [US3] Extend Protocol 1 models with range request, range result, and incomplete-after-reconnect state in `src/EtpClient/Models/ChannelStreamingModels.cs`
- [X] T029 [US3] Add the public `RequestChannelRangeAsync` workflow and correlation handling in `src/EtpClient/EtpClient.cs` and `src/EtpClient/Connection/EtpSessionManager.cs`
- [X] T030 [US3] Surface bounded range execution and fallback behavior in `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`, `samples/EtpClient.SampleConsole/SampleRunOutcome.cs`, and `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`

**Checkpoint**: User Story 3 supports bounded historical Protocol 1 retrieval with correct aggregation and reconnect semantics.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Align docs, guidance, and full validation across all ChannelStreaming stories.

- [X] T031 [P] Update end-to-end usage, verification steps, and API examples in `specs/005-channel-streaming/quickstart.md` and `specs/005-channel-streaming/contracts/etp-client-channel-streaming-api.md`
- [X] T032 [P] Update implementation notes, clause traceability, and repository guidance in `specs/005-channel-streaming/research.md`, `specs/005-channel-streaming/data-model.md`, and `.github/copilot-instructions.md`
- [X] T033 Run ChannelStreaming validation via `tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj`, `tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj`, and `tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup; blocks all story work.
- **User Story 1 (Phase 3)**: Depends on Foundational; defines the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational and reuses the typed description/channel identity plumbing established in User Story 1.
- **User Story 3 (Phase 5)**: Depends on Foundational and reuses shared Protocol 1 message, correlation, and channel identity plumbing from earlier stories.
- **Polish (Phase 6)**: Depends on all implemented stories.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on other user stories; start here for the MVP.
- **User Story 2 (P2)**: Uses described channels from User Story 1, but remains independently verifiable through live-stream start, stop, and lifecycle-event scenarios.
- **User Story 3 (P3)**: Uses described channels from User Story 1, but remains independently verifiable through bounded-range scenarios.

### Within Each User Story

- Tests MUST be written and shown failing before implementation.
- Protocol message and model work comes before session orchestration.
- Session orchestration comes before sample wiring.
- Documentation and quickstart updates follow completed behavior.

### Parallel Opportunities

- `T002` and `T003` can run in parallel after `T001` establishes the Protocol 1 file set.
- `T004`, `T005`, `T007`, and `T008` can run in parallel in the foundational phase.
- `T009`, `T010`, and `T011` can run in parallel for User Story 1.
- `T012` and `T013` can run in parallel once User Story 1 tests exist.
- `T017`, `T018`, and `T019` can run in parallel for User Story 2.
- `T020` and `T021` can run in parallel once User Story 2 tests exist.
- `T025` and `T026` can run in parallel for User Story 3.
- `T027` and `T028` can run in parallel once User Story 3 tests exist.
- `T031` and `T032` can run in parallel during polish.

---

## Parallel Example: User Story 1

```bash
# Launch User Story 1 tests together:
Task: "Add binary and JSON codec tests for ChannelDescribe and multipart ChannelMetadata frames in tests/EtpClient.UnitTests/Protocol/ChannelStreamingSessionCodecTests.cs"
Task: "Add integration tests for successful DescribeChannelsAsync flows and unsupported-URI rejection in tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncTests.cs"
Task: "Add sample runner and output tests for channel-description rendering in tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs and tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelStreamingTests.cs"

# Launch User Story 1 implementation tasks together after tests fail:
Task: "Implement Protocol 1 ChannelDescribe and ChannelMetadata encoding and decoding in src/EtpClient/Protocol/ChannelDescribeMessage.cs, src/EtpClient/Protocol/ChannelMetadataMessage.cs, src/EtpClient/Protocol/BinaryEtpSessionCodec.cs, and src/EtpClient/Protocol/JsonEtpSessionCodec.cs"
Task: "Implement typed channel-definition mapping and multipart description aggregation in src/EtpClient/Models/ChannelStreamingModels.cs and src/EtpClient/Connection/EtpSessionManager.cs"
```

## Parallel Example: User Story 2

```bash
# Launch User Story 2 tests together:
Task: "Add unit tests for ChannelStreamingStart, ChannelStreamingStop, ChannelData, ChannelDataChange, ChannelStatusChange, and ChannelRemove handling in tests/EtpClient.UnitTests/Protocol/ChannelStreamingSessionCodecTests.cs and tests/EtpClient.UnitTests/Connection/EtpSessionManagerChannelStreamingTests.cs"
Task: "Add integration tests for live stream start, selected-channel stop, and lifecycle-event delivery in tests/EtpClient.IntegrationTests/Connection/StartChannelStreamingAsyncTests.cs"
Task: "Add sample runner and output tests for live-stream display and stop behavior in tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs and tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelStreamingTests.cs"

# Launch User Story 2 implementation tasks together after tests fail:
Task: "Implement Protocol 1 start/stop/live-event message encoding and decoding in src/EtpClient/Protocol/ChannelStreamingStartMessage.cs, src/EtpClient/Protocol/ChannelStreamingStopMessage.cs, src/EtpClient/Protocol/ChannelDataMessage.cs, src/EtpClient/Protocol/BinaryEtpSessionCodec.cs, and src/EtpClient/Protocol/JsonEtpSessionCodec.cs"
Task: "Add streaming subscription and live-event models in src/EtpClient/Models/ChannelStreamingModels.cs"
```

## Parallel Example: User Story 3

```bash
# Launch User Story 3 tests together:
Task: "Add unit tests for ChannelRangeRequest encoding, correlated ChannelData aggregation, and reconnect-incomplete behavior in tests/EtpClient.UnitTests/Protocol/ChannelStreamingSessionCodecTests.cs and tests/EtpClient.UnitTests/Connection/EtpSessionManagerChannelStreamingTests.cs"
Task: "Add integration tests for successful range retrieval, multipart range responses, and reconnect-sensitive incomplete results in tests/EtpClient.IntegrationTests/Connection/RequestChannelRangeAsyncTests.cs"

# Launch User Story 3 implementation tasks together after tests fail:
Task: "Implement ChannelRangeRequest encoding and correlated range-response decoding in src/EtpClient/Protocol/ChannelRangeRequestMessage.cs, src/EtpClient/Protocol/ChannelDataMessage.cs, src/EtpClient/Protocol/BinaryEtpSessionCodec.cs, and src/EtpClient/Protocol/JsonEtpSessionCodec.cs"
Task: "Extend Protocol 1 models with range request, range result, and incomplete-after-reconnect state in src/EtpClient/Models/ChannelStreamingModels.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate typed channel description through the library and sample app.
5. Stop and review before expanding to live and historical flows.

### Incremental Delivery

1. Deliver typed channel description for valid Protocol 1 target URIs.
2. Add live streaming start, lifecycle events, and selected-channel stop behavior.
3. Add bounded range requests and reconnect-sensitive incomplete-result handling.
4. Finish with documentation alignment and full validation.

### Parallel Team Strategy

1. One developer handles foundational Protocol 1 message and session plumbing.
2. One developer prepares unit and integration coverage in parallel.
3. After User Story 1 lands, another developer can wire the sample app while live and range flows proceed.

---

## Notes

- [P] tasks operate on different files and avoid incomplete-task dependencies.
- User Story 1 is the recommended MVP scope.
- Protocol 1 work must remain secret-safe and preserve current Protocol 0 and Discovery behavior.
- The sample app should demonstrate describe-first usage without re-implementing protocol logic outside the library.
