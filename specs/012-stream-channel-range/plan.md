# Implementation Plan: Stream Channel Range

**Branch**: `012-stream-channel-range` | **Date**: 2026-05-19 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/012-stream-channel-range/spec.md`

**Path Rule**: Use repository-relative paths only. Never include machine-local absolute filesystem paths in generated artifacts.

## Summary

Convert `RequestChannelRangeAsync` from a `Task<ChannelRangeResult>` aggregator to an
`IAsyncEnumerable<ChannelDataItem>` streaming iterator, yielding each data point as its
`ChannelData` frame arrives. The ETP Protocol 1 wire format is unchanged; only the .NET API
shape and its call sites (14 files) change. `ChannelRangeResult` and `ChannelRangeResultState`
are removed from the public API. OpenTelemetry instrumentation and structured log events are
preserved, with activity lifetime spanning the full enumeration.

## Technical Context

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `System.Net.WebSockets.ClientWebSocket`, `Microsoft.Extensions.Logging.Abstractions`, existing internal Avro reader/writer helpers, `System.Runtime.CompilerServices.EnumeratorCancellation`  
**Storage**: N/A  
**Testing**: xUnit + NSubstitute; in-process stub transport for unit tests; `ITestOutputHelper` for diagnostic output  
**Target Platform**: .NET 10 class library  
**Project Type**: library  
**Performance Goals**: Network-bound; no throughput targets. Streaming reduces peak memory relative to aggregation.  
**Constraints**: No new external dependencies. Breaking API change — no backward-compatibility shim.  
**Scale/Scope**: 14 file changes across `src/`, `samples/`, `tests/`, and `README.md`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — all gates pass.*

- ✅ **Protocol fidelity**: ETP v1.1 §7.3 ChannelStreaming protocol. Message type 9 (`ChannelRangeRequest`), message type 3 (`ChannelData`), `FinalPart` flag (`MessageFlags & 0x02`) — all unchanged. Documented in R-001.
- ✅ **Secure session handling**: No authentication surface changes. Existing credential handling is unaffected.
- ✅ **Async streaming**: `IAsyncEnumerable<ChannelDataItem>` with `[EnumeratorCancellation]`. Disposal semantics explicit via iterator `DisposeAsync`. Documented in R-002, R-003.
- ✅ **Mandatory test coverage**: Unit tests (T025 group in `EtpSessionManagerChannelStreamingTests`, instrumentation tests) and integration test (`LiveRequestChannelRangeAsyncTests`) both updated.
- ✅ **Diagnosable behavior**: `EtpClientLog.RangeRequestStarted/Completed/Failed` events preserved. OTel `Activity` spans full enumeration. Error codes surfaced via `EtpChannelStreamingException`. Documented in R-004.
- ✅ **Breaking change documented**: FR-010 explicitly removes `ChannelRangeResult` and `ChannelRangeResultState`. Noted in Assumptions as intentional with no shim.

## Project Structure

### Documentation (this feature)

```text
specs/012-stream-channel-range/
├── plan.md              ← this file
├── research.md          ← Phase 0 complete
├── data-model.md        ← Phase 1 complete
├── quickstart.md        ← Phase 1 complete
├── contracts/
│   └── public-api.md    ← Phase 1 complete
├── checklists/
│   └── requirements.md  ← pre-existing
└── tasks.md             ← Phase 2 (created by /speckit.tasks)
```

### Source Code (affected files)

```text
src/
└── EtpClient/
    ├── Models/
    │   └── ChannelStreamingModels.cs      # remove ChannelRangeResult, ChannelRangeResultState
    ├── IEtpClient.cs                       # signature: Task<ChannelRangeResult> → IAsyncEnumerable<ChannelDataItem>
    ├── EtpClient.cs                        # same signature change; guard check + delegate to manager
    └── Connection/
        └── EtpSessionManager.cs            # convert to async IAsyncEnumerable iterator

samples/
└── EtpClient.SampleConsole/
    ├── IEtpConnector.cs                    # signature change
    ├── EtpConnector.cs                     # signature change
    ├── SampleRunOutcome.cs                 # replace ChannelRangeResult? with RangeSamples + RangeRequest
    ├── SampleConsoleRunner.cs              # await foreach collection loop
    └── SampleOutputWriter.cs               # update WriteChannelRange

tests/
├── EtpClient.UnitTests/
│   ├── Connection/
│   │   └── EtpSessionManagerChannelStreamingTests.cs   # T025 group: await foreach pattern
│   └── Instrumentation/
│       └── ChannelOperationInstrumentationTests.cs      # range span tests: await foreach
├── EtpClient.IntegrationTests/
│   └── Connection/
│       └── LiveRequestChannelRangeAsyncTests.cs         # await foreach + item assertions
└── EtpClient.SampleConsole.Tests/
    └── SampleConsoleRunnerChannelStreamingTests.cs       # RangeSamples assertion

README.md                                                  # update RequestChannelRangeAsync description
```

**Structure decision**: No new files. All changes are in-place edits to existing files. The iterator pattern in `EtpSessionManager` mirrors the existing `StartChannelStreamingAsync` method exactly. The `EtpClient` public class follows the same non-iterator wrapper pattern used by `StartChannelStreamingAsync` (guard check → return `_manager.RequestChannelRangeAsync(request, ct)`).

## Key Design Decisions

| Decision | Rationale |
|---|---|
| Single `async IAsyncEnumerable` iterator in `EtpSessionManager` | Mirrors `StartChannelStreamingAsync`; keeps OTel inside the iterator |
| `bool success` + `int? errorCode` flags in `finally` | `caughtEx` pattern not compatible with `yield return` inside `try`; flags are set before rethrowing in `catch` |
| Mid-iteration cancellation → `yield break` (no exception) | Consistent with `StartChannelStreamingAsync`; `OperationCanceledException` from inner `ReceiveFullFrameAsync` caught in the frame loop |
| Pre-cancelled token → propagate `OperationCanceledException` | Natural propagation from first `await` (in `EnsureChannelStreamingProtocolStartedAsync`); no extra guard needed |
| Early caller abandon (break) → OTel span status OK | Early abandonment is a valid caller decision; only `EtpChannelStreamingException` and unexpected exceptions set Error status |
| `EtpClient` guard check is eager (non-iterator wrapper) | Preserves `InvalidOperationException` on first call, not deferred to first `MoveNextAsync()` |
