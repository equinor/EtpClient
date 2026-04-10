# Contract: Sample Console Application

## Purpose

Defines the external contract for the runnable sample application that demonstrates use of the `EtpClient` library.

## Project Entry Point

- Project path: `samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj`
- Launch command: `dotnet run --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj`

## Configuration Contract

### Required configuration keys

| Key | Source | Description |
|---|---|---|
| `Etp:EndpointUri` | .NET user secrets | Absolute `ws` or `wss` endpoint URI |
| `Etp:Username` | .NET user secrets | Basic authentication username |
| `Etp:Password` | .NET user secrets | Basic authentication password |

### Optional configuration keys

| Key | Source | Description |
|---|---|---|
| `Etp:ShowSessionDetails` | app configuration | Controls whether negotiated session fields are printed in detail |

## Behavioral Contract

### Success path

1. Load configuration.
2. Validate required inputs.
3. Construct `EtpConnectionOptions`.
4. Call `EtpClient.ConnectAsync`.
5. Print a success summary.
6. Dispose/close the client cleanly.

### Failure path

The sample must distinguish the following failure categories in its user-facing output:
- Validation
- Authentication
- Transport
- Protocol
- Cancellation

The sample must not print:
- Raw passwords
- Authorization header values
- Base64-encoded credentials

## Output Contract

### Success output

The sample prints a concise success summary containing:
- Endpoint host
- Final connected state
- Server application name
- Server application version
- Server instance identifier

### Failure output

The sample prints a concise failure summary containing:
- Final non-success state
- Failure category
- Secret-safe message suitable for local troubleshooting

## Exit Behavior

- `0`: Session established and the sample completed normally.
- Non-zero: Validation, authentication, transport, protocol, or cancellation outcome.

## Test Contract

Automated tests for the sample must verify:
- Missing configuration fails before connection.
- Successful configuration invokes the public client flow and yields a success outcome.
- Failure categories are rendered distinctly.
- No output path leaks credentials.
