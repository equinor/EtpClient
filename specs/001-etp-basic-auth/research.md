# Research: ETP Basic Auth Connection

## Decision 1: Implement the client as a `.NET 10 / C#` class library

- **Decision**: Use a new C# class library targeting `.NET 10`.
- **Rationale**: The user explicitly requested `.NET 10/C#`, and the feature is a reusable client surface rather than an application. A class library keeps the API focused on connection and session behavior while remaining portable across supported .NET host types.
- **Alternatives considered**:
  - Console application: rejected because the feature is a reusable client capability, not an end-user tool.
  - Multi-target older .NET versions: rejected for this slice because the request explicitly sets `.NET 10` as the implementation target.

## Decision 2: Use `ClientWebSocket` and set the Basic auth header before `ConnectAsync`

- **Decision**: Use `System.Net.WebSockets.ClientWebSocket` and set the `Authorization` header with `ClientWebSocketOptions.SetRequestHeader` before connecting.
- **Rationale**: Microsoft documents `ClientWebSocket` as the supported client transport for the opening handshake, and `ClientWebSocketOptions.SetRequestHeader` is the supported way to add HTTP headers to the handshake request. This keeps the transport stack minimal and avoids building a custom handshake layer.
- **Alternatives considered**:
  - Manually composing a raw WebSocket upgrade request: rejected because it duplicates framework behavior and increases protocol risk.
  - Using `HttpMessageInvoker` for the initial implementation: rejected because the .NET WebSocket guidance warns about option ambiguity when mixing invoker-level and `ClientWebSocketOptions` settings. The first slice does not need connection pooling or HTTP/2-specific transport control.

## Decision 3: Keep the first slice on Protocol 0 connection establishment only

- **Decision**: Implement WebSocket connection establishment, `RequestSession`, `OpenSession` handling, and clean close/cancel behavior, while deferring Protocol 1 `Start` and streaming flows.
- **Rationale**: The feature specification is scoped to the minimum required to connect with Basic authentication. The local ETP/WITSML reference documents the broader streaming flow, but only the Protocol 0 session setup is necessary for this slice.
- **Alternatives considered**:
  - Including Protocol 1 `Start`: rejected because it expands the feature beyond the requested minimum and would force early design of message streaming APIs.
  - Deferring Protocol 0 parsing entirely: rejected because the client cannot truthfully report a connected ETP session without session acceptance.

## Decision 4: Model connection lifecycle as a small async state machine

- **Decision**: Represent connection behavior through explicit states such as `Connecting`, `Connected`, `Failed`, `Canceled`, and `Closed`.
- **Rationale**: The constitution requires deterministic, diagnosable, asynchronous behavior. A small state machine makes cancellation, cleanup, and error mapping explicit and testable.
- **Alternatives considered**:
  - Exposing only boolean connection flags: rejected because it does not distinguish failed, canceled, and closed outcomes.
  - Hiding lifecycle transitions internally: rejected because callers need observable states for reliable integration.

## Decision 5: Use `Microsoft.Extensions.Logging.Abstractions` for diagnostics contracts

- **Decision**: Depend on `Microsoft.Extensions.Logging.Abstractions` in the library and treat logging as optional through injected `ILogger` usage.
- **Rationale**: Microsoft documents the abstractions package as the standard contract surface for logging in .NET class libraries. It allows structured diagnostics without forcing a concrete logging provider on consumers.
- **Alternatives considered**:
  - Custom logging interfaces: rejected because they increase maintenance and reduce ecosystem interoperability.
  - Console logging in the library: rejected because a reusable client library should not own output sinks.

## Decision 6: Use xUnit for unit and integration tests

- **Decision**: Create xUnit-based unit and integration test projects and run them with `dotnet test`.
- **Rationale**: Microsoft testing guidance documents xUnit as a standard option for .NET test projects, and it works cleanly for a class library plus lightweight integration harness. This is sufficient for the constitution’s required unit and wire-behavior coverage.
- **Alternatives considered**:
  - MSTest: acceptable, but not selected because xUnit has a lighter default footprint for library-focused TDD.
  - Separate contract test project in the first slice: rejected because the public API and handshake behavior can be covered adequately by unit plus integration tests at this stage.
