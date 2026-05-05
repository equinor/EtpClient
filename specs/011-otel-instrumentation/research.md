# Research: ETP OpenTelemetry Instrumentation

**Feature**: `011-otel-instrumentation`  
**Branch**: `011-otel-instrumentation`

## Decision 1: Extension method placement — main library vs. separate package

**Decision**: Ship `AddEtpInstrumentation()` in the existing `EtpClient` library project (`src/EtpClient/`). No separate NuGet package.

**Rationale**: A separate `EtpClient.OpenTelemetry` package is the long-term ecosystem pattern (mirroring `OpenTelemetry.Instrumentation.Http`) but is premature at 0.x. The project has a single library project; splitting now adds build complexity with no user benefit yet. Revisit when the library approaches 1.0 and the package surface stabilizes.

**Alternatives considered**:
- `EtpClient.OpenTelemetry` separate project — deferred, too early
- Conditional compilation (`#if`) to avoid SDK dep — rejected as fragile and harder to test

---

## Decision 2: OpenTelemetry .NET SDK package and version

**Decision**: Add `OpenTelemetry` (core SDK, not `OpenTelemetry.Extensions.Hosting`) to `EtpClient.csproj`. Pin the version centrally in `Directory.Packages.props`.

**Package**: `OpenTelemetry` — provides `TracerProviderBuilder`, `MeterProviderBuilder`, `AddSource()`, `AddMeter()`.  
**Version**: Use the latest stable `1.x` release in `Directory.Packages.props`. As of 2026-05-05, `OpenTelemetry` 1.x is the stable channel. Pin to `1.x` with a specific version (e.g., `1.11.2`) and upgrade as part of routine dependency maintenance.

**What this package provides**:
- `OpenTelemetry.Trace.TracerProviderBuilder` (extension target for `AddEtpInstrumentation()`)
- `OpenTelemetry.Metrics.MeterProviderBuilder` (extension target for `AddEtpInstrumentation()`)
- `builder.AddSource(string name)` — registers the named `ActivitySource`
- `builder.AddMeter(string name)` — registers the named `Meter`

**What the library does NOT need from the SDK**: The `ActivitySource` and `Meter` objects used inside `EtpSessionManager` are from `System.Diagnostics` (BCL, no NuGet reference). Only the extension methods need the SDK types.

**Test packages**: Add `OpenTelemetry.Exporter.InMemory` to `EtpClient.UnitTests.csproj` and `EtpClient.IntegrationTests.csproj` for span and metric assertion via `InMemoryExporter<Activity>` and `InMemoryExporter<Metric>`.

**Alternatives considered**:
- `OpenTelemetry.Extensions.Hosting` — provides convenience on top of `OpenTelemetry`; unnecessary for a library that doesn't interact with `IServiceCollection` directly. Rejected.
- Using only BCL types and no extension methods — forces consumers to manually call `AddSource("EtpClient")`. Rejected per FR-001.

---

## Decision 3: ActivitySource and Meter names and versions

**Decision**:
- `ActivitySource` name: `"EtpClient"`
- `Meter` name: `"EtpClient"`
- Both versioned with the library's assembly version (`AssemblyInformationalVersion`), obtained via `typeof(EtpClient).Assembly.GetName().Version?.ToString() ?? "0.0.0"` at static initialization time.

**Rationale**: Following OpenTelemetry .NET naming conventions, the source/meter name should match the NuGet package ID (`EtpClient`). Using the assembly version enables per-version telemetry filtering in advanced observability backends.

**Alternatives considered**:
- `"Equinor.EtpClient"` — more globally unique but inconsistent with the existing `PackageId = "EtpClient"` and logging category names. Rejected.
- Hardcoded version string — fragile, diverges from the `VersionPrefix` in `Directory.Build.props`. Rejected.

---

## Decision 4: Zero-overhead opt-out mechanism

**Decision**: Rely on the .NET BCL's built-in no-op path for `ActivitySource` and `Meter`.

**Mechanism**:
- `ActivitySource.StartActivity()` returns `null` when no listener has subscribed to the named source. All call sites check for `null` before setting attributes.
- `System.Diagnostics.Metrics` instruments (Counter, Histogram, UpDownCounter) record measurements only when a `MeterListener` is attached. Calls to `.Add()`, `.Record()` etc. are no-ops otherwise.
- The static `EtpInstrumentation` class creates the `ActivitySource` and `Meter` at class initialization time (once per process). This is the standard pattern for all .NET OTEL instrumentation libraries.

**Result**: An application that does not call `AddEtpInstrumentation()` on either provider builder will never have a listener attached to `"EtpClient"`, so all `StartActivity()` calls return `null` and all metric instrument calls are no-ops.

---

## Decision 5: Span names and attribute schema

**Decision**: Use lowercase dotted span names with an `etp.` prefix for ETP-specific attributes alongside applicable OpenTelemetry semantic convention attributes.

### Span names

