# Contract: AddEtpInstrumentation Extension Methods

**Feature**: `011-otel-instrumentation`  
**Branch**: `011-otel-instrumentation`  
**Contract type**: Public library API — extension methods on OpenTelemetry provider builders

---

## Overview

`EtpClient` exposes instrumentation registration through two extension methods that follow the established `AddXxxInstrumentation()` pattern used by `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.AspNetCore`, etc. Each method is independently optional: an application may register only tracing, only metrics, or both.

---

## TracerProviderBuilder Extension

**Namespace**: `EtpClient.Instrumentation`  
**Assembly**: `EtpClient`

```csharp
namespace EtpClient.Instrumentation;

public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds ETP client tracing to the OpenTelemetry tracer provider.
    /// Registers the <c>"EtpClient"</c> activity source so that spans produced
    /// by <see cref="EtpClient"/> operations are included in the application's
    /// distributed trace.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The same <paramref name="builder"/> for method chaining.</returns>
    public static TracerProviderBuilder AddEtpInstrumentation(
        this TracerProviderBuilder builder);
}
```

### Behavior

| Condition | Result |
|-----------|--------|
| Called once | Registers `"EtpClient"` source via `builder.AddSource("EtpClient")` |
| Called more than once on the same builder | Idempotent — additional calls are no-ops (delegated to `AddSource` idempotency) |

### Postcondition

After `Build()` is called on the `TracerProviderBuilder`, any `Activity` started by `EtpInstrumentation.Source` (`ActivitySource` named `"EtpClient"`) will be captured by the tracer provider and forwarded to configured exporters.

---

## MeterProviderBuilder Extension

**Namespace**: `EtpClient.Instrumentation`  
**Assembly**: `EtpClient`

```csharp
namespace EtpClient.Instrumentation;

public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds ETP client metrics to the OpenTelemetry meter provider.
    /// Registers the <c>"EtpClient"</c> meter so that measurements produced
    /// by <see cref="EtpClient"/> operations are included in the application's
    /// metric pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
    /// <returns>The same <paramref name="builder"/> for method chaining.</returns>
    public static MeterProviderBuilder AddEtpInstrumentation(
        this MeterProviderBuilder builder);
}
```

### Behavior

| Condition | Result |
|-----------|--------|
| Called once | Registers `"EtpClient"` meter via `builder.AddMeter("EtpClient")` |
| Called more than once on the same builder | Idempotent — delegated to `AddMeter` idempotency |

### Postcondition

After `Build()` is called on the `MeterProviderBuilder`, all measurements recorded on instruments created from `EtpInstrumentation.Meter` (meter named `"EtpClient"`) will be collected and forwarded to configured exporters.

---

## Registered Source and Meter Names

These names are stable and MUST NOT change without a major version bump, as consumer dashboards, alert rules, and `AddSource`/`AddMeter` calls in application code may reference them directly.

| Asset | Name | Version |
|-------|------|---------|
| `ActivitySource` | `"EtpClient"` | Library assembly informational version |
| `Meter` | `"EtpClient"` | Library assembly informational version |

---

## Emitted Spans

| Span name | Triggered by |
|-----------|-------------|
| `etp.connect` | `EtpClient.ConnectAsync` |
| `etp.disconnect` | `EtpClient.CloseAsync` |
| `etp.discovery` | `EtpClient.DiscoverResourcesAsync` |
| `etp.channel.describe` | `EtpClient.DescribeChannelsAsync` |
| `etp.channel.range_request` | `EtpClient.RequestChannelRangeAsync` |

Full attribute schema per span is documented in [data-model.md](../data-model.md).

---

## Emitted Metrics

| Instrument name | Type | Unit |
|-----------------|------|------|
| `etp.client.active_connections` | UpDownCounter\<int\> | `{connection}` |
| `etp.client.operation.duration` | Histogram\<double\> | `s` |
| `etp.client.operation.errors` | Counter\<long\> | `{error}` |
| `etp.client.messages.sent` | Counter\<long\> | `{message}` |
| `etp.client.messages.received` | Counter\<long\> | `{message}` |

Full tag schema is documented in [data-model.md](../data-model.md).

---

## Usage Example

```csharp
// In application startup (e.g., Program.cs using Microsoft.Extensions.Hosting)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddEtpInstrumentation()          // <-- registers "EtpClient" activity source
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddEtpInstrumentation()          // <-- registers "EtpClient" meter
        .AddOtlpExporter());
```

Full working example with a connected session is in [quickstart.md](../quickstart.md).

---

## Stability Guarantees

| Element | Stability |
|---------|-----------|
| `AddEtpInstrumentation()` method signatures | Stable — no breaking changes without major version |
| Span names (`etp.*`) | Stable |
| Attribute key names (`etp.*`, `server.*`, `error.*`) | Stable |
| Metric instrument names | Stable |
| Activity source name `"EtpClient"` | Stable |
| Meter name `"EtpClient"` | Stable |
