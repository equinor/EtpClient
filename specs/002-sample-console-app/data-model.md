# Data Model: Sample Console Application

## SampleConsoleOptions

**Purpose**: Represents the user-facing configuration the sample binds from configuration sources before constructing `EtpConnectionOptions`.

**Fields**:
- `EndpointUri`: Absolute `ws` or `wss` URI for the ETP endpoint.
- `Username`: Basic authentication username.
- `Password`: Basic authentication password.
- `ShowSessionDetails`: Optional boolean controlling whether negotiated session details are printed in the success output.

**Validation Rules**:
- `EndpointUri` is required.
- `EndpointUri` must be absolute and use `ws` or `wss`.
- `Username` is required and cannot be blank.
- `Password` is required and cannot be blank.
- Validation occurs before constructing the library connection options.

**Relationships**:
- Converts into one `EtpConnectionOptions` instance.
- Produces one `SampleRunOutcome` instance after execution.

## SampleRunOutcome

**Purpose**: Represents the summarized result of one sample execution.

**Fields**:
- `Succeeded`: Boolean indicating whether the session was established.
- `FinalState`: Final connection state observed by the sample.
- `FailureCategory`: Optional validation/authentication/transport/protocol/cancellation category when unsuccessful.
- `ServerApplicationName`: Optional server application name from negotiated session info.
- `ServerApplicationVersion`: Optional server application version from negotiated session info.
- `ServerInstanceId`: Optional server instance identifier from negotiated session info.
- `EndpointHost`: Endpoint host shown in output without credentials.

**Validation Rules**:
- `FailureCategory` must be absent on success.
- Negotiated session fields are only populated on success.
- No output field may contain raw credentials or authorization values.

**Relationships**:
- Derived from `EtpConnectionResult` or `EtpConnectionException`.
- Displayed by the console presentation layer.

## SecretConfigurationContract

**Purpose**: Describes the expected local-development secret keys used to populate `SampleConsoleOptions`.

**Fields**:
- `Etp:EndpointUri`
- `Etp:Username`
- `Etp:Password`

**Validation Rules**:
- All three keys are required for a successful run.
- Values are read from user secrets first, then may be overridden by environment variables or command-line arguments if implementation chooses to preserve standard .NET configuration precedence.
- Secret keys are documented, but secret values are never checked into source control.

## State Transitions

```text
NotConfigured -> InvalidConfiguration
NotConfigured -> ReadyToConnect
ReadyToConnect -> Connecting
Connecting -> Connected
Connecting -> Failed
Connecting -> Canceled
Connected -> Closed
Failed -> Closed
Canceled -> Closed
```
