# Data Model: ETP Basic Auth Connection

## ConnectionProfile

- **Purpose**: Describes everything required to initiate an authenticated ETP connection.
- **Fields**:
  - `EndpointUri`: WebSocket endpoint address.
  - `Username`: Basic authentication user name.
  - `Password`: Basic authentication secret.
  - `ClientInstanceId`: Client identifier used in the session request.
  - `RequestedProtocols`: Protocol identifiers requested during session negotiation.
  - `KeepAliveInterval`: Requested keep-alive interval for the WebSocket.
  - `ConnectionTimeout`: Maximum time allowed for the connection attempt.
- **Validation rules**:
  - `EndpointUri` must be absolute and use `ws` or `wss`.
  - `Username` and `Password` are required and must not be blank.
  - Session request metadata must be present before any network attempt starts.

## SessionAttempt

- **Purpose**: Tracks the runtime lifecycle of one connection attempt.
- **Fields**:
  - `AttemptId`: Correlation identifier for the attempt.
  - `State`: Current lifecycle state.
  - `StartedAtUtc`: Connection start timestamp.
  - `CompletedAtUtc`: Final timestamp when the attempt completes.
  - `NegotiatedSessionInfo`: Session details captured from `OpenSession` when successful.
  - `Failure`: Populated only when the attempt fails.
- **State transitions**:
  - `Created -> Connecting`
  - `Connecting -> Connected`
  - `Connecting -> Failed`
  - `Connecting -> Canceled`
  - `Connected -> Closed`
  - `Connected -> Failed`

## NegotiatedSessionInfo

- **Purpose**: Captures session metadata returned when the endpoint accepts the connection.
- **Fields**:
  - `ServerInstanceId`
  - `ProtocolVersion`
  - `SupportedProtocols`
  - `EndpointCapabilities`
- **Validation rules**:
  - Required fields from the accepted session message must be present before the client transitions to `Connected`.

## ConnectionFailure

- **Purpose**: Normalized failure result returned to callers and diagnostics.
- **Fields**:
  - `Category`: Validation, Authentication, Transport, Protocol, or Cancellation.
  - `Message`: Secret-safe human-readable summary.
  - `Exception`: Underlying exception when available.
  - `HttpStatusCode`: Optional handshake status when response details are available.
  - `ProtocolMessageType`: Optional ETP message type associated with the failure.
- **Validation rules**:
  - `Message` must not contain the raw password or the encoded authorization header.

## DiagnosticsEvent

- **Purpose**: Structured event emitted for connection lifecycle changes.
- **Fields**:
  - `EventName`
  - `AttemptId`
  - `State`
  - `EndpointHost`
  - `Details`
- **Validation rules**:
  - Diagnostic payloads may include endpoint host and state, but not credentials or authorization header contents.
