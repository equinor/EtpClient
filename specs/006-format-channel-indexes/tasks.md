---
description: "Task list for Format Channel Index Output"
---

# Tasks: Format Channel Index Output

**Branch**: `006-format-channel-indexes`  
**Input**: Design documents from `/specs/006-format-channel-indexes/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED. Include unit, integration, and sample-output tests for metadata preservation, time conversion, depth conversion, and fallback rendering.

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
- **Feature docs**: `specs/006-format-channel-indexes/`

---

## Phase 1: Setup

**Purpose**: Establish the feature-specific test and documentation entry points for index interpretation work.

- [ ] T001 Create feature-specific unit test scaffolding in `tests/EtpClient.UnitTests/Models/ChannelIndexValueConverterTests.cs` and `tests/EtpClient.UnitTests/Protocol/ChannelMetadataMessageIndexMetadataTests.cs`
- [ ] T002 [P] Create sample-output test scaffolding in `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelIndexFormattingTests.cs`
- [ ] T003 [P] Create integration test scaffolding for metadata propagation in `tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncIndexMetadataTests.cs`

**Checkpoint**: The feature has dedicated coverage locations for library, integration, and sample behavior.

---

## Phase 2: Foundational

**Purpose**: Preserve index metadata and add shared conversion primitives needed by all stories.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [ ] T004 [P] Extend channel metadata models with primary-index interpretation fields in `src/EtpClient/Models/ChannelStreamingModels.cs`
- [ ] T005 [P] Preserve binary `IndexMetadataRecord` fields (`scale`, `timeDatum`, `depthDatum`, mnemonic, description`) in `src/EtpClient/Protocol/ChannelMetadataMessage.cs`
- [ ] T006 [P] Preserve JSON `IndexMetadataRecord` fields (`scale`, `timeDatum`, `depthDatum`, mnemonic, description`) in `src/EtpClient/Protocol/JsonEtpSessionCodec.cs`
- [ ] T007 Implement reusable primary-index interpretation helpers in `src/EtpClient/Models/ChannelIndexValueConverter.cs`
- [ ] T008 [P] Add foundational tests for metadata retention and helper behavior in `tests/EtpClient.UnitTests/Models/ChannelIndexValueConverterTests.cs` and `tests/EtpClient.UnitTests/Protocol/ChannelMetadataMessageIndexMetadataTests.cs`
- [ ] T009 [P] Record feature-specific protocol semantics and API expectations in `specs/006-format-channel-indexes/research.md`, `specs/006-format-channel-indexes/data-model.md`, and `specs/006-format-channel-indexes/contracts/etp-client-channel-index-formatting-api.md`

**Checkpoint**: `ChannelDefinition` preserves the required ETP index metadata, and one shared helper can interpret raw primary index values without changing `ChannelDataItem.Indexes`.

---

## Phase 3: User Story 1 - Read Time-Indexed Samples Clearly (Priority: P1) 🎯 MVP

**Goal**: Show time-indexed channel data as human-readable local-time timestamps instead of raw long values.

**Independent Test**: Run the sample against a time-indexed channel or equivalent test fixture and verify that values such as `1775845444000000` are rendered as readable local-time timestamps in both live and historical output.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T010 [P] [US1] Add unit tests for UTC-epoch and `timeDatum`-based time interpretation in `tests/EtpClient.UnitTests/Models/ChannelIndexValueConverterTests.cs`
- [ ] T011 [P] [US1] Add integration tests that verify `DescribeChannelsAsync` preserves time index metadata in `tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncIndexMetadataTests.cs`
- [ ] T012 [P] [US1] Add sample output tests for local-time rendering of time-indexed live and range data in `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelIndexFormattingTests.cs` and `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs`

### Implementation for User Story 1

- [ ] T013 [P] [US1] Add time-index interpretation result types and helper APIs in `src/EtpClient/Models/ChannelIndexValueConverter.cs` and `src/EtpClient/Models/ChannelStreamingModels.cs`
- [ ] T014 [US1] Route sample live-data formatting through the shared time-index formatter in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`
- [ ] T015 [US1] Extend historical range output to print formatted time-indexed sample lines in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs` and `samples/EtpClient.SampleConsole/SampleRunOutcome.cs`
- [ ] T016 [US1] Ensure the sample runner provides the channel metadata needed for time formatting during live and range output in `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`

**Checkpoint**: Time-indexed channels print readable local timestamps in both live and range output without changing the raw public channel-data payload shape.

---

## Phase 4: User Story 2 - Read Depth-Indexed Samples Clearly (Priority: P2)

**Goal**: Show depth-indexed channel data as correctly scaled depth values instead of raw long values.

**Independent Test**: Run the sample against a depth-indexed channel or equivalent fixture and verify that values such as `403675000` are rendered as scaled depth output such as `4036,75m`, with the correct precision preserved.

### Tests for User Story 2 (REQUIRED) ⚠️

- [ ] T017 [P] [US2] Add unit tests for scale-based depth interpretation and precision preservation in `tests/EtpClient.UnitTests/Models/ChannelIndexValueConverterTests.cs`
- [ ] T018 [P] [US2] Add integration tests that verify `DescribeChannelsAsync` preserves depth scale and datum metadata in `tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncIndexMetadataTests.cs`
- [ ] T019 [P] [US2] Add sample output tests for scaled depth rendering in `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelIndexFormattingTests.cs` and `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs`

### Implementation for User Story 2

- [ ] T020 [P] [US2] Add depth-index interpretation and unit-aware formatting support in `src/EtpClient/Models/ChannelIndexValueConverter.cs`
- [ ] T021 [US2] Render depth-indexed live and range sample lines using preserved scale and UOM metadata in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`
- [ ] T022 [US2] Populate or preserve any additional sample-facing depth metadata needed by the output path in `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs` and `src/EtpClient/Models/ChannelStreamingModels.cs`

**Checkpoint**: Depth-indexed channels print scaled depth values with meaningful precision in both live and range output.

---

## Phase 5: User Story 3 - Keep Non-Time and Non-Depth Output Trustworthy (Priority: P3)

**Goal**: Preserve a clear fallback representation when index metadata is missing, unsupported, or cannot be interpreted confidently.

**Independent Test**: Run the sample against channels with unsupported or incomplete index metadata and verify that the output remains readable without falsely claiming time or depth semantics.

### Tests for User Story 3 (REQUIRED) ⚠️

- [ ] T023 [P] [US3] Add unit tests for unsupported-index and incomplete-metadata fallback behavior in `tests/EtpClient.UnitTests/Models/ChannelIndexValueConverterTests.cs`
- [ ] T024 [P] [US3] Add sample output tests for fallback rendering in `tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelIndexFormattingTests.cs`
- [ ] T025 [P] [US3] Add integration coverage for incomplete index metadata propagation in `tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncIndexMetadataTests.cs`

### Implementation for User Story 3

- [ ] T026 [P] [US3] Add fallback interpretation branches and explicit non-misleading output states in `src/EtpClient/Models/ChannelIndexValueConverter.cs`
- [ ] T027 [US3] Render fallback values consistently for live and range output in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`
- [ ] T028 [US3] Document fallback expectations and example scenarios in `specs/006-format-channel-indexes/quickstart.md` and `specs/006-format-channel-indexes/contracts/etp-client-channel-index-formatting-api.md`

**Checkpoint**: Unsupported or incomplete metadata never produces misleading time or depth output.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize shared behavior, examples, and end-to-end validation across all stories.

- [ ] T029 [P] Refine sample formatting consistency, including shared line formatting and culture-aware output behavior, in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`
- [ ] T030 [P] Update repository guidance and feature docs in `specs/006-format-channel-indexes/quickstart.md`, `specs/006-format-channel-indexes/research.md`, and `/Users/LGEIR/src/etp_test/.github/copilot-instructions.md`
- [ ] T031 Run feature validation via `tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj`, `tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj`, and `tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup; blocks all story work.
- **User Story 1 (Phase 3)**: Depends on Foundational; defines the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational and reuses the metadata and helper infrastructure established earlier.
- **User Story 3 (Phase 5)**: Depends on Foundational and reuses the shared interpretation and rendering pipeline.
- **Polish (Phase 6)**: Depends on all implemented stories.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on other user stories; start here for the MVP.
- **User Story 2 (P2)**: Depends on the foundational metadata-preservation work but remains independently testable as a depth-formatting slice.
- **User Story 3 (P3)**: Depends on the foundational metadata-preservation work but remains independently testable as a fallback-behavior slice.

### Within Each User Story

- Tests MUST be written and shown failing before implementation.
- Metadata preservation and conversion logic comes before sample-output integration.
- Sample-output integration comes before final documentation polish.
- Each story should be validated independently before moving on.

### Parallel Opportunities

- `T001`, `T002`, and `T003` can run in parallel.
- `T004`, `T005`, `T006`, and `T009` can run in parallel once the target files are identified.
- `T010`, `T011`, and `T012` can run in parallel for User Story 1.
- `T017`, `T018`, and `T019` can run in parallel for User Story 2.
- `T023`, `T024`, and `T025` can run in parallel for User Story 3.
- `T029` and `T030` can run in parallel during polish.

---

## Parallel Example: User Story 1

```bash
# Launch User Story 1 tests together:
Task: "Add unit tests for UTC-epoch and timeDatum-based time interpretation in tests/EtpClient.UnitTests/Models/ChannelIndexValueConverterTests.cs"
Task: "Add integration tests that verify DescribeChannelsAsync preserves time index metadata in tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncIndexMetadataTests.cs"
Task: "Add sample output tests for local-time rendering of time-indexed live and range data in tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelIndexFormattingTests.cs and tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs"

# Launch User Story 1 implementation work together after tests fail:
Task: "Add time-index interpretation result types and helper APIs in src/EtpClient/Models/ChannelIndexValueConverter.cs and src/EtpClient/Models/ChannelStreamingModels.cs"
Task: "Route sample live-data formatting through the shared time-index formatter in samples/EtpClient.SampleConsole/SampleOutputWriter.cs"
```

## Parallel Example: User Story 2

```bash
# Launch User Story 2 tests together:
Task: "Add unit tests for scale-based depth interpretation and precision preservation in tests/EtpClient.UnitTests/Models/ChannelIndexValueConverterTests.cs"
Task: "Add integration tests that verify DescribeChannelsAsync preserves depth scale and datum metadata in tests/EtpClient.IntegrationTests/Connection/DescribeChannelsAsyncIndexMetadataTests.cs"
Task: "Add sample output tests for scaled depth rendering in tests/EtpClient.SampleConsole.Tests/SampleOutputWriterChannelIndexFormattingTests.cs and tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs"

# Launch User Story 2 implementation work together after tests fail:
Task: "Add depth-index interpretation and unit-aware formatting support in src/EtpClient/Models/ChannelIndexValueConverter.cs"
Task: "Render depth-indexed live and range sample lines using preserved scale and UOM metadata in samples/EtpClient.SampleConsole/SampleOutputWriter.cs"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Validate time-indexed live and range output independently.

### Incremental Delivery

1. Complete Setup + Foundational → metadata retention and conversion helpers are ready.
2. Add User Story 1 → validate time formatting.
3. Add User Story 2 → validate depth formatting.
4. Add User Story 3 → validate fallback safety.
5. Finish polish and run the full test suite.

### Parallel Team Strategy

1. Complete Setup + Foundational together.
2. Once Foundational is complete:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Integrate and validate using the shared sample-output path.
