# Feature Specification: ETP OpenTelemetry Instrumentation

**Feature Branch**: `011-otel-instrumentation`  
**Created**: 2026-05-05  
**Status**: Draft  
**Input**: User description: "I want the library to include open telemetry metrics/traces and expose a WithEtpInstrumentation method for client applications to use to add metrics/traces to their application telemetry"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register ETP Instrumentation in an Application (Priority: P1)

As an application developer using `EtpClient`, I want to call `AddEtpInstrumentation()` on the tracing builder and `AddEtpInstrumentation()` on the metrics builder when configuring OpenTelemetry for my application — following the same pattern as `AddAspNetCoreInstrumentation()` — so that ETP-specific traces and metrics are included in my telemetry pipeline without manually locating activity source names or meter names.

**Why this priority**: This is the primary entry point for the entire feature. Without a discoverable, convention-following integration point, application developers cannot benefit from any of the traces or metrics the library emits.

**Independent Test**: Can be fully tested by calling `AddEtpInstrumentation()` on both the tracer and meter provider builders in a test host, connecting to an ETP endpoint, and verifying that at least one span and one metric measurement are captured by the configured exporter.

**Acceptance Scenarios**:

1. **Given** an application that configures OpenTelemetry via its dependency injection setup, **When** the developer calls `AddEtpInstrumentation()` on the `TracerProviderBuilder` and `AddEtpInstrumentation()` on the `MeterProviderBuilder`, **Then** all ETP trace sources and metric meters are registered without requiring the developer to supply source names or instrument names manually.
2. **Given** both `AddEtpInstrumentation()` registrations are present, **When** the application makes any ETP operation (connect, discover, stream), **Then** the resulting spans and measurements flow through the application's configured exporters (e.g., console, OTLP).
3. **Given** neither `AddEtpInstrumentation()` registration is present, **When** ETP operations are performed, **Then** no spans or measurements are created, ensuring no observability overhead for applications that opt out.
4. **Given** only the tracing `AddEtpInstrumentation()` is registered, **When** ETP operations are performed, **Then** spans are produced but no metric measurements are recorded, and vice versa when only the metrics registration is present.

---

### User Story 2 - Observe ETP Connection Lifecycle via Traces (Priority: P2)

As a developer or operator, I want distributed traces for ETP connection establishment, session negotiation, and disconnection so that I can identify latency, failures, and connection patterns in production telemetry dashboards.

**Why this priority**: Connection-level observability is the most fundamental diagnostic signal for a networking library. It provides immediate value for on-call investigations and capacity planning.

**Independent Test**: Can be fully tested by establishing and closing an ETP session and verifying that the resulting trace contains spans with connection-relevant attributes (endpoint URI, encoding, error status) in the correct parent–child relationship.

**Acceptance Scenarios**:

1. **Given** a client initiating a connection, **When** the connection succeeds, **Then** a trace span is recorded that covers the full connection establishment duration and includes the endpoint URI and negotiated encoding as attributes.
2. **Given** a client initiating a connection, **When** the connection fails (e.g., wrong credentials, unreachable host), **Then** the span is marked as failed with a human-readable status description and the error kind, without embedding credentials.
3. **Given** a connected session that is explicitly closed, **When** the disconnect completes, **Then** a span covering the disconnect is recorded and linked to the originating connection span.

---

### User Story 3 - Observe ETP Operations via Traces (Priority: P2)

As a developer diagnosing a data-retrieval problem, I want individual ETP protocol operations (discovery traversal, channel describe, channel range request) to produce child spans within the active application trace so that I can pinpoint which operation is slow or failing without guessing from log timestamps.

**Why this priority**: Operation-level traces complement connection-level traces and are essential for diagnosing production data-latency issues in connected systems.

**Independent Test**: Can be fully tested by issuing a discovery request and a channel range request within a parent span, then verifying that the resulting trace tree contains one child span per ETP operation with the operation URI or channel ID as a span attribute.

**Acceptance Scenarios**:

1. **Given** an active trace context in the calling application, **When** `DiscoverResourcesAsync` is called, **Then** a child span is created that covers the round-trip from request to final response, annotated with the discovery URI and resource count.
2. **Given** an active trace context, **When** `DescribeChannelsAsync` or `RequestChannelRangeAsync` is called, **Then** a child span is created with relevant channel identifiers and response counts as attributes.
3. **Given** a protocol operation that results in an `EtpDiscoveryException` or `EtpChannelStreamingException`, **Then** the corresponding span is marked failed with the ETP error code (if present) as an attribute.
4. **Given** no active trace context in the calling application, **When** an ETP operation is performed, **Then** spans are still created as root spans so the operation remains independently traceable.

