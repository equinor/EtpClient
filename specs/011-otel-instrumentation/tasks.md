# Tasks: ETP OpenTelemetry Instrumentation

**Input**: Design documents from `specs/011-otel-instrumentation/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/AddEtpInstrumentation.md](contracts/AddEtpInstrumentation.md)

**Tests**: Tests are REQUIRED per constitution. Unit tests cover span attributes, metric instruments, and no-op behavior; integration tests cover end-to-end span/metric capture through real operation paths.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4)
- All paths are repository-relative

---

## Phase 1: Setup

**Purpose**: Add package dependencies and create the new source folder.

- [X] T001 Add `OpenTelemetry` package version to `Directory.Packages.props` (production) and `OpenTelemetry.Exporter.InMemory` for test projects
- [X] T002 Add `<PackageReference Include="OpenTelemetry" />` to `src/EtpClient/EtpClient.csproj`
- [X] T003 [P] Add `<PackageReference Include="OpenTelemetry.Exporter.InMemory" />` to `tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj`
- [X] T004 [P] Add `<PackageReference Include="OpenTelemetry.Exporter.InMemory" />` to `tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement the `EtpInstrumentation` static class with the `ActivitySource`, `Meter`, and all five metric instruments. This must exist before any call sites or extension methods can be written.

**⚠️ CRITICAL**: No user story implementation can begin until T005 is complete.

- [X] T005 Create `src/EtpClient/Instrumentation/EtpInstrumentation.cs` — static class with `static readonly ActivitySource Source` (named `"EtpClient"`, versioned from assembly), `static readonly Meter EtpMeter` (named `"EtpClient"`, versioned), and the five metric instruments: `ActiveConnections` (UpDownCounter\<int\>, unit `{connection}`), `OperationDuration` (Histogram\<double\>, unit `s`), `OperationErrors` (Counter\<long\>, unit `{error}`), `MessagesSent` (Counter\<long\>, unit `{message}`), `MessagesReceived` (Counter\<long\>, unit `{message}`)
- [X] T006 Add `StartConnectActivity(string host, int port)`, `StartOperationActivity(string spanName, string host, int port)`, and `RecordOperationError(string operation, string host, int? etpErrorCode)` helper methods to `src/EtpClient/Instrumentation/EtpInstrumentation.cs`; enforce the 512-char truncation rule on `etp.uri` / `etp.channel_target` attributes and the `server.address`-from-`Uri.Host`-only rule

**Checkpoint**: `EtpInstrumentation` is complete and independently compilable. No call sites exist yet.

---

## Phase 3: User Story 1 — Register ETP Instrumentation (Priority: P1) 🎯 MVP

**Goal**: Deliver the two `AddEtpInstrumentation()` extension methods that follow the `AddAspNetCoreInstrumentation()` pattern, allowing applications to opt in to ETP tracing and metrics independently.

**Independent Test**: Call `AddEtpInstrumentation()` on a `TracerProviderBuilder` with an in-memory exporter, perform a no-op operation, and assert the source `"EtpClient"` is registered. Repeat for `MeterProviderBuilder`. Assert that omitting both registrations leaves the exporters empty.

### Tests for User Story 1 (write first, assert failure before T009–T010)

- [X] T007 [P] [US1] Create `tests/EtpClient.UnitTests/Instrumentation/TracerProviderBuilderExtensionsTests.cs` — assert `AddEtpInstrumentation()` registers source `"EtpClient"`, is idempotent on repeated calls, and returns the builder for chaining
- [X] T008 [P] [US1] Create `tests/EtpClient.UnitTests/Instrumentation/MeterProviderBuilderExtensionsTests.cs` — assert `AddEtpInstrumentation()` registers meter `"EtpClient"`, is idempotent on repeated calls, and returns the builder for chaining

### Implementation for User Story 1

- [X] T009 [P] [US1] Create `src/EtpClient/Instrumentation/TracerProviderBuilderExtensions.cs` — `public static TracerProviderBuilder AddEtpInstrumentation(this TracerProviderBuilder builder)` calling `builder.AddSource("EtpClient")` and returning `builder`
- [X] T010 [P] [US1] Create `src/EtpClient/Instrumentation/MeterProviderBuilderExtensions.cs` — `public static MeterProviderBuilder AddEtpInstrumentation(this MeterProviderBuilder builder)` calling `builder.AddMeter("EtpClient")` and returning `builder`

**Checkpoint**: Both extension methods compile and tests T007–T008 pass. US1 is independently deliverable.

---

## Phase 4: User Story 2 — Connection Lifecycle Traces (Priority: P2)

**Goal**: Instrument `ConnectAsync` and `CloseAsync` in `EtpSessionManager` so that connection establishment and disconnection each produce a named span with the correct attributes and status.

