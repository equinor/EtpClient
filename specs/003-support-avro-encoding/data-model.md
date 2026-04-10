# Data Model: Support Avro Encoding

## EtpMessageEncoding

**Purpose**: Represents the caller-selected ETP message encoding mode for a session.

**Fields**:
- `Binary`: Uses the existing Avro binary message representation.
- `Json`: Uses the ETP JSON message representation.

**Validation Rules**:
- Exactly one encoding mode is selected for a connection attempt.
- A default encoding is applied when the caller does not specify one.

**Relationships**:
- Selected by `EtpConnectionOptions`.
- Governs both outgoing and incoming message handling for a session.

## EtpConnectionOptions

**Purpose**: Represents the caller-provided connection configuration needed to start an authenticated ETP session.

**Fields**:
- `EndpointUri`: Absolute `ws` or `wss` URI for the ETP endpoint.
- `Username`: Basic authentication username.
- `Password`: Basic authentication password.
- `ClientInstanceId`: Caller or library generated client instance identifier.
- `RequestedProtocols`: Protocol capabilities advertised in the request session flow.
- `KeepAliveInterval`: Requested WebSocket keep-alive interval.
- `ConnectionTimeout`: Maximum time allowed for connection and handshake.
- `MessageEncoding`: Selected ETP message encoding mode.

**Validation Rules**:
- Endpoint URI is absolute and uses `ws` or `wss`.
- Username and password are present and secret-safe.
- Message encoding is always defined, either explicitly or by default.

**Relationships**:
- Creates one encoding-aware session attempt.
- Feeds the transport and codec selection logic.

## EncodingAwareSession

**Purpose**: Represents one active or attempted ETP session bound to a single selected encoding mode.

**Fields**:
- `SelectedEncoding`: The encoding used for the session.
- `ConnectionState`: Closed, connecting, connected, failed, or canceled.
- `NegotiatedSessionInfo`: Session details returned by the server on success.
- `FailureCategory`: Validation, authentication, transport, protocol, cancellation, or encoding-related observable failure.

**Validation Rules**:
- The selected encoding does not change after the connection attempt begins.
- All inbound and outbound protocol messages use the same selected encoding for the session.

**Relationships**:
- Produced by one `EtpConnectionOptions` instance.
- Returns one `EtpConnectionResult` on success or one `EtpConnectionException` on failure.

## EncodingAwareDiagnosticContext

**Purpose**: Describes the observable diagnostic context associated with a connection or session attempt.

**Fields**:
- `EndpointHost`: Secret-safe endpoint host for logging and exceptions.
- `SelectedEncoding`: Binary or JSON.
- `FailureCategory`: The externally visible failure bucket.
- `ProtocolErrorCode`: Optional ETP protocol error code when the server returns one.

**Validation Rules**:
- Must not contain credentials, authorization headers, or raw secret values.
- Must be available on both success-adjacent lifecycle events and failure events where relevant.

## State Transitions

```text
Configured -> Connecting
Connecting -> Connected
Connecting -> Failed
Connecting -> Canceled
Connected -> Closed
Failed -> Closed
Canceled -> Closed
```

The selected encoding is fixed for the full duration of the session attempt and any resulting connected session.
