# Implementation Plan: Sample Console Application

**Branch**: `002-sample-console-app` | **Date**: 2026-04-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/002-sample-console-app/spec.md`

## Summary

Add a runnable .NET 10 console sample that demonstrates the existing `EtpClient` connection flow end to end. The sample will load endpoint and credential inputs from .NET user secrets for local development, validate configuration before connecting, use the public `EtpClient` API only, print a clear success/failure summary, and shut down cleanly without exposing secrets.

## Technical Context

**Language/Version**: C# with .NET 10  
**Primary Dependencies**: `EtpClient` project reference, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console`  
**Storage**: .NET user secrets for local development inputs; no application data storage  
**Testing**: xUnit for sample-level unit tests and integration-style host/flow tests, existing repo `dotnet test` workflow  
**Target Platform**: Developer workstation running .NET 10 on macOS/Linux/Windows  
**Project Type**: Console application sample plus focused tests  
**Performance Goals**: Local startup should be near-immediate and add no meaningful overhead beyond existing endpoint connection latency  
**Constraints**: Must use secret-safe local input handling, must not bypass the library, must remain easy to run with `dotnet run`, must not require `DOTNET_ENVIRONMENT=Development` to load secrets  
**Scale/Scope**: One sample console app, one sample test project or test expansion, and related documentation/configuration wiring

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **PASS**: Relevant ETP behavior remains limited to the already implemented Protocol 0 session flow: WebSocket open, `RequestSession`, `OpenSession`, and `ProtocolException` handling grounded in the repo protocol source under `docs/`.
- **PASS**: Basic authentication remains explicit and secret-safe. The sample reads endpoint and credentials from .NET user secrets and never prints raw secret values.
- **PASS**: Public API usage remains asynchronous and cancellation-aware by consuming `EtpClient.ConnectAsync`, `CloseAsync`, and `IAsyncDisposable` only.
- **PASS**: Required tests are identified for sample configuration validation, result presentation, and clean shutdown; existing protocol wire coverage remains in the library test projects.
- **PASS**: Diagnostics will rely on structured console/log output that reports outcome categories without leaking secrets.
- **PASS**: No protocol deviation or breaking change is introduced; the feature is additive and sample-only.

## Project Structure

### Documentation (this feature)

```text
specs/002-sample-console-app/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
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

samples/
└── EtpClient.SampleConsole/
    ├── Program.cs
    ├── SampleConsoleOptions.cs
    ├── appsettings.json
    └── EtpClient.SampleConsole.csproj

tests/
├── EtpClient.UnitTests/
├── EtpClient.IntegrationTests/
└── EtpClient.SampleConsole.Tests/

docs/
└── ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.pdf
```

**Structure Decision**: Keep the sample outside `src/EtpClient` in a dedicated `samples/EtpClient.SampleConsole` project so the library remains production-focused and the example remains easy to discover and run. Add a dedicated sample test project only if the existing test projects would otherwise blur sample-specific concerns.

## Complexity Tracking

No constitution violations or exceptional complexity expected.