**Independent Test**: Using the in-memory exporter, call `ConnectAsync` against a fake transport, assert an `etp.connect` span is recorded with `server.address`, `server.port`, and `etp.encoding` attributes and `OK` status. Call `CloseAsync`, assert an `etp.disconnect` span. Force a connection failure, assert the `etp.connect` span has `Error` status and `error.type` attribute but no credential values.

### Tests for User Story 2

- [X] T011 [US2] Create `tests/EtpClient.UnitTests/Instrumentation/ConnectionInstrumentationTests.cs` — assert: (a) `etp.connect` span name, `server.address`, `server.port`, `etp.encoding` attributes on success; (b) `etp.connect` span `Error` status + `error.type` on connection failure with no credentials in any attribute; (c) `etp.disconnect` span recorded by `CloseAsync`; (d) no spans produced when `AddEtpInstrumentation()` is not registered on the tracer builder

### Implementation for User Story 2

- [X] T012 [US2] Modify `src/EtpClient/Connection/EtpSessionManager.cs` — wrap `ConnectAsync` body: open `etp.connect` activity via `EtpInstrumentation.StartConnectActivity(host, port)`, set `etp.encoding` after negotiation, call `EtpInstrumentation.ActiveConnections.Add(1)` on success, set `ActivityStatusCode.Error` and `etp.error_code` in catch, record `OperationDuration` and `OperationErrors` in finally
- [X] T013 [US2] Modify `src/EtpClient/Connection/EtpSessionManager.cs` — wrap `CloseAsync` body: open `etp.disconnect` activity via `EtpInstrumentation.StartOperationActivity("etp.disconnect", host, port)`, decrement `ActiveConnections` in finally (always), close activity in finally

**Checkpoint**: Tests T011 pass. Connection spans appear in the trace with correct attributes.

---

## Phase 5: User Story 3 — Protocol Operation Traces (Priority: P2)

**Goal**: Instrument `DiscoverResourcesAsync`, `DescribeChannelsAsync`, and `RequestChannelRangeAsync` in `EtpSessionManager` so each produces a child span with operation-specific attributes and correct error/cancellation handling.

**Independent Test**: Create a parent `Activity`, call each operation under it via a fake transport, assert the operation span is a child of the parent span, carries the expected attributes (`etp.uri` / `etp.channel_target` / `etp.channel_count`), and is closed. Trigger an `EtpDiscoveryException` with an error code; assert `Error` status and `etp.error_code` attribute. Call without a parent activity; assert the span is created as a root span.

### Tests for User Story 3

- [X] T014 [P] [US3] Create `tests/EtpClient.UnitTests/Instrumentation/DiscoveryInstrumentationTests.cs` — assert: (a) `etp.discovery` span with `etp.uri`, `etp.resource_count` on success; (b) `etp.uri` truncated at 512 chars; (c) `Error` status and `etp.error_code` on `EtpDiscoveryException`; (d) span is child of ambient activity when one exists; (e) span is root when no ambient activity
- [X] T015 [P] [US3] Create `tests/EtpClient.UnitTests/Instrumentation/ChannelOperationInstrumentationTests.cs` — assert: (a) `etp.channel.describe` span with `etp.channel_target`, `etp.channel_count`; (b) `etp.channel.range_request` span with `etp.channel_count`; (c) `Error` status and `etp.error_code` on `EtpChannelStreamingException`; (d) no spans when tracer not registered

### Implementation for User Story 3

- [X] T016 [US3] Modify `src/EtpClient/Connection/EtpSessionManager.cs` — wrap `DiscoverResourcesAsync`: open `etp.discovery` activity, set `etp.uri` (truncated), set `etp.resource_count` on success, set `Error` status + `error.type` + `etp.error_code` on `EtpDiscoveryException`, record `OperationDuration` and `OperationErrors` in finally
- [X] T017 [US3] Modify `src/EtpClient/Connection/EtpSessionManager.cs` — wrap `DescribeChannelsAsync`: open `etp.channel.describe` activity, set `etp.channel_target` (truncated), set `etp.channel_count` on success, set error attributes on `EtpChannelStreamingException`, record duration/errors in finally
- [X] T018 [US3] Modify `src/EtpClient/Connection/EtpSessionManager.cs` — wrap `RequestChannelRangeAsync`: open `etp.channel.range_request` activity, set `etp.channel_count` from request, set error attributes on `EtpChannelStreamingException`, record duration/errors in finally

**Checkpoint**: Tests T014–T015 pass. All protocol operations produce correctly parented spans.

---

## Phase 6: User Story 4 — Health Metrics (Priority: P3)

**Goal**: Wire all five metric instruments into the operation call sites so that counters, histograms, and the active-connection gauge produce correct measurements through a registered `MeterProvider`.

