# Quickstart: Sample Console Application

## Goal

Run the sample console application against a real ETP endpoint using .NET user secrets for local input.

## Prerequisites

- .NET 10 SDK installed
- A reachable ETP endpoint that supports the existing authenticated connection flow
- Valid Basic authentication credentials for that endpoint

## 1. Initialize user secrets for the sample project

The sample project already contains a pre-configured `UserSecretsId` (`etp-client-sample-console`).
No initialization step is required — just set the secrets directly:

## 2. Set the required secrets

```bash
dotnet user-secrets set "Etp:EndpointUri" "wss://example.com/etp" --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj
dotnet user-secrets set "Etp:Username" "your-username" --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj
dotnet user-secrets set "Etp:Password" "your-password" --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj
```

## 3. Run the sample

```bash
dotnet run --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj
```

## Expected success behavior

- The sample validates configuration before connecting.
- The sample opens an authenticated session through the public `EtpClient` API.
- The sample prints a concise success summary including endpoint host and negotiated session details.
- The sample closes the session cleanly before exit.

## Expected failure behavior

- Missing or malformed configuration fails before a network attempt.
- Authentication, transport, protocol, and cancellation failures are reported distinctly.
- No console output includes credential values or authorization headers.

## Optional overrides

- Set `Etp:ShowSessionDetails` to `true` in `appsettings.json` (or via user secrets) to print negotiated session fields (server application name, version, and instance ID) in the success summary.
- If the implementation preserves the default .NET configuration precedence, environment variables and command-line arguments may override user-secret values for non-secret experimentation.

## Verification

Run the repository test suite:

```bash
dotnet test
```