---

### User Story 4 - Monitor ETP Health via Metrics (Priority: P3)

As an operator or SRE, I want metric instruments for ETP connection counts, operation durations, and error rates so that I can build dashboards and alerting rules without instrumenting each operation in application code.

**Why this priority**: Metrics are coarser than traces but cheaper to collect at scale. They enable aggregate health monitoring and alerting patterns that complement the trace-level detail.

**Independent Test**: Can be fully tested by performing a series of ETP operations and verifying that the registered meter produces the expected measurements for counters, histograms, and observable gauges through a test metric exporter.

**Acceptance Scenarios**:

1. **Given** the metrics `AddEtpInstrumentation()` is registered, **When** a connection is established, **Then** the active-connection count instrument increments; **When** the session closes, it decrements.
2. **Given** discovery or channel operations are executed, **When** the operation completes, **Then** a histogram measurement for operation duration is recorded, tagged with operation type and success/failure status.
3. **Given** a protocol operation fails with an ETP error, **When** the error is thrown, **Then** an error counter instrument is incremented, tagged with the operation type and ETP error code.
4. **Given** messages are exchanged over the WebSocket, **When** the session is active, **Then** counters for messages sent and received are updated, allowing operators to observe traffic volume.

---

### Edge Cases

- `AddEtpInstrumentation()` is called more than once on the same `TracerProviderBuilder` or `MeterProviderBuilder` without error or duplicate registrations.
- The OpenTelemetry SDK is not present in the application; the library MUST not fail to load or operate in any way — instrumentation is additive only.
- A span or measurement is attempted after the SDK has been shut down; the library MUST NOT throw or propagate SDK-internal exceptions to callers.
- An ETP operation completes extremely quickly (sub-millisecond); duration measurements remain non-negative.
- Cancellation before a response arrives: the span MUST be closed with `ActivityStatusCode.Error` and status description `"Cancelled"`, with `error.type` set to `"System.OperationCanceledException"`. `ActivityStatusCode` has no `Cancelled` value; `Error` with this description matches the pattern used by `AddAspNetCoreInstrumentation`.
- Attribute values (e.g., URIs) that exceed OTEL attribute length limits are truncated to a well-known maximum rather than causing export failures.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST provide an `AddEtpInstrumentation()` extension method on `TracerProviderBuilder` that registers the ETP activity source into the application's tracer provider, and a separate `AddEtpInstrumentation()` extension method on `MeterProviderBuilder` that registers the ETP meter into the application's meter provider, following the same pattern as `AddAspNetCoreInstrumentation()`.
- **FR-002**: The library MUST create and own a named `ActivitySource` for ETP distributed tracing, using a stable, versioned source name (e.g., `EtpClient`) that follows the OpenTelemetry semantic conventions for naming.
- **FR-003**: The library MUST create and own a named `Meter` for ETP metrics, using a stable, versioned meter name that matches the activity source name convention.
- **FR-004**: Connection establishment MUST produce a trace span covering the full WebSocket upgrade and ETP session negotiation, with endpoint URI (without credentials), negotiated encoding, and outcome (success/failure) as span attributes.
- **FR-005**: Each Discovery protocol operation (`DiscoverResourcesAsync`) MUST produce a trace span with the requested URI, number of resources returned, and success/failure status as attributes.
- **FR-006**: Each Channel Streaming protocol operation (`DescribeChannelsAsync`, `RequestChannelRangeAsync`) MUST produce a trace span with relevant channel identifiers and outcome attributes.
- **FR-007**: Spans for ETP operations MUST be created as children of the ambient `Activity` from the calling application context when one exists, preserving distributed trace propagation.
- **FR-008**: The library MUST provide metric instruments for: active connection count (UpDownCounter), operation duration by operation type (Histogram), operation error count by operation type and ETP error code (Counter), and messages sent and received (Counter).
- **FR-009**: All metric instruments MUST include an operation-type dimension so consumers can filter dashboards and alerts by operation (connect, discover, describe, range-request, stream).
- **FR-010**: The instrumentation MUST be entirely opt-in: if neither `AddEtpInstrumentation()` registration is present, the library MUST NOT create any `Activity` objects or record any measurements, ensuring zero observability overhead for non-instrumented applications. Each registration is independently optional — an application may register only tracing or only metrics.
- **FR-011**: The library MUST NOT include credentials, tokens, or secrets in any span attribute, tag, or metric label.
- **FR-012**: The feature MUST include automated tests verifying that spans and measurements are produced for the connection, discovery, and channel operation paths.
- **FR-013**: The `README.md` MUST be updated to document how to call `AddEtpInstrumentation()` on both `TracerProviderBuilder` and `MeterProviderBuilder`, and list the emitted activity source name, meter name, and key instrument names.

