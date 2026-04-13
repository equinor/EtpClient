# Implementation Plan: ETP Basic Auth Connection

**Branch**: `001-etp-basic-auth` | **Date**: 2026-04-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/001-etp-basic-auth/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Build a new `.NET 10 / C#` class library that can open a Basic-authenticated WebSocket connection to an ETP endpoint, send the minimal Protocol 0 `RequestSession` message, transition to connected only after `OpenSession` is received, and expose deterministic failure and shutdown behavior. The first implementation uses `System.Net.WebSockets.ClientWebSocket` with an explicit `Authorization` request header, models the session lifecycle as a small async state machine, and includes unit plus integration coverage for success, rejection, and cancellation flows.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`  
**Storage**: N/A  
**Testing**: xUnit v3 with `dotnet test`; unit tests plus integration tests against a controllable test endpoint  
**Target Platform**: Cross-platform .NET library for macOS, Linux, and Windows
**Project Type**: class library  
**Performance Goals**: Control-plane only for this feature; connection setup must be asynchronous, cancellation-aware, and avoid unnecessary buffering before the session is established  
**Constraints**: No credential leakage; connection state transitions must be deterministic; `ClientWebSocket` send/receive access must be serialized per platform guidance; Protocol 1 streaming is out of scope for this slice  
**Scale/Scope**: MVP scope is one authenticated ETP session per client instance, with enough design headroom to add protocol handlers and subscriptions later

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- Relevant ETP v1.1 clauses and message types are identified from the protocol PDF in docs/.
- Basic authentication handling is explicit, secret-safe, and transport assumptions are documented.
- Public API design is asynchronous, cancellation-aware, and defines subscription and disposal semantics.
- Required tests are identified: unit coverage plus contract or integration coverage for wire behavior.
- Diagnostics cover connection lifecycle, protocol errors, and subscription state without leaking secrets.
- Any protocol deviation or breaking change is documented with versioning impact and rationale.

**Gate Result**: PASS

- Relevant protocol references identified for this feature: WITSML ETP implementation specification section `ETP Protocols in Scope` and the documented connection sequences showing WebSocket open followed by `RequestSession` and `OpenSession` in the local protocol reference.
- Scope boundary documented: this slice implements Protocol 0 connection establishment only and intentionally defers Protocol 1 `Start` and subscription flows to later features.
- No constitution violations require justification.

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
EtpClient.sln

src/
└── EtpClient/
    ├── Connection/
    ├── Protocol/
    ├── Diagnostics/
    └── Models/

tests/
├── EtpClient.UnitTests/
└── EtpClient.IntegrationTests/

docs/
└── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.pdf
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

Use a single solution with one production library and two test projects. The repository does not yet contain source code, so this feature will establish `EtpClient.sln`, the production project under `src/EtpClient`, and unit/integration coverage under `tests/`. A separate contract test project is unnecessary for this first slice because the only external interface is the public library API and wire behavior can be covered by an integration harness that validates the opening handshake and Protocol 0 session exchange.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
