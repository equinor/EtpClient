# Quickstart: ETP Basic Auth Connection

## 1. Create the solution structure

From the repository root:

```bash
dotnet new sln -n EtpClient
dotnet new classlib -n EtpClient -f net10.0 -o src/EtpClient
dotnet new xunit -n EtpClient.UnitTests -f net10.0 -o tests/EtpClient.UnitTests
dotnet new xunit -n EtpClient.IntegrationTests -f net10.0 -o tests/EtpClient.IntegrationTests
dotnet sln EtpClient.sln add src/EtpClient/EtpClient.csproj tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj
dotnet add tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj reference src/EtpClient/EtpClient.csproj
dotnet add tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj reference src/EtpClient/EtpClient.csproj
```

## 2. Implement the minimum connection slice

- Add connection configuration and lifecycle models to `src/EtpClient/Models/`.
- Add the transport and state-machine code to `src/EtpClient/Connection/`.
- Add Protocol 0 request/response message models to `src/EtpClient/Protocol/`.
- Add secret-safe diagnostics helpers to `src/EtpClient/Diagnostics/`.

## 3. Build the client surface

The first public API should let a caller:

- Supply endpoint and Basic authentication settings.
- Start an async connection attempt with cancellation.
- Observe whether the session reaches `Connected`, `Failed`, `Canceled`, or `Closed`.
- Close the session explicitly.

## 4. Test the feature

Run the test suite:

```bash
dotnet test EtpClient.sln
```

Expected test coverage for this slice:

- Unit tests for configuration validation, state transitions, message serialization, and secret-safe error formatting.
- Integration tests for successful handshake, invalid credentials, incomplete session negotiation, and caller cancellation.

## 5. Verify the acceptance criteria manually

- Point the integration harness at a reachable ETP endpoint that accepts Basic authentication.
- Confirm the client reports `Connected` only after the WebSocket opens and `OpenSession` is received.
- Retry with invalid credentials and confirm an authentication-specific failure.
- Cancel an in-progress attempt and confirm the final state is `Canceled` or `Closed` with no active session left behind.
