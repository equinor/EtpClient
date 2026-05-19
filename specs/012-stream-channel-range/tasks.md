# Tasks: Stream Channel Range

**Input**: Design documents from `specs/012-stream-channel-range/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Feature**: Convert `RequestChannelRangeAsync` from `Task<ChannelRangeResult>` to `IAsyncEnumerable<ChannelDataItem>`. 14 file changes, no new files.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel with other [P] tasks in the same phase (different files, no mutual deps)
- **[US1]** / **[US2]**: User story the task belongs to

---

## Phase 1: Setup

No project or environment setup required. All affected projects exist. Proceed to Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Remove the old aggregate types and update the three core library files. Nothing in samples/ or tests/ can compile until this phase is complete.

**⚠️ CRITICAL**: All user story work depends on Phase 2 completion.

- [X] T001 [P] Remove `ChannelRangeResult` class and `ChannelRangeResultState` enum from `src/EtpClient/Models/ChannelStreamingModels.cs`
- [X] T002 [P] Change `RequestChannelRangeAsync` return type from `Task<ChannelRangeResult>` to `IAsyncEnumerable<ChannelDataItem>` in `src/EtpClient/IEtpClient.cs`
- [X] T003 Convert `EtpSessionManager.RequestChannelRangeAsync` to an `async IAsyncEnumerable<ChannelDataItem>` iterator in `src/EtpClient/Connection/EtpSessionManager.cs`: yield each item from each `ChannelData` frame, stop on `FinalPart` flag, catch mid-iteration `OperationCanceledException` with `yield break`, rethrow `EtpChannelStreamingException`, wrap full enumeration in OTel activity + `Stopwatch` via `try/finally` using `bool success` + `int? errorCode` flags (early-abandon and normal completion → OK status; only exception path → Error status); **do NOT catch `OperationCanceledException` from initial sends or setup** — let it propagate naturally through the iterator's `finally` block (FR-004b: a pre-cancelled token must throw, not silently `yield break`)
- [X] T004 Update `EtpClient.RequestChannelRangeAsync` in `src/EtpClient/EtpClient.cs`: keep the eager `InvalidOperationException` guard check, then `return _manager.RequestChannelRangeAsync(request, ct)` (non-iterator wrapper, mirrors `StartChannelStreamingAsync` pattern)

**Checkpoint**: `dotnet build src/EtpClient/EtpClient.csproj` passes.

---

## Phase 3: User Story 1 — Iterate channel range data as it arrives (Priority: P1) 🎯 MVP

**Goal**: Library consumers can `await foreach` over `RequestChannelRangeAsync` and receive `ChannelDataItem` values as each `ChannelData` frame arrives, without waiting for the server to send the final-part frame.

**Independent Test**: The T025 range tests in `EtpSessionManagerChannelStreamingTests.cs` pass (single-frame, multi-frame, ProtocolException, NotConnected guard, pre-cancelled token, unexpected message type); the live integration test completes without hanging.

- [X] T005 [P] [US1] Update `IEtpConnector.RequestChannelRangeAsync` return type to `IAsyncEnumerable<ChannelDataItem>` in `samples/EtpClient.SampleConsole/IEtpConnector.cs`
- [X] T006 [P] [US1] Update `EtpConnector.RequestChannelRangeAsync` to return `IAsyncEnumerable<ChannelDataItem>` and delegate directly to `_client.RequestChannelRangeAsync(request, ct)` in `samples/EtpClient.SampleConsole/EtpConnector.cs`
- [X] T007 [P] [US1] Replace `ChannelRangeResult? ChannelRangeResult` property with `IReadOnlyList<ChannelDataItem>? RangeSamples` and `ChannelRangeRequestModel? RangeRequest` in `samples/EtpClient.SampleConsole/SampleRunOutcome.cs`; update the `FromSuccess` factory accordingly
- [X] T008 [US1] Update `SampleConsoleRunner` in `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs`: replace the single `await connector.RequestChannelRangeAsync(...)` call and `channelRangeResult` variable with an `await foreach` loop that collects items into `List<ChannelDataItem>`, then sets `outcome.RangeSamples` and `outcome.RangeRequest`
- [X] T009 [US1] Update `SampleOutputWriter.WriteChannelRange` in `samples/EtpClient.SampleConsole/SampleOutputWriter.cs`: use `RangeSamples` and `RangeRequest` from `SampleRunOutcome`; remove the `WasMultipart` and `State` output lines
- [X] T010 [P] [US1] Update all T025 range tests in `tests/EtpClient.UnitTests/Connection/EtpSessionManagerChannelStreamingTests.cs` to use `await foreach` pattern: `_ServerReturnsSinglePartResponse_*`, `_MultipartResponse_*` → collect items and assert count/values; `_ProtocolException_*` → `await Assert.ThrowsAsync<EtpChannelStreamingException>` over a full enumeration; `_NotConnected_*` → `await Assert.ThrowsAsync<InvalidOperationException>` on first `MoveNextAsync`; **add** `_PreCancelledToken_ThrowsOperationCanceledException` — pass a pre-cancelled `CancellationToken`, assert `OperationCanceledException` is thrown without any frame sent to the transport (SC-004, FR-004b); **add** `_UnexpectedMessageType_ThrowsEtpChannelStreamingException` — configure stub to return a non-`ChannelData`/non-`ProtocolException` message type correlated to the request, assert `EtpChannelStreamingException` is thrown (FR-006, US2 AC-2)
- [X] T011 [US1] Update `LiveRequestChannelRangeAsyncTests` in `tests/EtpClient.IntegrationTests/Connection/LiveRequestChannelRangeAsyncTests.cs`: replace `var result = await client.RequestChannelRangeAsync(request)` with an `await foreach` loop; assert that **enumeration completes without throwing** (remove `State == Completed` and `WasMultipart` assertions); item count may be zero if the server has no data in the requested range — the key assertion is completion-not-timeout (SC-002)
- [X] T012 [P] [US1] Update `SampleConsoleRunnerChannelStreamingTests` in `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs`: replace `Assert.Null(outcome.ChannelRangeResult)` with `Assert.Null(outcome.RangeSamples)` (or `Assert.Null(outcome.RangeRequest)` as appropriate)

**Checkpoint**: `dotnet test tests/EtpClient.UnitTests` and `dotnet test tests/EtpClient.SampleConsole.Tests` pass.

---

## Phase 4: User Story 2 — Error propagation during streaming (Priority: P2)

**Goal**: When the server sends a `ProtocolException` or an unexpected message type, `EtpChannelStreamingException` is thrown from the enumerator. OTel spans record Error status for exceptions and OK status for early caller abandonment.

**Independent Test**: Instrumentation tests for range spans pass: success span has OK status and `channel.count` tag; error span has Error status and `etp.error_code` tag; early-abandon span has OK status.

- [X] T013 [US2] Update range span tests in `tests/EtpClient.UnitTests/Instrumentation/ChannelOperationInstrumentationTests.cs`: change `RequestChannelRangeAsync_Success_*` to use `await foreach`; update `RequestChannelRangeAsync_ProtocolException_*` to assert `EtpChannelStreamingException` thrown from enumerator; add or update `RequestChannelRangeAsync_EarlyAbandon_*` to assert span status is OK when the caller breaks before `FinalPart`

**Checkpoint**: `dotnet test tests/EtpClient.UnitTests` passes including all instrumentation range tests.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T014 Update `README.md`: change the `RequestChannelRangeAsync` description and any code snippets from `Task<ChannelRangeResult>` / `result.Samples` to `IAsyncEnumerable<ChannelDataItem>` / `await foreach`; remove references to `ChannelRangeResult`, `ChannelRangeResultState`, and `WasMultipart`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: No dependencies — start immediately. **BLOCKS** all other phases.
- **US1 (Phase 3)**: Depends on Phase 2 completion.
- **US2 (Phase 4)**: Depends on Phase 2 completion. Can run in parallel with Phase 3.
- **Polish (Phase 5)**: Can run after T002 (API finalised); does not depend on tests passing.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2. No dependency on US2.
- **US2 (P2)**: Can start after Phase 2. No dependency on US1.

### Within Phase 2

```
T001 [P] ─┐
T002 [P] ─┤─► T003 ──► T004
           └─(T003 uses the interface defined by T002 and the removed types from T001)
