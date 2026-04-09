# Contract: ETP Client Connection API

## Purpose

Define the public library surface for the minimum authenticated ETP connection slice.

## Public Types

### `EtpConnectionOptions`

- Must contain the endpoint URI, Basic authentication credentials, and session request settings.
- Must support validation before any network call is made.
- Must not expose helper members that serialize or log credentials directly.

### `EtpConnectionState`

- Enumerates the externally visible states:
  - `Connecting`
  - `Connected`
  - `Failed`
  - `Canceled`
  - `Closed`

### `EtpConnectionResult`

- Returned when a connection attempt completes successfully.
- Must expose negotiated session details required by later protocol work.

### `EtpConnectionException`

- Returned or thrown for failed attempts.
- Must distinguish authentication, transport, validation, protocol, and cancellation failures.
- Must not expose secrets in message text.

## Public Operations

### `ConnectAsync`

- **Input**: `EtpConnectionOptions`, `CancellationToken`
- **Behavior**:
  - Opens the WebSocket connection.
  - Sends the Protocol 0 `RequestSession` message.
  - Waits for `OpenSession` before reporting success.
- **Success output**: `EtpConnectionResult`
- **Failure output**: typed failure or exception with secret-safe details.

### `CloseAsync`

- **Input**: `CancellationToken`
- **Behavior**:
  - Cleanly shuts down an established session.
  - Releases transport resources.
- **Output**: completion notification only.

### `CurrentState`

- Read-only state observable by callers.
- Must transition deterministically across all success, failure, cancellation, and closure flows.

## Protocol Mapping Expectations

- The client must follow the documented local protocol sequence for this feature:
  - Open WebSocket connection.
  - Send `RequestSession`.
  - Transition to connected only after `OpenSession` is received.
- Protocol 1 messages are not part of this contract yet.

## Diagnostics Expectations

- The API may emit structured diagnostics for lifecycle and failure events.
- Diagnostics must exclude raw passwords and encoded authorization header values.
