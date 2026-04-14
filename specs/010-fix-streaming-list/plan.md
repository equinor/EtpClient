# Implementation Plan: Fix Streaming List

**Branch**: `010-fix-streaming-list` | **Date**: 2026-04-14 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/010-fix-streaming-list/spec.md`

**Note**: This plan covers Phase 0 research and Phase 1 design artifacts for changing the explorer stream view from append-only event rendering to a fixed, alphabetically ordered channel list with in-place updates.

## Summary

Change `EtpExplorer` streaming from line-by-line event output to a live fixed-row view. The implementation will build a session-local row snapshot for the selected endpoints at stream start, sort rows alphabetically by channel name once, initialize each row with `Waiting for data`, and update dedicated status/index/value fields in place as `ChannelData`, `ChannelDataChange`, `ChannelStatusChange`, and `ChannelRemove` events arrive. The protocol-facing `EtpClient` library behavior stays unchanged; the work is confined to the explorer sample UI, streaming orchestration, and explorer tests.

## Technical Context

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `EtpClient` project reference, `Spectre.Console`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Logging.Console`; test-time use of xUnit and fake explorer UI seams in `tests/EtpExplorer.Tests`  
**Storage**: N/A  
**Testing**: xUnit in `tests/EtpExplorer.Tests`; existing library unit/integration coverage remains authoritative for wire semantics  
**Target Platform**: Cross-platform terminal application on macOS, Linux, and Windows with .NET 10  
**Project Type**: Console sample application plus dedicated test project  
**Performance Goals**: Stream startup should render the full fixed list immediately, row updates should feel instantaneous for typical developer validation loads, and the view should remain readable while at least 5 channels update concurrently  
**Constraints**: No ETP wire-format or public library API changes; row set remains fixed during the session; rows are alphabetically ordered by channel name; rows start in `Waiting for data`; lifecycle state is shown in a dedicated status field; removed channels stay visible until the stream ends; user-visible sample workflow changes require README review  
**Scale/Scope**: `samples/EtpExplorer` streaming models/UI/orchestration, `tests/EtpExplorer.Tests` rendering/workflow coverage, and documentation artifacts for this feature

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **PASS**: Relevant ETP v1.1 clauses and message types are already in use and remain unchanged: Protocol 1 `ChannelStreamingStart`, `ChannelData`, `ChannelDataChange`, `ChannelStatusChange`, and `ChannelRemove` semantics continue to drive the explorer’s live view; this feature changes only sample-side presentation behavior.
- **PASS**: Basic authentication handling stays explicit and secret-safe because explorer startup, user-secrets loading, and connection options are unchanged.
- **PASS**: Public API usage remains asynchronous and cancellation-aware because the explorer continues consuming `IAsyncEnumerable<ChannelEvent>` from `StartChannelStreamingAsync` and uses explicit cancellation/stop semantics.
- **PASS**: Required tests are identified in `tests/EtpExplorer.Tests` for fixed-row initialization, alphabetical ordering, in-place updates, waiting state, dedicated status updates, and stream-stop behavior; no new protocol-wire integration suite is needed.
- **PASS**: Diagnostics remain secret-safe by surfacing stream lifecycle and rendering-state changes through sample UI status messages without echoing credentials.
- **PASS**: No protocol deviation or breaking library change is planned; the change is additive and sample-scoped.

### Post-Design Constitution Check

- **PASS**: The design stays sample-local and does not alter Protocol 1 request/response framing or negotiated behavior.
- **PASS**: Secret handling remains unchanged because no new configuration or logging surfaces are introduced.
- **PASS**: Async streaming semantics remain intact; a snapshot builder consumes the existing event stream and updates fixed rows without changing cancellation or disposal semantics.
- **PASS**: Test coverage is explicitly planned in the existing explorer test project for UI/state behavior rather than duplicate wire-level checks.
- **PASS**: Diagnostics remain actionable and secret-safe through explicit row state plus existing status/error messages.
- **PASS**: No protocol deviation or versioning impact is introduced; the change is a presentation-layer refinement to the explorer sample.

## Project Structure

### Documentation (this feature)

```text
specs/010-fix-streaming-list/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── explorer-fixed-stream-view.md
└── tasks.md
```

### Source Code (repository root)

```text
samples/
└── EtpExplorer/
    ├── ExplorerApp.cs
    ├── ExplorerModels.cs
    ├── ExplorerStreamingService.cs
    ├── IExplorerUi.cs
    ├── SpectreExplorerUi.cs
    └── StreamEventFormatter.cs

tests/
└── EtpExplorer.Tests/
    ├── ExplorerStreamRenderingTests.cs
    ├── ExplorerStreamingWorkflowTests.cs
    └── TestSupport/
        └── FakeExplorerUi.cs

docs/
├── ETP_Specification_v1.1_Doc_v1.1.md
└── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md
```

**Structure Decision**: Keep the feature entirely inside the existing explorer sample and test project. `samples/EtpExplorer` owns the streaming snapshot/state/rendering logic, while `tests/EtpExplorer.Tests` extends the current fake-UI workflow and rendering seams. No new shared project or library API layer is needed for this slice.

## Complexity Tracking

No constitution violations or exceptional complexity require formal justification.

