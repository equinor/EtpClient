# etp_test Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-10

## Active Technologies
- C# with .NET 10 + `EtpClient` project reference, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console` (002-sample-console-app)
- .NET user secrets for local development inputs; no application data storage (002-sample-console-app)
- C# with .NET 10 + `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions` (001-etp-basic-auth)
- `EtpMessageEncoding` enum (`Binary=0`, `Json=1`) controls Avro binary vs JSON-over-text frames (003-support-avro-encoding)
- `IEtpSessionCodec` / `BinaryEtpSessionCodec` / `JsonEtpSessionCodec` in `src/EtpClient/Protocol/` (003-support-avro-encoding)
- `IWebSocketTransport.SendAsync` requires `WebSocketMessageType` parameter (003-support-avro-encoding)

## Project Structure

```text
src/
  EtpClient/
    Models/          # EtpConnectionOptions, EtpConnectionResult, EtpMessageEncoding, ...
    Connection/      # EtpSessionManager, IWebSocketTransport, ClientWebSocketTransport
    Protocol/        # IEtpSessionCodec, BinaryEtpSessionCodec, JsonEtpSessionCodec, Avro helpers
    Diagnostics/     # EtpClientLog (structured log events 1001-1006)
samples/
  EtpClient.SampleConsole/   # Console sample using EtpClient
tests/
  EtpClient.UnitTests/       # Unit tests (xUnit + NSubstitute)
  EtpClient.IntegrationTests/# In-process WebSocket server tests (TestHost)
  EtpClient.SampleConsole.Tests/
specs/
  003-support-avro-encoding/ # spec, plan, tasks, contracts, quickstart, research
```

## Commands

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj
dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj
dotnet test tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj
```

## Code Style

C# with .NET 10: Follow standard conventions

## Key Conventions

- `EtpConnectionResult` has required `MessageEncoding` — always set in test factories
- `FakeLogger` is under `Microsoft.Extensions.Logging.Testing` (not `Microsoft.Extensions.Diagnostics.Testing`)
- Test files using `TestHost` suppress `CS0618` with `#pragma warning disable/restore CS0618`
- `EtpClientLog` uses `[LoggerMessage]` source gen; event IDs are sequential (1001–1006)
- Transport `SendAsync` signature: `(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct)`
- JSON ETP encoding wire format: `[header_object, body_object]` two-element JSON array over WebSocket text frames

## Recent Changes
- 003-support-avro-encoding: Added configurable binary/JSON ETP message encoding via `EtpMessageEncoding`, `IEtpSessionCodec` abstraction, frame-type mismatch detection, `EtpConnectionResult.MessageEncoding`, `EtpClientLog.EncodingSelected` (event 1006)
- 002-sample-console-app: Added C# with .NET 10 + `EtpClient` project reference, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console`


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
