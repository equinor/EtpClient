# Implementation Plan: ETP Discovery Traversal

**Branch**: `004-add-etp-discovery` | **Date**: 2026-04-10 | **Spec**: [/Users/LGEIR/src/etp_test/specs/004-etp-discovery/spec.md](/Users/LGEIR/src/etp_test/specs/004-etp-discovery/spec.md)
**Input**: Feature specification from `/specs/004-etp-discovery/spec.md`

## Summary

Add read-only ETP Discovery support so the client can request `GetResources` on `eml://` and deeper URIs, aggregate `GetResourcesResponse` messages into one logical traversal result, and expose resource metadata that helps callers identify streamable targets. The implementation will extend the current session/codec plumbing beyond Protocol 0, model Discovery request and response messages for both supported transport encodings, add a public async API for URI traversal, and update the sample app to resolve and print the server's top-level URIs.

## Technical Context

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, existing internal Avro reader/writer helpers  
**Storage**: N/A  
**Testing**: xUnit, existing unit/integration test projects, live integration tests gated by user secrets or environment variables  
**Target Platform**: Developer and CI environments running .NET 10 on macOS/Linux/Windows  
**Project Type**: Client library with sample app and test projects  
**Performance Goals**: Discovery should complete in one request/response flow per traversed URI, aggregate multipart responses without unbounded buffering, and remain negligible relative to network latency for root and single-level traversal  
**Constraints**: Must remain secret-safe, async and cancellation-aware, preserve current session-establishment behavior, support both binary and JSON encodings through the existing codec seam, and treat Discovery multipart/Acknowledge semantics per ETP v1.1 without undocumented protocol shortcuts  
**Scale/Scope**: Protocol 3 `GetResources` / `GetResourcesResponse` / `Acknowledge` / `ProtocolException` handling, one public discovery traversal API, sample-app top-level URI listing, and unit/integration/live coverage for root traversal, child traversal, empty results, and discovery failure behavior

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **PASS**: Relevant ETP v1.1 clauses are identified from `docs/ETP_Specification_v1.1_Doc_v1.1.md`: Discovery protocol overview and roles (§2.3 / §3.4.4), `GetResources` (Protocol 3, messageType 1), `GetResourcesResponse` (Protocol 3, messageType 2), `Acknowledge` for valid URIs with no children, root `eml://` traversal behavior, pagination/limit behavior, and the `Resource` record semantics (`uri`, `contentType`, `channelSubscribable`, `resourceType`, `hasChildren`, `uuid`, `objectNotifiable`).
- **PASS**: Basic authentication remains explicit and secret-safe because discovery starts only after the existing authenticated session is established; no credential values are added to discovery payloads, logs, or sample output.
- **PASS**: Public API design will remain asynchronous and cancellation-aware by adding an async traversal method to the library and connector layer rather than introducing blocking enumeration APIs.
- **PASS**: Required tests are identified: unit coverage for binary/JSON discovery message codecs and aggregation logic, plus integration coverage for root traversal, child traversal, empty-child acknowledgements, multipart results, and protocol-limit failure behavior.
- **PASS**: Diagnostics will extend current structured logging/error reporting to include discovery-request URIs, empty results, and protocol-limit failures without leaking credentials or raw authorization headers.
- **PASS**: No intentional protocol deviation is planned. The design follows Protocol 3 request/response semantics, including `Acknowledge` for no-children results and `ProtocolException` handling for invalid or denied traversal.

## Project Structure

### Documentation (this feature)

```text
specs/004-etp-discovery/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── etp-client-discovery-api.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
└── EtpClient/
    ├── Connection/
    ├── Diagnostics/
    ├── Models/
    └── Protocol/

tests/
├── EtpClient.UnitTests/
├── EtpClient.IntegrationTests/
└── EtpClient.SampleConsole.Tests/

samples/
└── EtpClient.SampleConsole/

docs/
├── ETP_Specification_v1.1_Doc_v1.1.md
├── ETP_Specification_v1.1_Doc_v1.1.pdf
├── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md
└── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.pdf
```

**Structure Decision**: Keep Discovery support inside the existing library and sample projects. Protocol 3 message models/codecs will live under `src/EtpClient/Protocol`, request/response orchestration under `src/EtpClient/Connection`, public traversal models under `src/EtpClient/Models`, sample invocation/output under `samples/EtpClient.SampleConsole`, and coverage split across the existing unit, integration, and sample test projects.

## Complexity Tracking

No constitution violations or exceptional complexity expected.