```

### Within Phase 3

```
T005 [P] ─┐
T006 [P] ─┤  (all independent, different files)
T007 [P] ─┤─► T008 ──► T012
           └─► T009 ──┘

T010 [P] ─── (independent, depends only on Phase 2)
T011      ─── (independent, depends only on Phase 2)
```

---

## Parallel Execution Examples

### Phase 2 (start in parallel)

```
Agent A: T001 — remove ChannelRangeResult, ChannelRangeResultState from ChannelStreamingModels.cs
Agent B: T002 — update IEtpClient signature
→ then: T003 — implement EtpSessionManager iterator (most complex task)
→ then: T004 — update EtpClient wrapper
```

### Phase 3 (start in parallel once Phase 2 is done)

```
Agent A: T005 → T006 → T008 → T012
Agent B: T007 → T009
Agent C: T010  (unit tests)
Agent D: T011  (integration test)
```

### Phase 4 (can overlap with Phase 3)

```
Agent A: T013 — instrumentation tests
```

---

## Implementation Strategy

**MVP scope**: Phase 2 + Phase 3 (T001–T012). This delivers a fully working streaming API with sample console support and unit/integration test coverage for the happy path and guard conditions.

**Suggested task order for single-developer execution**:
1. T001, T002 (parallel — quick edits)
2. T003 (core iterator — most effort)
3. T004 (quick wrapper update)
4. T005, T006, T007 (parallel — signature/model changes)
5. T008, T009 (sequential — depend on T007)
6. T010, T011 (parallel — test updates)
7. T012 (test assertion update)
8. T013 (instrumentation tests)
9. T014 (README)
