# Implementation Plan: Channel Streaming Support

**Branch**: `005-add-channel-streaming` | **Date**: 2026-04-10 | **Spec**: [/Users/LGEIR/src/etp_test/specs/005-channel-streaming/spec.md](/Users/LGEIR/src/etp_test/specs/005-channel-streaming/spec.md)
**Input**: Feature specification from `/specs/005-channel-streaming/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add consumer-side ETP Protocol 1 ChannelStreaming support so callers can describe streamable channels, start and stop live streams, receive channel lifecycle events, and request bounded historical ranges. The implementation will extend the current session manager and codec infrastructure beyond handshake and discovery flows, add typed Protocol 1 request/response models for both binary and JSON encodings, expose a public async API for description and streaming workflows, and update the sample app to demonstrate end-to-end describe plus stream consumption behavior.

## Technical Context

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, existing internal Avro reader/writer helpers  
**Storage**: N/A  
**Testing**: xUnit, existing unit/integration/sample test projects, live integration tests gated by user secrets or environment variables  
**Target Platform**: Developer and CI environments running .NET 10 on macOS/Linux/Windows
**Project Type**: Client library with sample app and test projects  
**Performance Goals**: Live Protocol 1 handling should preserve incoming event order per channel, complete describe and range request aggregation without unbounded buffering, and add negligible client-side overhead relative to network latency for typical multi-channel sessions  
**Constraints**: Must remain secret-safe, async and cancellation-aware, preserve current Protocol 0 and Protocol 3 behavior, support both binary and JSON encodings through the existing codec seam, implement Protocol 1 multipart and correlation semantics per ETP v1.1, and avoid undocumented protocol shortcuts  
**Scale/Scope**: Protocol 1 consumer support for `Start` where required, `ChannelDescribe`, `ChannelStreamingStart`, `ChannelStreamingStop`, `ChannelRangeRequest`, `ChannelMetadata`, `ChannelData`, `ChannelDataChange`, `ChannelStatusChange`, and `ChannelRemove`, plus public typed models, sample workflow updates, and unit/integration/sample coverage

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **PASS**: Relevant ETP v1.1 clauses are identified from `docs/ETP_Specification_v1.1_Doc_v1.1.md`: Protocol 1 overview and requirements (§1.3.1.2, §2.2.1), consumer interface and methods (§2.2.2), producer interface message expectations (§2.2.3), session survivability and multipart behavior (§2.2.1), and message definitions for `ChannelDescribe`, `ChannelMetadata`, `ChannelData`, `ChannelStreamingStart`, `ChannelStreamingStop`, `ChannelRangeRequest`, `ChannelDataChange`, `ChannelRemove`, and `ChannelStatusChange` in §3.4.2. WITSML guidance in `docs/ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md` defines describable URI shapes and SimpleStreamer/full streaming flows for wells, wellbores, logs, and log curves.
- **PASS**: Basic authentication remains explicit and secret-safe because Protocol 1 traffic begins only after the existing authenticated session is established; no credential values are added to Protocol 1 payloads, logs, exceptions, or sample output.
- **PASS**: Public API design remains asynchronous and cancellation-aware by exposing async describe/stream/range operations and explicit stream/disposal lifetimes rather than blocking listeners or hidden threads.
- **PASS**: Required tests are identified: unit coverage for binary/JSON Protocol 1 codecs, request/response aggregation, lifecycle-event handling, and correlation semantics, plus integration/sample coverage for describe, live streaming, stop behavior, range retrieval, SimpleStreamer compatibility, and protocol failure behavior.
- **PASS**: Diagnostics will extend current structured logging/error reporting to include Protocol 1 request targets, stream lifecycle transitions, range correlation context, and producer-sent status/remove/change events without leaking credentials or authorization headers.
- **PASS**: No intentional protocol deviation is planned. The design follows consumer-side Protocol 1 semantics, including ordered data delivery expectations, multipart metadata/range aggregation, reconnect-sensitive incomplete range behavior, and SimpleStreamer compatibility where the spec requires it.

## Project Structure

### Documentation (this feature)

```text
specs/005-channel-streaming/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── etp-client-channel-streaming-api.md
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
├── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md
└── etp-basic-auth-notes.md
```

**Structure Decision**: Keep ChannelStreaming support inside the existing library and sample projects. Protocol 1 message models and encoding logic will live under `src/EtpClient/Protocol`, orchestration and receive-loop integration under `src/EtpClient/Connection`, public request/result/stream models under `src/EtpClient/Models`, diagnostics under `src/EtpClient/Diagnostics`, sample usage under `samples/EtpClient.SampleConsole`, and coverage split across the existing unit, integration, and sample test projects.

## Complexity Tracking

No constitution violations or exceptional complexity expected.
