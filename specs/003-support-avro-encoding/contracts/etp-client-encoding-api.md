# Contract: ETP Client Encoding Selection

## Purpose

Defines the public client contract for selecting ETP message encoding during connection establishment.

## Public API Surface

### `EtpMessageEncoding` enum (`src/EtpClient/Models/EtpMessageEncoding.cs`)

```csharp
public enum EtpMessageEncoding
{
    Binary = 0,  // default
    Json   = 1,
}
```

### `EtpConnectionOptions` — encoding parameter

```csharp
// Constructor (last optional parameter)
new EtpConnectionOptions(
    endpointUri,
    username,
    password,
    // ...other optional params...
    messageEncoding: EtpMessageEncoding.Json  // defaults to Binary
)

// Property
EtpMessageEncoding MessageEncoding { get; }
```

### `EtpConnectionResult` — encoding property

```csharp
// Required property set on every successful result
EtpMessageEncoding MessageEncoding { get; init; }
```

## Public Configuration Contract

The public connection configuration surface allows callers to choose the ETP message encoding for a session.

### Required behavior

- The connection options include one explicit encoding selection field (`MessageEncoding`).
- The field supports both `Binary` and `Json` values.
- When the field is omitted, the client defaults to `Binary`.
- The selected value governs the entire session once the connection attempt begins.

## Behavioral Contract

### Binary selection

1. The caller sets `MessageEncoding = EtpMessageEncoding.Binary` (or omits it — default).
2. The client sends `RequestSession` and receives `OpenSession`/`ProtocolException` using Avro binary format over WebSocket binary frames.
3. If the endpoint supports binary mode, the session establishes and `result.MessageEncoding == Binary`.

### JSON selection

1. The caller sets `MessageEncoding = EtpMessageEncoding.Json`.
2. The client sends `RequestSession` and receives `OpenSession`/`ProtocolException` using JSON-encoded ETP messages (two-element `[header, body]` JSON array) over WebSocket text frames.
3. If the endpoint supports JSON mode, the session establishes and `result.MessageEncoding == Json`.

### Encoding-mismatch failure

If the server responds with a frame type inconsistent with the selected encoding (e.g., returns a binary frame when JSON was selected), the client throws `EtpConnectionException` with `FailureCategory.Protocol`.

### Diagnostics

The client logs encoding selection at `Debug` level (event 1006, message: `"ETP using {MessageEncoding} encoding for connection to {EndpointHost}"`). This log is secret-safe.

### Failure behavior

Failures remain categorized and secret-safe across all categories:

| Category      | Trigger                                              |
|---------------|------------------------------------------------------|
| `Validation`  | Invalid options                                      |
| `Auth`        | HTTP 401/403 on WebSocket upgrade                    |
| `Transport`   | Network/socket errors                                |
| `Protocol`    | Frame-type mismatch, corrupt message, ProtocolException from server |
| `Cancelled`   | `CancellationToken` triggered                        |

## Compatibility Contract

- Existing callers that do not supply an encoding continue to get `Binary` (the documented default).
- Encoding choice is additive; callers do not need to change authentication or endpoint configuration patterns.
- `EtpConnectionResult.MessageEncoding` is a required property — any test factories creating results must supply it.

## Test Contract

Automated tests for this feature verify:

- Default encoding is `Binary` when not specified (`EtpConnectionOptionsEncodingTests`)
- Successful binary-mode session establishment (`ConnectAsyncEncodingTests`, `BinarySessionCodecTests`)
- Successful JSON-mode session establishment (`ConnectAsyncEncodingTests`, `JsonSessionCodecTests`)
- Frame-type mismatch detection — binary server vs JSON client raises `Protocol` failure (`EtpSessionManagerEncodingFailureTests`, `ConnectAsyncEncodingFailureTests`)
- Invalid JSON payload raises `Protocol` failure (`EtpSessionManagerEncodingFailureTests`)
- Encoding-selected diagnostics are secret-safe and at `Debug` level (`EtpClientLogEncodingTests`)
- `EtpConnectionResult.MessageEncoding` matches the selected encoding (`EtpSessionManagerEncodingConsistencyTests`)
- `SendAsync` uses the correct WebSocket frame type for the selected encoding (`EtpSessionManagerEncodingConsistencyTests`)
- Sample app exposes encoding option, forwards to connection options, and displays encoding in output (`SampleConsoleEncodingOptionTests`)
