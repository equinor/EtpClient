# Quickstart: ETP OpenTelemetry Instrumentation

**Feature**: `011-otel-instrumentation`  
**Branch**: `011-otel-instrumentation`

## Prerequisites

- .NET 10 application using `Microsoft.Extensions.Hosting`
- `EtpClient` NuGet package referenced
- OpenTelemetry SDK packages:
  - `OpenTelemetry.Extensions.Hosting` — provides `AddOpenTelemetry()` on `IServiceCollection`
  - An exporter package such as `OpenTelemetry.Exporter.Console` (for local development) or `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP, for production)

> **Note**: The `EtpClient` package already depends on `OpenTelemetry` (core SDK) for the extension methods. You only need to add `OpenTelemetry.Extensions.Hosting` and an exporter to your application project.

---

## Step 1 — Add packages to your application project

```xml
<!-- YourApp.csproj -->
<ItemGroup>
  <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.x.x" />
  <PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.x.x" />
</ItemGroup>
```

---

## Step 2 — Register ETP instrumentation in startup

```csharp
// Program.cs
using EtpClient.Instrumentation;
using OpenTelemetry.Resources;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyEtpApp"))
    .WithTracing(tracing => tracing
        .AddEtpInstrumentation()      // registers "EtpClient" activity source
        .AddConsoleExporter())        // replace with AddOtlpExporter() for production
    .WithMetrics(metrics => metrics
        .AddEtpInstrumentation()      // registers "EtpClient" meter
        .AddConsoleExporter());
```

Registering only one of the two is supported: `.WithTracing(t => t.AddEtpInstrumentation())` enables traces only; `.WithMetrics(m => m.AddEtpInstrumentation())` enables metrics only.

---

## Step 3 — Use EtpClient normally

No changes to your ETP client code are needed. Instrumentation is emitted automatically:

```csharp
// All operations automatically produce spans and metrics
await using var client = new EtpClient(logger);
var result = await client.ConnectAsync(options, ct);
// → span "etp.connect" recorded
// → etp.client.active_connections +1

var resources = await client.DiscoverResourcesAsync("eml://", ct);
// → span "etp.discovery" recorded (etp.uri, etp.resource_count attributes)

var channels = await client.DescribeChannelsAsync(uris, ct);
// → span "etp.channel.describe" recorded

await client.CloseAsync(ct);
// → span "etp.disconnect" recorded
// → etp.client.active_connections -1
```

---

## Step 4 — Verify output (console exporter)

When using `AddConsoleExporter()`, you will see output similar to:

```
Activity.DisplayName: etp.connect
Activity.Duration:    00:00:00.2140000
Activity.Tags:
    server.address: my-etp-server.example.com
    server.port: 443
    etp.encoding: binary
Activity.Status: Unset

Activity.DisplayName: etp.discovery
Activity.Duration:    00:00:00.0830000
Activity.Tags:
    server.address: my-etp-server.example.com
    server.port: 443
    etp.uri: eml://
    etp.resource_count: 4
Activity.Status: Unset
```

---

## Sending to an OTLP collector (production)

Replace `AddConsoleExporter()` with `AddOtlpExporter()` and configure the endpoint:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyEtpApp"))
    .WithTracing(tracing => tracing
        .AddEtpInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://otel-collector:4317")))
    .WithMetrics(metrics => metrics
        .AddEtpInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://otel-collector:4317")));
```

---

## Activity source and meter names

Use these names if you need to reference them directly (e.g., in `AddSource()` / `AddMeter()` calls in scenarios where you configure OpenTelemetry manually without the extension methods):

| Asset | Name |
|-------|------|
| Activity source | `"EtpClient"` |
| Meter | `"EtpClient"` |

---

## Available metric instruments

| Instrument name | Description |
|-----------------|-------------|
| `etp.client.active_connections` | Number of currently open ETP sessions |
| `etp.client.operation.duration` | Round-trip duration (seconds) per ETP operation |
| `etp.client.operation.errors` | Count of failed ETP operations |
| `etp.client.messages.sent` | Total WebSocket messages sent |
| `etp.client.messages.received` | Total WebSocket messages received |

All instruments carry a `server.address` tag. `etp.client.operation.duration` and `etp.client.operation.errors` additionally carry an `etp.operation` tag with values: `connect`, `disconnect`, `discover`, `channel.describe`, `channel.range_request`, `channel.stream`.

---

## Opting out

Do not call `AddEtpInstrumentation()`. No spans will be created and no measurements will be recorded. The `EtpClient` library has effectively zero observability overhead when instrumentation is not registered.
