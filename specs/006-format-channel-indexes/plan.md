# Implementation Plan: Format Channel Index Output

**Branch**: `006-format-channel-indexes` | **Date**: 2026-04-10 | **Spec**: [/Users/LGEIR/src/etp_test/specs/006-format-channel-indexes/spec.md](/Users/LGEIR/src/etp_test/specs/006-format-channel-indexes/spec.md)
**Input**: Feature specification from `/specs/006-format-channel-indexes/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Improve the sample console so time-indexed channels print human-readable local-time timestamps and depth-indexed channels print correctly scaled depth values instead of raw long indexes. The implementation will preserve raw protocol index values in `ChannelDataItem`, extend `ChannelDefinition` to keep the primary-index metadata required for interpretation (`scale`, `timeDatum`, `depthDatum`, and related fields), add a reusable conversion helper for primary index interpretation, and route both live and range sample output through one shared formatting path.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, existing internal Avro reader/writer helpers, sample console infrastructure in `Microsoft.Extensions.Hosting`  
**Storage**: N/A  
**Testing**: xUnit with existing unit, integration, and sample test projects  
**Target Platform**: Developer and CI environments running .NET 10 on macOS/Linux/Windows; console sample runs in the local machine timezone and culture context
**Project Type**: Client library with sample console and test projects  
**Performance Goals**: Preserve existing receive-path behavior, add no meaningful overhead to protocol decoding beyond retaining primary-index metadata, and keep index formatting work at the sample-output boundary so per-sample rendering cost remains negligible relative to network and console I/O  
**Constraints**: Must remain protocol-faithful to ETP v1.1 index metadata semantics; preserve raw `ChannelDataItem.Indexes` values for callers; keep current async streaming and range APIs non-breaking; apply the same formatting rules to live and range output; render time indexes in local time only at the presentation boundary; remain secret-safe  
**Scale/Scope**: Additive change to Protocol 1 metadata decoding, public channel metadata, reusable index interpretation logic, sample output formatting, and automated coverage for time, depth, and fallback scenarios

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **PASS**: Relevant ETP v1.1 clauses are identified from `docs/ETP_Specification_v1.1_Doc_v1.1.md`: `IndexMetadataRecord` (§3.3.16.8) defines `indexType`, `uom`, `depthDatum`, `scale`, and `timeDatum`; `ChannelMetadataRecord` (§3.3.16.9) carries the `indexes` array exposed to consumers; `ChannelData` (§3.4.2.4) defines the raw `<index,value>` tuples that remain unchanged in `ChannelDataItem`.
- **PASS**: Basic authentication handling is unchanged; this feature only affects post-connect metadata interpretation and sample rendering, so no new credential flow, secret storage, or logging surface is introduced.
- **PASS**: Public API design remains asynchronous and cancellation-aware because existing describe, stream, stop, and range methods are preserved; any helper added for index interpretation is additive and does not alter stream lifetime semantics.
- **PASS**: Required tests are identified: unit coverage for binary/JSON metadata decoding and conversion rules, sample-output tests for live and range formatting behavior, and integration coverage for describe metadata propagation where the wire-mapped public model changes.
- **PASS**: Diagnostics remain secret-safe; interpretation failures are handled as local formatting/fallback behavior rather than new connection or protocol failure classes.
- **PASS**: No protocol deviation or breaking change is planned; the design preserves raw wire values and applies ETP-defined index semantics only when formatting output.

## Project Structure

### Documentation (this feature)

```text
specs/006-format-channel-indexes/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── etp-client-channel-index-formatting-api.md
├── checklists/
│   └── requirements.md
└── tasks.md
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

tests/
├── EtpClient.UnitTests/
├── EtpClient.IntegrationTests/
└── EtpClient.SampleConsole.Tests/

samples/
└── EtpClient.SampleConsole/

docs/
├── ETP_Specification_v1.1_Doc_v1.1.md
├── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md
└── etp-basic-auth-notes.md
```

**Structure Decision**: Keep the feature inside the existing library, sample, and test projects. Protocol metadata preservation and reusable index interpretation logic will live under `src/EtpClient/Models` and `src/EtpClient/Protocol`, sample rendering changes under `samples/EtpClient.SampleConsole`, and coverage split across `tests/EtpClient.UnitTests`, `tests/EtpClient.IntegrationTests`, and `tests/EtpClient.SampleConsole.Tests`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitution violations or exceptional complexity expected.

## Post-Design Constitution Check

- **PASS**: Protocol fidelity is preserved by retaining raw `ChannelData` index values and using the `IndexMetadataRecord` fields already defined by ETP to interpret them.
- **PASS**: Secure session handling is unchanged because no authentication, transport, or secret-bearing workflow is modified.
- **PASS**: Async streaming semantics remain intact; live streaming and range retrieval stay on the current API surface, with interpretation happening after data receipt.
- **PASS**: Test coverage is explicitly planned across codec decoding, metadata preservation, conversion rules, and sample rendering for live, range, and fallback scenarios.
- **PASS**: Diagnostics remain secret-safe and additive; unsupported or incomplete metadata leads to fallback formatting rather than ambiguous failures.
- **PASS**: No intentional protocol deviation or breaking API change is introduced; the chosen hybrid approach was selected over early specialization specifically to keep the public contract stable.
