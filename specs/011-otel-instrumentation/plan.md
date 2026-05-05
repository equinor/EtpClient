# Implementation Plan: ETP OpenTelemetry Instrumentation

**Branch**: `011-otel-instrumentation` | **Date**: 2026-05-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/011-otel-instrumentation/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

**Path Rule**: Use repository-relative paths only. Never include machine-local absolute filesystem paths in generated artifacts.

## Summary

Add opt-in OpenTelemetry distributed tracing and metrics to the `EtpClient` library. The library will own a named `ActivitySource` and `Meter` (both keyed `"EtpClient"`) using only BCL `System.Diagnostics` types, ensuring zero overhead for applications that do not register instrumentation. Two extension methods — `AddEtpInstrumentation()` on `TracerProviderBuilder` and `AddEtpInstrumentation()` on `MeterProviderBuilder` — follow the `AddAspNetCoreInstrumentation()` convention and are the sole integration points. Spans cover connection establishment/close and each protocol operation (discovery, channel describe, channel range request). Metric instruments cover active connections (UpDownCounter), operation duration (Histogram), operation errors (Counter), and messages sent/received (Counter). No ETP protocol behavior changes; the feature is purely additive.

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: `OpenTelemetry` SDK (adds `TracerProviderBuilder` / `MeterProviderBuilder` extension point); `System.Diagnostics.ActivitySource` + `System.Diagnostics.Metrics.Meter` from BCL (no OTEL SDK reference needed for core instrumentation points)  
**Storage**: N/A  
**Testing**: xUnit + `OpenTelemetry.Testing.InMemory` exporter (or equivalent in-memory collector) for span/metric assertions in `EtpClient.UnitTests` and `EtpClient.IntegrationTests`  
**Target Platform**: .NET 10 library (cross-platform)  
**Project Type**: Library (NuGet package `EtpClient`)  
**Performance Goals**: Zero measurable overhead when OTEL is not configured (relies on `ActivitySource`/`Meter` BCL no-op path); span creation must not block the calling thread  
**Constraints**: No credentials in span attributes or metric tags; no breaking changes to any existing public API; `EtpClient.csproj` may add `OpenTelemetry` as a direct dependency for the extension methods (making it a transitive dep for consumers, which is acceptable and expected)  
**Scale/Scope**: Additive change to one library project; new `Instrumentation/` folder inside `src/EtpClient/`; test additions in existing test projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **ETP v1.1 clauses**: This feature adds observability wrappers around existing operations; it introduces no new ETP messages or protocol behavior. No ETP specification clauses are relevant. ✅ PASS
- **Secure credential handling**: Span attributes will use only the sanitized endpoint host (`server.address`), never the full URI with credentials, password fields, or auth tokens. Verified as an explicit requirement (FR-011). ✅ PASS
- **Async / cancellation-aware API**: `Activity` objects work naturally with async/await and `IAsyncDisposable`. Spans are completed in `finally` blocks. Cancellation surfaces as a cancelled span status, not a thrown exception from instrumentation code. ✅ PASS
- **Required tests**: Unit tests for span/metric production, attribute presence, and no-op behavior when OTEL is not registered; integration tests that verify end-to-end span capture through the real operation paths. ✅ PASS
- **Diagnostics without secrets**: Existing `EtpClientLog` structured log events (1001–1020) are unaffected. New span attributes follow the `etp.*` prefix convention and are defined in `research.md`. ✅ PASS
- **Protocol deviation / breaking change**: None. Purely additive. ✅ PASS

**Post-Phase-1 re-check**: See bottom of this document.

## Project Structure

### Documentation (this feature)

```text
specs/011-otel-instrumentation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── AddEtpInstrumentation.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
└── EtpClient/
    ├── Instrumentation/               # NEW — all OTEL integration code
    │   ├── EtpInstrumentation.cs      # Static ActivitySource + Meter singletons; internal span/metric helpers

    │   ├── TracerProviderBuilderExtensions.cs  # AddEtpInstrumentation() on TracerProviderBuilder
    │   └── MeterProviderBuilderExtensions.cs   # AddEtpInstrumentation() on MeterProviderBuilder
    ├── Connection/
    │   └── EtpSessionManager.cs       # MODIFIED — add span + metric call sites
    ├── Diagnostics/
    │   └── EtpClientLog.cs            # UNCHANGED
    ├── EtpClient.cs                   # UNCHANGED (operation dispatch unchanged)
    └── EtpClient.csproj               # MODIFIED — add OpenTelemetry package reference

tests/
├── EtpClient.UnitTests/
│   └── Instrumentation/               # NEW — span attribute, metric, and no-op tests
└── EtpClient.IntegrationTests/
    └── Instrumentation/               # NEW — end-to-end span capture via TestHost
```

**Structure Decision**: All new instrumentation code lives in `src/EtpClient/Instrumentation/`. This avoids a separate NuGet package (premature for a 0.x library) while keeping the instrumentation concern isolated. The extension methods reference `OpenTelemetry` SDK types, which becomes a direct dependency of the library — the same approach used by `OpenTelemetry.Instrumentation.Http` for BCL types. The BCL `ActivitySource` and `Meter` objects are created unconditionally as static singletons; they are zero-overhead when no listener is attached, as guaranteed by the .NET runtime.

## Post-Phase-1 Constitution Re-check

After design is complete:

- **Protocol Fidelity**: No protocol changes. ✅
- **Secure credential handling**: `server.address` uses `options.Endpoint.Host` only (never `options.Endpoint.UserInfo` or password). Verified in contracts. ✅
- **Async / cancellation**: Spans opened with `using var activity = EtpInstrumentation.StartXxxActivity(...)` pattern; `Activity.SetStatus(ActivityStatusCode.Error)` called in catch/finally. ✅
- **Tests**: Unit tests assert span names, attribute keys, and metric instrument names via in-memory exporter; no-op test asserts zero activities when OTEL is not configured. ✅
- **Diagnostics**: New span attributes defined and documented in `research.md` under `etp.*` prefix. ✅
- **Breaking changes**: None. ✅

## Complexity Tracking

> No Constitution Check violations. No complexity tracking entries required.
