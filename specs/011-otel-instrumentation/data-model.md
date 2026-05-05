# Data Model: ETP OpenTelemetry Instrumentation

**Feature**: `011-otel-instrumentation`  
**Branch**: `011-otel-instrumentation`

## Overview

This feature introduces a single new concern — the `Instrumentation/` folder inside `src/EtpClient/` — plus call sites inside the existing `EtpSessionManager`. No new ETP protocol entities are added. The model below describes the key types, their relationships, and the state transitions relevant to instrumentation.

---

## Core Types

### `EtpInstrumentation` (static class)

The central owner of the `ActivitySource` and `Meter` singletons plus all helper methods that create spans and record measurements. Internal to the library; application code never references it directly.

| Member | Kind | Description |
|--------|------|-------------|
| `Source` | `ActivitySource` (static readonly) | Named `"EtpClient"`, versioned with the library assembly version. Singleton for the process lifetime. |
| `Meter` | `Meter` (static readonly) | Named `"EtpClient"`, versioned with the library assembly version. Singleton for the process lifetime. |
| `ActiveConnections` | `UpDownCounter<int>` | Created from `Meter`. Tracks currently-open ETP sessions. |
| `OperationDuration` | `Histogram<double>` | Created from `Meter`. Records operation round-trip time in seconds. |
| `OperationErrors` | `Counter<long>` | Created from `Meter`. Increments on any failed ETP operation. |
| `MessagesSent` | `Counter<long>` | Created from `Meter`. Increments per outbound WebSocket frame. |
| `MessagesReceived` | `Counter<long>` | Created from `Meter`. Increments per inbound WebSocket frame. |
| `StartConnectActivity(host, port)` | static method | Opens an `Activity` named `"etp.connect"` as a child of the ambient context. Returns `null` if no listener is attached. |
| `StartOperationActivity(spanName, host, port)` | static method | Generic helper for protocol operation spans. |
| `RecordOperationError(operation, host, errorCode)` | static method | Increments `OperationErrors` with appropriate tags. |

**Lifecycle**: `EtpInstrumentation` is initialized once when the class is first accessed. The `ActivitySource` and `Meter` are never disposed during normal operation (consistent with the BCL pattern for long-lived diagnostic sources).

---

### `TracerProviderBuilderExtensions` (static class)

Extension methods on `OpenTelemetry.Trace.TracerProviderBuilder`.

| Method | Signature | Effect |
|--------|-----------|--------|
| `AddEtpInstrumentation` | `(this TracerProviderBuilder) → TracerProviderBuilder` | Calls `builder.AddSource("EtpClient")`. Returns the builder for chaining. |

---

### `MeterProviderBuilderExtensions` (static class)

Extension methods on `OpenTelemetry.Metrics.MeterProviderBuilder`.

| Method | Signature | Effect |
|--------|-----------|--------|
| `AddEtpInstrumentation` | `(this MeterProviderBuilder) → MeterProviderBuilder` | Calls `builder.AddMeter("EtpClient")`. Returns the builder for chaining. |

---

## Metric Instrument Details

| Instrument | Type | Unit | Tag keys | Notes |
|------------|------|------|----------|-------|
| `etp.client.active_connections` | `UpDownCounter<int>` | `{connection}` | `server.address` | +1 on `ConnectAsync` success; −1 on `CloseAsync` or error cleanup |
| `etp.client.operation.duration` | `Histogram<double>` | `s` | `etp.operation`, `server.address`, `error.type`* | Measured from operation start to final response (success or first exception) |
| `etp.client.operation.errors` | `Counter<long>` | `{error}` | `etp.operation`, `server.address`, `etp.error_code`* | `etp.error_code` tag present only when an ETP protocol error code is available |
| `etp.client.messages.sent` | `Counter<long>` | `{message}` | `server.address` | One per `IWebSocketTransport.SendAsync` call |
| `etp.client.messages.received` | `Counter<long>` | `{message}` | `server.address` | One per complete WebSocket message read |

*\* Optional tags — only present on error measurements*