| Operation | Span name |
|-----------|-----------|
| Connect | `etp.connect` |
| Disconnect | `etp.disconnect` |
| Discovery | `etp.discovery` |
| ChannelDescribe | `etp.channel.describe` |
| ChannelRangeRequest | `etp.channel.range_request` |

### Standard attributes (OTel semantic conventions)

| Attribute key | Type | Set on | Notes |
|---|---|---|---|
| `server.address` | string | all spans | `options.Endpoint.Host` — never `UserInfo` |
| `server.port` | int | all spans | `options.Endpoint.Port` |
| `error.type` | string | error spans | Exception type name or `"etp_error"` |

### ETP-specific attributes (`etp.*` prefix)

| Attribute key | Type | Set on | Notes |
|---|---|---|---|
| `etp.encoding` | string | `etp.connect` | `"binary"` or `"json"` |
| `etp.uri` | string | `etp.discovery` | Requested discovery URI (truncated to 512 chars) |
| `etp.resource_count` | int | `etp.discovery` | Resources returned |
| `etp.channel_target` | string | `etp.channel.describe` | First URI in the describe request |
| `etp.channel_count` | int | `etp.channel.describe` | Channels returned |
| `etp.error_code` | int | error spans | ETP protocol error code (when available) |

**Rationale**: The `etp.*` prefix is an unregistered custom namespace following OTel guidance for vendor/domain-specific attributes. Truncating `etp.uri` to 512 characters guards against attribute value size limits in OTLP exporters.

---

## Decision 6: Metric instrument schema

**Decision**: Five instruments registered under meter `"EtpClient"`.

| Instrument name | Type | Unit | Tags | Notes |
|---|---|---|---|---|
| `etp.client.active_connections` | UpDownCounter\<int\> | `{connection}` | `server.address` | +1 on connect success, −1 on close |
| `etp.client.operation.duration` | Histogram\<double\> | `s` | `etp.operation`, `server.address`, `error.type` (on error) | Full round-trip per operation |
| `etp.client.operation.errors` | Counter\<long\> | `{error}` | `etp.operation`, `etp.error_code`, `server.address` | Incremented on any thrown exception |
| `etp.client.messages.sent` | Counter\<long\> | `{message}` | `server.address` | Per WebSocket send |
| `etp.client.messages.received` | Counter\<long\> | `{message}` | `server.address` | Per WebSocket receive |

**`etp.operation` tag values**: `connect`, `disconnect`, `discover`, `channel.describe`, `channel.range_request`, `channel.stream`.

**Rationale**: Instrument names follow the OTel metric naming conventions (`component.noun.verb` or `component.noun`). Units use the UCUM unit notation for durations (`s`) and the event-count notation (`{connection}`, `{message}`, `{error}`) recommended by OTel semantic conventions.

---

## Decision 7: Where to place instrumentation call sites in existing code

**Decision**: Instrumentation call sites live in `EtpSessionManager` (the internal class that owns the actual WebSocket operations), accessed via a static `EtpInstrumentation` helper class.

**Rationale**: `EtpClient.cs` is a thin delegation wrapper; `EtpSessionManager.cs` contains the actual operation logic and is the most natural place to open/close spans and record measurements. Adding call sites to `EtpSessionManager` avoids touching the public API.

**Pattern**:
```csharp
// Inside EtpSessionManager operation methods
using var activity = EtpInstrumentation.StartConnectActivity(host, port);
try
{
    // ... existing logic ...
    activity?.SetTag("etp.encoding", encoding);
    EtpInstrumentation.ActiveConnections.Add(1, new TagList { { "server.address", host } });
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    EtpInstrumentation.RecordOperationError("connect", host, etpErrorCode);
    throw;
}
```

---

## Decision 8: *(removed — EtpInstrumentationOptions not added)*

An empty `EtpInstrumentationOptions` class was initially considered for future extensibility, but decided against: the extension methods carry no options parameter. If configuration needs arise in the future, an options parameter can be added as a new overload without breaking existing callers.

---

## Resolved Unknowns Summary

| Unknown | Resolution |
|---------|------------|
| Extension method host type | `TracerProviderBuilder` + `MeterProviderBuilder` (not `OpenTelemetryBuilder`) |
| NuGet package for extension methods | `OpenTelemetry` (core SDK) in `EtpClient.csproj` |
| Source/meter name | `"EtpClient"` |
| Zero-overhead mechanism | BCL `ActivitySource`/`Meter` no-op path — no OTEL listener = no cost |
| Span naming | `etp.{operation}` lowercase dotted names |
| ETP-specific attribute prefix | `etp.*` custom namespace |
| Metric instruments | 5 instruments (see Decision 6) |
| Call site location | `EtpSessionManager` (internal), via static `EtpInstrumentation` helper |
| Options extensibility | No options class; add as a new overload if needs arise in the future |
| Test assertion packages | `OpenTelemetry.Exporter.InMemory` |
