# Implementation Plan: Search Active Explorer Column

**Branch**: `008-search-column-filter` | **Date**: 2026-04-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/008-search-column-filter/spec.md`

**Note**: This plan covers Phase 0 research and Phase 1 design artifacts for adding active-column search and filtering, including `*` wildcard matching, to the explorer sample application.

## Summary

Add search and filter behavior to the focused browse column in `EtpExplorer` without changing ETP wire behavior. The implementation will keep filter state scoped to each `ExplorerBrowseColumn`, preserve the unfiltered resource list so the user can clear terms without rediscovery, extend the keyboard-driven Spectre UI with a focused search interaction, and add workflow/rendering tests in `EtpExplorer.Tests` for plain-text, wildcard, no-match, and selection-preservation scenarios.

## Technical Context

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `Spectre.Console`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Logging.Console`, `EtpClient` project reference  
**Storage**: N/A  
**Testing**: xUnit, NSubstitute, `Microsoft.NET.Test.Sdk`, `coverlet.collector` in `tests/EtpExplorer.Tests`  
**Target Platform**: Cross-platform terminal application on .NET 10 (macOS, Linux, Windows)  
**Project Type**: Console application sample plus dedicated test project  
**Performance Goals**: Search/filter updates should feel immediate during interactive browsing and avoid requiring a fresh discovery round-trip when terms change or clear.  
**Constraints**: Search/filter scope is limited to the currently focused browse column; `*` is the only wildcard; existing discovery, authentication, and streaming behavior must remain unchanged; keyboard navigation and secret-safe diagnostics must continue to work.  
**Scale/Scope**: One sample app (`samples/EtpExplorer`), one test project (`tests/EtpExplorer.Tests`), and a small set of browse/session models and UI interactions.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Protocol fidelity**: Pass. This feature is local explorer UI state only and introduces no new ETP message types or wire semantics. Existing Discovery and ChannelStreaming flows remain unchanged and continue to rely on prior protocol-backed features.
- **Secure session handling**: Pass. Authentication inputs, transport assumptions, and connection setup remain in existing `ExplorerOptions` and `EtpConnectionOptions`; this feature does not add credential surfaces.
- **Async API design**: Pass. The feature remains within the current async explorer workflow and does not introduce blocking network operations or change streaming/disposal semantics.
- **Required tests**: Pass. Unit/workflow coverage is planned in `tests/EtpExplorer.Tests` for filtering, wildcard matching, no-results feedback, and selection/index behavior.
- **Diagnostics**: Pass. User-visible status messaging will distinguish empty discovery results from no-match filter results without exposing secrets.
- **Protocol deviation / breaking change**: Pass. No protocol deviation or public library API break is expected; the change is limited to the explorer sample UI contract.

### Post-Design Constitution Check

- **Protocol fidelity**: Still passes. Design keeps all matching/filtering local to browse-column presentation state.
- **Secure session handling**: Still passes. No new config, auth, or logging surfaces were introduced in design artifacts.
- **Async API design**: Still passes. Search/filter operates on already discovered resources and does not alter cancellation or streaming lifecycles.
- **Required tests**: Still passes. Design includes workflow, rendering, and index-preservation tests in the existing explorer test project.
- **Diagnostics**: Still passes. Design distinguishes empty columns, filtered no-match state, and selection-hidden state in explicit status feedback.
- **Protocol deviation / breaking change**: Still passes. No deviation or versioning impact identified.

## Project Structure

### Documentation (this feature)

```text
specs/008-search-column-filter/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── explorer-column-search.md
└── tasks.md
```

### Source Code (repository root)

```text
samples/
└── EtpExplorer/
    ├── ExplorerApp.cs
    ├── ExplorerModels.cs
    ├── IExplorerUi.cs
    ├── SelectionSetService.cs
    └── SpectreExplorerUi.cs

tests/
└── EtpExplorer.Tests/
    ├── ExplorerBrowseRenderingTests.cs
    ├── ExplorerBrowseWorkflowTests.cs
    ├── ExplorerSelectionWorkflowTests.cs
    └── TestSupport/
        └── FakeExplorerUi.cs

docs/
├── ETP_Specification_v1.1_Doc_v1.1.md
└── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md
```

**Structure Decision**: Implement the feature entirely inside the existing explorer sample and test project. Search/filter state belongs with browse-column/session models in `samples/EtpExplorer`, while coverage stays in `tests/EtpExplorer.Tests` using the existing `FakeExplorerUi` seam rather than adding a separate UI harness.

## Complexity Tracking

No constitution violations or exceptional complexity require formal justification.
