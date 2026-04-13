# Implementation Plan: Support Avro Encoding

**Branch**: `003-support-avro-encoding` | **Date**: 2026-04-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/003-support-avro-encoding/spec.md`

## Summary

Add configurable ETP message encoding to the client so callers can choose binary or JSON through the public connection options while preserving binary as the default. The implementation will introduce an explicit encoding selection model, separate message codec responsibilities from transport responsibilities, extend the WebSocket abstraction to support the selected frame mode, and add unit plus integration coverage for successful and failing binary/JSON session establishment flows.

## Technical Context

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`  
**Storage**: N/A  
**Testing**: xUnit, existing unit/integration test projects, live integration tests gated by user secrets or environment variables  
**Target Platform**: Developer and CI environments running .NET 10 on macOS/Linux/Windows  
**Project Type**: Client library with sample app and test projects  
**Performance Goals**: Preserve existing binary-mode connection behavior and keep encoding overhead negligible relative to network/session latency for Protocol 0 handshake flows  
**Constraints**: Backward-compatible default behavior, secret-safe diagnostics, asynchronous and cancellation-aware public APIs, no protocol deviations without explicit documentation, selected encoding must be applied consistently for the full session  
**Scale/Scope**: One public option addition, one codec abstraction layer, transport updates for binary/text frame handling, Protocol 0 handshake support for both encodings, and corresponding sample/test/documentation updates

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **PASS**: Relevant ETP v1.1 references are identified for planning: the local spec copy states that ETP supports both binary and JSON encoding for transport, and the current client scope remains grounded in Protocol 0 `RequestSession`, `OpenSession`, `ProtocolException`, and the ETP message header semantics already implemented in the library.
- **PASS**: Basic authentication remains explicit and secret-safe. Encoding selection is orthogonal to credentials and will not change how secrets enter the client or appear in logs/errors.
- **PASS**: Public API design remains asynchronous and cancellation-aware because encoding selection will extend `EtpConnectionOptions` rather than introduce a separate synchronous connection path.
- **PASS**: Required tests are identified: unit coverage for option validation and codec selection, plus integration coverage for binary/text frame handling and successful/failing session establishment in both modes.
- **PASS**: Diagnostics will be extended to expose selected encoding and encoding-related failures without logging credentials or raw authorization values.
- **PASS**: No intentional protocol deviation is planned. Backward compatibility is preserved by keeping binary as the default when callers do not opt into JSON.

## Project Structure

### Documentation (this feature)

```text
specs/003-support-avro-encoding/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── etp-client-encoding-api.md
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
├── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md
└── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.pdf
```

**Structure Decision**: Keep the implementation inside the existing library projects rather than adding a new project. The public option surface lives under `src/EtpClient/Models`, session/transport updates stay under `src/EtpClient/Connection`, codec/message changes live under `src/EtpClient/Protocol`, and coverage is split between unit tests, integration tests, and the existing sample app for reference usage.

## Complexity Tracking

No constitution violations or exceptional complexity expected.