### Key Entities

- **ActivitySource (`EtpClient`)**: The named diagnostic source that creates spans for ETP operations. Owned by the library; registered into the application's tracer provider by `tracerBuilder.AddEtpInstrumentation()`.
- **Meter (`EtpClient`)**: The named metric producer for ETP measurements. Owned by the library; registered into the application's meter provider by `meterBuilder.AddEtpInstrumentation()`.
- **ETP Span**: A single `Activity` covering one logical ETP operation (connection, discovery request, channel describe, channel range request) with standardized attributes.
- **ETP Instrument**: A metric counter, histogram, or up-down-counter scoped to a specific observable property of the ETP session or its operations.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An application that calls `AddEtpInstrumentation()` on both the tracer and meter provider builders and connects to an ETP server captures at least one trace span and one metric measurement in its configured exporter during a single end-to-end test run.
- **SC-002**: All public `EtpClient` protocol operations (connect, discover, describe channels, request range) each produce a named, attribute-annotated span that appears in the trace output with the correct parent–child hierarchy.
- **SC-003**: The active-connection metric accurately reflects the number of live sessions: the count increments on successful connect and decrements on every disconnect, verified by test assertions.
- **SC-004**: An application that does **not** call either `AddEtpInstrumentation()` registration produces no spans and no metric measurements after performing the same set of ETP operations, verified by asserting an empty exporter.
- **SC-005**: No credentials or secret values appear in any span attribute or metric dimension in the automated test output.
- **SC-006**: The activity source name and meter name documented in `README.md` match the names asserted in the automated tests.
- **SC-007**: Registering only `AddEtpInstrumentation()` on the tracer builder (but not the meter builder) produces spans but no metric measurements, and registering only the meter builder variant produces measurements but no spans, each verified independently.

## Assumptions

- The application targets .NET 10 and has access to `OpenTelemetry.Extensions.Hosting` and `OpenTelemetry` SDK packages; the `EtpClient` library itself uses only built-in `System.Diagnostics` types (`ActivitySource`, `Meter`) so that applications without the OTEL SDK still compile and run without error.
- `AddEtpInstrumentation()` follows the same pattern as `AddAspNetCoreInstrumentation()`: one method extends `TracerProviderBuilder` (from `OpenTelemetry`) and one extends `MeterProviderBuilder` (from `OpenTelemetry`). Neither carries options parameters. This implies adding a dependency on the `OpenTelemetry` SDK package in the `EtpClient` library project for the extension methods.
- Span attribute naming follows OpenTelemetry semantic conventions where applicable (e.g., `server.address`, `error.type`) and uses an `etp.` prefix for domain-specific attributes (e.g., `etp.uri`, `etp.encoding`, `etp.error_code`).
- Live channel streaming events (`StartChannelStreamingAsync`) are counter-instrumented per individual data event but do NOT produce individual spans per event, to avoid excessive span volume in high-throughput scenarios. The `etp.operation` tag value for `MessagesSent`, `MessagesReceived`, and any `OperationDuration` measurement in the streaming message loop is `"etp.channel.stream"`, consistent with the dot-separated naming pattern used for all other operations.
- Scope is limited to the `EtpClient` library and its existing protocol operations; no new ETP protocol operations are added by this feature.
- The feature does not change any existing public API signatures — instrumentation is purely additive.

## Clarifications

### Session 2026-05-05

- Q: Should `AddEtpInstrumentation()` extension methods ship in the main `EtpClient` library (accepting a transitive `OpenTelemetry` dependency for all consumers) or in a separate `EtpClient.OpenTelemetry` package? → A: Ship in the main library; consumers accept the transitive `OpenTelemetry` dependency.
- Q: How should a span be closed when an ETP operation is cancelled via `CancellationToken`? → A: Set `ActivityStatusCode.Error` with description `"Cancelled"` and `error.type = "System.OperationCanceledException"`.
- Q: What `etp.operation` tag value should `MessagesSent` / `MessagesReceived` counters and streaming `OperationDuration` measurements use? → A: `"etp.channel.stream"`.