### `etp.operation` tag values

| Value | Triggered by |
|-------|-------------|
| `connect` | `ConnectAsync` |
| `disconnect` | `CloseAsync` |
| `discover` | `DiscoverResourcesAsync` |
| `channel.describe` | `DescribeChannelsAsync` |
| `channel.range_request` | `RequestChannelRangeAsync` |
| `channel.stream` | Individual `ChannelData` events in `StartChannelStreamingAsync` |

---

## Activity (Span) Details

### `etp.connect`

| Attribute | Value |
|-----------|-------|
| `server.address` | `options.Endpoint.Host` |
| `server.port` | `options.Endpoint.Port` |
| `etp.encoding` | `"binary"` or `"json"` (set after negotiation) |
| `error.type` | Exception type name (on failure) |
| `etp.error_code` | ETP protocol error code integer (on `EtpConnectionException` with a code) |

### `etp.disconnect`

| Attribute | Value |
|-----------|-------|
| `server.address` | endpoint host |
| `server.port` | endpoint port |

### `etp.discovery`

| Attribute | Value |
|-----------|-------|
| `server.address` | endpoint host |
| `server.port` | endpoint port |
| `etp.uri` | requested URI (truncated to 512 chars) |
| `etp.resource_count` | count of resources returned (set on success) |
| `error.type` | exception type (on failure) |
| `etp.error_code` | ETP error code (on `EtpDiscoveryException` with a code) |

### `etp.channel.describe`

| Attribute | Value |
|-----------|-------|
| `server.address` | endpoint host |
| `server.port` | endpoint port |
| `etp.channel_target` | first URI in the describe request (truncated to 512 chars) |
| `etp.channel_count` | channels returned (set on success) |
| `error.type` | exception type (on failure) |
| `etp.error_code` | ETP error code (on `EtpChannelStreamingException` with a code) |

### `etp.channel.range_request`

| Attribute | Value |
|-----------|-------|
| `server.address` | endpoint host |
| `server.port` | endpoint port |
| `etp.channel_count` | number of channel IDs in the request |
| `error.type` | exception type (on failure) |
| `etp.error_code` | ETP error code (on `EtpChannelStreamingException` with a code) |

---

## State Transitions (Connection Instrumentation)

```
[ConnectAsync called]
    │
    ├─ etp.connect span STARTED (child of ambient Activity)
    │
    ├─ Success path ──► etp.encoding attribute SET
    │                   etp.connect span ENDED (OK status)
    │                   etp.client.active_connections += 1
    │                   etp.client.operation.duration recorded (operation=connect)
    │
    └─ Failure path ──► error.type attribute SET
                        etp.error_code attribute SET (if ETP code present)
                        etp.connect span ENDED (Error status)
                        etp.client.operation.duration recorded (operation=connect, error.type)
                        etp.client.operation.errors += 1 (operation=connect)

[CloseAsync called]
    │
    ├─ etp.disconnect span STARTED
    │
    ├─ Success path ──► etp.disconnect span ENDED (OK status)
    │                   etp.client.active_connections -= 1
    │
    └─ Failure path ──► etp.disconnect span ENDED (Error status)
                        etp.client.active_connections -= 1 (always, to prevent gauge drift)
```

---

## Validation Rules

- `etp.uri` and `etp.channel_target` attributes MUST be truncated to ≤ 512 characters before being set on a span.
- `server.address` MUST be derived from `Uri.Host` only — never from `Uri.UserInfo`, `Uri.AbsoluteUri`, or `Uri.ToString()` on an URI that contains credentials.
- `etp.error_code` MUST only be set when the exception carries a non-null ETP error code integer. It MUST NOT be set to `0` as a sentinel for "no code".
- `etp.client.active_connections` MUST be decremented in a `finally` block to prevent gauge drift if `CloseAsync` throws.
- Metric instruments and `ActivitySource` MUST be initialized as `static readonly` fields so that they are created once per process, matching the BCL idiom for `ActivitySource` and `Meter`.
