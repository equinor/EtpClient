# Quickstart: Support Avro Encoding

## Goal

Use the ETP client with an explicit message encoding selection so the caller can choose either binary or JSON transport encoding for the supported session flow.

## Prerequisites

- .NET 10 SDK installed
- A reachable ETP endpoint that supports the scoped Protocol 0 session flow
- Valid Basic authentication credentials for that endpoint
- Knowledge of whether the endpoint expects binary or JSON ETP message encoding

## 1. Choose the desired encoding mode

`EtpMessageEncoding` has two values:

| Value    | Wire format                | WebSocket frame |
|----------|---------------------------|-----------------|
| `Binary` | Avro binary (default)      | Binary          |
| `Json`   | JSON `[header, body]` array | Text           |

If the caller does not specify one, the client defaults to `Binary`.

## 2. Construct connection options

```csharp
using EtpClient.Models;

// Binary (default — omit the parameter or pass explicitly)
var binaryOptions = new EtpConnectionOptions(
    new Uri("wss://example.com/etp"),
    username: "user",
    password: "password");

// JSON
var jsonOptions = new EtpConnectionOptions(
    new Uri("wss://example.com/etp"),
    username: "user",
    password: "password",
    messageEncoding: EtpMessageEncoding.Json);
```

## 3. Connect using the public client API

```csharp
using EtpClient;

var client = new EtpClient(logger);

// Binary session
EtpConnectionResult result = await client.ConnectAsync(binaryOptions, cancellationToken);
Console.WriteLine(result.MessageEncoding);  // Binary

// JSON session
EtpConnectionResult result = await client.ConnectAsync(jsonOptions, cancellationToken);
Console.WriteLine(result.MessageEncoding);  // Json
```

## Expected success behavior

- The client uses the selected encoding for the full session handshake.
- `EtpConnectionResult.MessageEncoding` matches the encoding that was selected.
- Session establishment succeeds when the endpoint supports the mode.
- Diagnostics log the selected encoding at `Debug` level (event 1006), without exposing credentials.

## Expected failure behavior

- If the server responds with a frame type inconsistent with the selected encoding, an `EtpConnectionException` with `FailureCategory.Protocol` is thrown.
- Authentication, transport, and cancellation failures remain distinguishable and secret-safe as before.

## Verification

Run the automated tests to validate encoding behavior:

```bash
# Unit tests (codec, options, session manager, diagnostics)
dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj

# Integration tests (in-process binary + JSON server)
dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj

# Sample app tests
dotnet test tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj
```
