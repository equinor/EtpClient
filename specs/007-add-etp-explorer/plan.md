# Implementation Plan: Add ETP Explorer

**Branch**: `007-add-etp-explorer` | **Date**: 2026-04-12 | **Spec**: [/Users/LGEIR/src/etp_test/specs/007-add-etp-explorer/spec.md](/Users/LGEIR/src/etp_test/specs/007-add-etp-explorer/spec.md)
**Input**: Feature specification from `/specs/007-add-etp-explorer/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Create a second console application, `EtpExplorer`, that uses Spectre.Console to provide a polished interactive workflow for connecting to an ETP server, prompting the user to choose one of the discovered root nodes such as `witsml14` or `witsml20`, navigating the selected branch as a tree, selecting one or more streamable endpoints, and rendering live streamed output. The implementation will keep protocol behavior in the existing `EtpClient` library, use explicit .NET user secrets for server URL and credentials, build a root-selection and tree-navigation explorer flow on top of the existing discovery and channel-streaming APIs, and add dedicated application tests for root selection, browsing, selection, streaming, and shutdown behavior.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `EtpClient` project reference, `Spectre.Console`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console`; test-time use of xUnit and NSubstitute, with optional `Spectre.Console.Testing` only if the chosen UI seams need it  
**Storage**: N/A  
**Testing**: xUnit + NSubstitute in a dedicated explorer test project; existing library unit/integration coverage remains authoritative for protocol wire behavior  
**Target Platform**: Developer and CI environments running .NET 10 on macOS/Linux/Windows with interactive terminal support
**Project Type**: Client library plus multiple console sample applications and test projects  
**Performance Goals**: Root-node selection and tree-navigation actions should feel immediate, and live rendering should keep pace with typical developer validation streams without blocking cancellation or hiding endpoint attribution  
**Constraints**: Must remain protocol-faithful to ETP v1.1 Discovery and ChannelStreaming semantics; server URL and credentials must come from .NET user secrets; no secret values in output, logs, exceptions, or tests; keep streaming cancellation and shutdown explicit; avoid forcing UI concerns into the core library  
**Scale/Scope**: Add one new console sample app, one dedicated test project, and sample-local orchestration/presentation code that reuses the existing library APIs for connection, discovery, describe, and streaming

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **PASS**: Relevant ETP v1.1 clauses and message types are identified from `docs/ETP_Specification_v1.1_Doc_v1.1.md`: ChannelStreaming overview (§1.3.1.2) defines the describe-first flow and channel/session semantics; the ChannelStreaming consumer interface lists `ChannelDescribe`, `ChannelStreamingStart`, `ChannelStreamingStop`, `ChannelRangeRequest`, `ChannelMetadata`, `ChannelData`, `ChannelRemove`, and `ChannelDataChange`; Discovery interfaces and sequence (§2.3.1.1, §2.3.2.1, §2.3.4) define `GetResources`, `GetResourcesResponse`, and `Acknowledge` behavior for empty discovery results; message header semantics describe `correlationId` and multipart flags used by range and discovery responses.
- **PASS**: Basic authentication handling stays explicit and secret-safe by following the existing sample pattern of binding options from configuration and loading URL/credentials from .NET user secrets instead of command-line arguments or checked-in files.
- **PASS**: Public API usage remains asynchronous and cancellation-aware because the explorer will orchestrate the existing `EtpClient` async methods instead of introducing blocking wrappers or hidden background threads.
- **PASS**: Required tests are identified: explorer app tests for startup validation, root-node selection, tree navigation, selection state, stream start/stop, and rendered output attribution; no new wire-level integration suite is required unless the implementation exposes new library behavior.
- **PASS**: Diagnostics remain secret-safe by surfacing connection, discovery, describe, and streaming failures as user-understandable status output without echoing credentials.
- **PASS**: No protocol deviation or breaking library API change is planned; the explorer is an additive sample application built on the current library surface.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
src/
└── EtpClient/
    ├── Connection/
    ├── Diagnostics/
    ├── Models/
    └── Protocol/

samples/
├── EtpClient.SampleConsole/
└── EtpExplorer/

tests/
├── EtpClient.UnitTests/
├── EtpClient.IntegrationTests/
├── EtpClient.SampleConsole.Tests/
└── EtpExplorer.Tests/

docs/
├── ETP_Specification_v1.1_Doc_v1.1.md
├── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md
└── etp-basic-auth-notes.md
```

**Structure Decision**: Keep protocol logic in `src/EtpClient` and add the new explorer as a separate sample app under `samples/EtpExplorer` with its own user-secrets configuration, Spectre.Console presentation layer, and dedicated tests under `tests/EtpExplorer.Tests`. No new shared utility project is planned for the first slice; if later samples need the same orchestration seams, extraction can happen after the explorer flow is proven.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitution violations or exceptional complexity expected.

## Post-Design Constitution Check

- **PASS**: The design stays within the documented Discovery and ChannelStreaming clauses by reusing `GetResources`, `ChannelDescribe`, and `ChannelStreamingStart/Stop` semantics already implemented in the library.
- **PASS**: Secret handling remains explicit and deterministic because the explorer will load server URL and credentials from .NET user secrets during startup and validate configuration before connecting.
- **PASS**: Async streaming semantics remain intact; live rendering consumes the existing `IAsyncEnumerable<ChannelEvent>` stream and uses cancellation plus best-effort `StopChannelStreamingAsync` on exit.
- **PASS**: Test coverage is explicitly planned around user-visible explorer behavior, including root-node selection and tree navigation, without duplicating the existing library wire-protocol suite.
- **PASS**: Diagnostics remain actionable and secret-safe through structured logging plus Spectre.Console status/error rendering that identifies the failing workflow stage.
- **PASS**: No protocol deviation or breaking library change is introduced; the plan is additive and sample-scoped.
