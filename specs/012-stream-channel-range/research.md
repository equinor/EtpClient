# Research: Stream Channel Range

**Feature**: 012-stream-channel-range  
**Phase**: 0 — Resolve unknowns before design

---

## R-001 — ETP Protocol 1 wire behavior for ChannelRangeRequest / ChannelData

**Decision**: No wire protocol changes. The `ChannelRangeRequest` (Protocol 1, message type 9) and `ChannelData` (message type 3) exchange is unchanged. The `FinalPart` flag on a `ChannelData` message header (`MessageFlags & 0x02`) remains the sole signal that the range response is complete.

**Rationale**: The change is a .NET API shape change only. The Avro encoding, message types, and frame-level sequencing documented in docs/ETP_Specification_v1.1_Doc_v1.1.md §7.3 (ChannelStreaming protocol) stay identical.

**Alternatives considered**: None — protocol wire format is fixed by the spec.

---

## R-002 — `IAsyncEnumerable` + `try/catch/finally` in C# async iterators

**Decision**: Use a single `async IAsyncEnumerable<ChannelDataItem>` iterator method in `EtpSessionManager`. The OTel `Activity` and `Stopwatch` are local variables inside the iterator, and a `try/finally` block ensures the duration metric and error counter are always recorded.

**Rationale**: C# 8+ (and .NET 10) fully supports:
- `yield return` inside `try` blocks (including `try` blocks that have a corresponding `finally`).
- `yield break` inside `catch` blocks (used for cancellation).
- `throw` / rethrowing exceptions from `catch` blocks.
- `using var` disposables at iterator scope (disposed via the iterator's implicit `DisposeAsync`).

The `finally` block is guaranteed to run on normal completion, `yield break`, and unhandled exceptions — satisfying FR-008 and FR-009.

**Key constraint**: `yield return` is not permitted inside a `catch` block. Decoding exceptions are always rethrown, never yielded from, so this constraint is not violated.

**Alternatives considered**: Wrapper method that owns activity + delegates to private iterator. Rejected because it adds structural complexity without benefit; the single-iterator approach is identical to `StartChannelStreamingAsync`.

---

## R-003 — Cancellation handling pattern

**Decision**: Wrap `ReceiveFullFrameAsync` in an inner `try/catch (OperationCanceledException)` that does `yield break`. Cancellation of `EnsureChannelStreamingProtocolStartedAsync` or `SendFrameAsync` is NOT caught — it propagates out as `OperationCanceledException` through the iterator's `finally` block. This matches the existing `StartChannelStreamingAsync` pattern exactly.

**Rationale**: Early sends must fail fast on cancellation. Frame-receive cancellation during the read loop terminates cleanly without an exception visible to the `await foreach` caller, consistent with the existing pattern.

**Alternatives considered**: Using `ct.ThrowIfCancellationRequested()` as the cancellation mechanism. Rejected because `ReceiveFullFrameAsync` already propagates `OperationCanceledException` from the underlying WebSocket; catching it in the frame loop is simpler.

---

## R-004 — OTel Activity lifetime across async iterator

**Decision**: Declare `activity` with `using var` at the top of the iterator. The activity is disposed when the iterator's `IAsyncDisposable.DisposeAsync()` is called, which `await foreach` does automatically at end of loop or on break. Duration metric is recorded in a `finally` block using a `bool success` flag instead of the current `Exception? caughtEx` pattern.

**Rationale**: A `using var` in an async iterator body is subject to the same disposal semantics as `try/finally` — it runs on iterator disposal, whether via normal completion, `yield break`, or exception propagation. The current `caughtEx`/`caughtEtpErrorCode` tracking pattern is replaced by a `bool success` + `int? errorCode` pair because the error-type and OTel-status are set in the `catch` blocks before rethrowing.

**Alternatives considered**: Wrapping the OTel activity outside the iterator (e.g., in `EtpClient.RequestChannelRangeAsync`). Rejected because all OTel instrumentation for channel operations currently lives in `EtpSessionManager`; splitting it would break the established pattern.

---

## R-005 — Affected call sites

**Decision**: Twelve files require changes. No new files need to be created.

| File | Change type |
|---|---|
| `src/EtpClient/Models/ChannelStreamingModels.cs` | Remove `ChannelRangeResult`, `ChannelRangeResultState`; no `WasMultipart` to remove (it's a property, not a type) |
| `src/EtpClient/IEtpClient.cs` | Signature: `Task<ChannelRangeResult>` → `IAsyncEnumerable<ChannelDataItem>` |
| `src/EtpClient/EtpClient.cs` | Same signature change; return `_manager.RequestChannelRangeAsync(request, ct)` directly |
| `src/EtpClient/Connection/EtpSessionManager.cs` | Convert to `async IAsyncEnumerable<ChannelDataItem>` iterator |
| `samples/EtpClient.SampleConsole/IEtpConnector.cs` | Signature change |
| `samples/EtpClient.SampleConsole/EtpConnector.cs` | Signature change |
| `samples/EtpClient.SampleConsole/SampleRunOutcome.cs` | Replace `ChannelRangeResult?` with `IReadOnlyList<ChannelDataItem>? RangeSamples` + `ChannelRangeRequestModel? RangeRequest` |
| `samples/EtpClient.SampleConsole/SampleConsoleRunner.cs` | `await foreach` collection loop |
| `samples/EtpClient.SampleConsole/SampleOutputWriter.cs` | Update `WriteChannelRange` |
| `tests/EtpClient.UnitTests/Connection/EtpSessionManagerChannelStreamingTests.cs` | `await foreach` in T025 range tests; remove `WasMultipart`/`State` assertions |
| `tests/EtpClient.UnitTests/Instrumentation/ChannelOperationInstrumentationTests.cs` | `await foreach` in range instrumentation tests |
| `tests/EtpClient.IntegrationTests/Connection/LiveRequestChannelRangeAsyncTests.cs` | `await foreach` + assert items directly |
| `tests/EtpClient.SampleConsole.Tests/SampleConsoleRunnerChannelStreamingTests.cs` | Update `ChannelRangeResult` assertion to `RangeSamples` |
| `README.md` | Update `RequestChannelRangeAsync` description to streaming pattern |

**Alternatives considered**: Keeping `ChannelRangeResult` as an optional aggregated result alongside the stream. Rejected — spec FR-010 explicitly removes it.

---

## R-006 — `SampleRunOutcome` replacement for `ChannelRangeResult`

**Decision**: Replace the single `ChannelRangeResult? ChannelRangeResult` property with two properties:
- `ChannelRangeRequestModel? RangeRequest`
- `IReadOnlyList<ChannelDataItem>? RangeSamples`

`SampleConsoleRunner` collects items with `await foreach` into a `List<ChannelDataItem>` before setting these properties. `SampleOutputWriter.WriteChannelRange` is updated to use the new properties; the `WasMultipart` and `State` output lines are removed.

**Rationale**: The SampleConsole is a demonstration tool that wants to display collected data. Collecting into a list before building the outcome is the simplest adaptation and mirrors the prior behavior without introducing a new model type.

**Alternatives considered**: Store items in `SampleRunOutcome` as they arrive (streaming output). Rejected because the output writer runs after the run completes — the sample's output model is not designed for streaming display.