**Independent Test**: Register only `AddEtpInstrumentation()` on the `MeterProviderBuilder` (not tracer). Perform connect/disconnect and assert `etp.client.active_connections` increments then decrements. Perform discovery; assert `etp.client.operation.duration` has a measurement with `etp.operation=discover`. Force a failure; assert `etp.client.operation.errors` increments with the correct tags. Assert `etp.client.messages.sent` and `etp.client.messages.received` increment per frame.

### Tests for User Story 4

- [X] T019 [US4] Create `tests/EtpClient.UnitTests/Instrumentation/MetricInstrumentTests.cs` — assert: (a) `etp.client.active_connections` +1 on connect success, −1 on close (always, even on error), gauge does not drift across multiple connect/close cycles; (b) `etp.client.operation.duration` recorded for connect, discover, channel.describe, channel.range_request with correct `etp.operation` tag; (c) `etp.client.operation.errors` incremented on exception with `etp.operation` and `etp.error_code` tags; (d) `etp.client.messages.sent` / `etp.client.messages.received` increment per transport frame; (e) no measurements produced when `MeterProviderBuilder.AddEtpInstrumentation()` is not called
- [X] T020 [US4] Create `tests/EtpClient.IntegrationTests/Instrumentation/InstrumentationIntegrationTests.cs` — end-to-end test using `TestHost` fake server: registers both tracing and metrics in-memory exporters, performs connect + discover + close, asserts at least one span per operation in trace exporter and matching metric measurements in metric exporter

### Implementation for User Story 4

- [X] T021 [US4] Modify `src/EtpClient/Connection/EtpSessionManager.cs` — add `MessagesSent.Add(1, ...)` call site in `IWebSocketTransport.SendAsync` wrapper and `MessagesReceived.Add(1, ...)` in the inbound message read path (the two call sites in the existing message loop)
- [X] T022 [US4] Verify all `OperationDuration.Record(elapsed, ...)` and `OperationErrors.Add(1, ...)` call sites placed in T012, T013, T016, T017, T018 carry the correct `etp.operation` tag value per the schema in [data-model.md](data-model.md)

**Checkpoint**: Tests T019–T020 pass. All metric instruments produce correct measurements independently of the tracer registration.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Goal**: README update, build verification, and credential-safety audit.

- [X] T023 [P] Update `README.md` — add an "OpenTelemetry Instrumentation" section documenting: `AddEtpInstrumentation()` usage on both builders, the activity source name `"EtpClient"`, the meter name `"EtpClient"`, and a table of the five instrument names with their units and key tags (per FR-013 and SC-006)
- [X] T024 [P] Audit all new span attribute set-sites in `src/EtpClient/Instrumentation/EtpInstrumentation.cs` and `src/EtpClient/Connection/EtpSessionManager.cs` — confirm no `Uri.UserInfo`, `Uri.AbsoluteUri`, password, or token value is set on any activity tag or metric tag (per FR-011 and SC-005)
- [X] T025 Run `dotnet build` across the full solution and resolve any errors or analyzer warnings introduced by the new `OpenTelemetry` package reference
- [X] T026 Run `dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj` and `dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj` — all new and existing tests must pass

---

## Dependencies

```
T001 → T002, T003, T004
T002, T005 → T006
T005, T006 → T007, T008, T009, T010, T011, T012, T013, T014, T015, T016, T017, T018, T019
T009, T010 → T007, T008 (tests validate the extension methods)
T012, T013 → T011 (tests validate connection instrumentation)
T016, T017, T018 → T014, T015 (tests validate operation instrumentation)
T012, T013, T016, T017, T018 → T021, T022 (metric call sites depend on span call sites being in place)
T021, T022 → T019, T020
T019, T020 → T023, T024, T025, T026
```

## Parallel Execution Examples

**US1 (after T005–T006)**:
- T007 and T008 can run in parallel (different test files)
- T009 and T010 can run in parallel (different source files)

**US2 + US3 (after T005–T006)**:
- T012/T013 (US2) and T014/T015 (US3 tests) can start in parallel once EtpInstrumentation is done
- T014 and T015 can run in parallel (different test files)
- T016, T017, T018 can run in parallel if editing different method bodies in EtpSessionManager

**Final phase**:
- T023 (README) and T024 (audit) can run in parallel

## Implementation Strategy

**MVP scope — deliver in order**:
1. Phase 1 + Phase 2 (T001–T006): packages and `EtpInstrumentation` core
2. Phase 3 (T007–T010): extension methods — US1 is independently demonstrable
3. Phases 4–5 (T011–T018): connection and operation spans — US2/US3
4. Phase 6 (T019–T022): metrics — US4
5. Final Phase (T023–T026): polish, README, build check

Each phase produces independently testable, deployable functionality. US1 alone is sufficient to verify the integration pattern works before wiring in any spans or metrics.

**Total tasks**: 26  
**Parallelizable**: T003, T004, T007, T008, T009, T010, T014, T015, T023, T024 (10 tasks)
