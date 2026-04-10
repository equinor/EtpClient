# etp_test Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-05-30

## Active Technologies
- C# with .NET 10 + `EtpClient` project reference, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console` (002-sample-console-app)
- .NET user secrets for local development inputs; no application data storage (002-sample-console-app)
- C# with .NET 10 + `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions` (001-etp-basic-auth)
- `EtpMessageEncoding` enum (`Binary=0`, `Json=1`) controls Avro binary vs JSON-over-text frames (003-support-avro-encoding)
- `IEtpSessionCodec` / `BinaryEtpSessionCodec` / `JsonEtpSessionCodec` in `src/EtpClient/Protocol/` (003-support-avro-encoding)
- `IWebSocketTransport.SendAsync` requires `WebSocketMessageType` parameter (003-support-avro-encoding)
- C# with .NET 10 + `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, existing internal Avro reader/writer helpers (004-add-etp-discovery)
- ETP Protocol 3 (Discovery): `DiscoverResourcesAsync(string uri)` returns `DiscoveryResult` with `Resources`, `WasEmptyAcknowledged`, `State`, `MessageEncoding` (004-add-etp-discovery)
- `EtpDiscoveryException` carries `RequestedUri` and optional `EtpErrorCode?`; thrown on `ProtocolException` or unexpected message (004-add-etp-discovery)

## Project Structure

```text
src/
  EtpClient/
    Models/          # EtpConnectionOptions, EtpConnectionResult, EtpMessageEncoding, DiscoveryModels, ...
    Connection/      # EtpSessionManager, IWebSocketTransport, ClientWebSocketTransport
    Protocol/        # IEtpSessionCodec, BinaryEtpSessionCodec, JsonEtpSessionCodec, GetResourcesMessage, GetResourcesResponseMessage, Avro helpers
    Diagnostics/     # EtpClientLog (structured log events 1001-1010)
samples/
  EtpClient.SampleConsole/   # Console sample using EtpClient
tests/
  EtpClient.UnitTests/       # Unit tests (xUnit + NSubstitute)
  EtpClient.IntegrationTests/# In-process WebSocket server tests (TestHost)
  EtpClient.SampleConsole.Tests/
specs/
  003-support-avro-encoding/ # spec, plan, tasks, contracts, quickstart, research
  004-etp-discovery/         # spec, plan, tasks, contracts, quickstart, research, data-model
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
- `EtpClientLog` uses `[LoggerMessage]` source gen; event IDs are sequential (1001–1010)
- Transport `SendAsync` signature: `(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct)`
- JSON ETP encoding wire format: `[header_object, body_object]` two-element JSON array over WebSocket text frames
- Discovery: `EtpClient.DiscoverResourcesAsync(uri)` requires `State == Connected`; throws `InvalidOperationException` otherwise
- Discovery: `EtpDiscoveryException` (not `InvalidOperationException`) is thrown for `ProtocolException` or unexpected protocol responses; catch separately
- Discovery: `DiscoveryResult.State` is computed — `CompletedWithResources` when `Resources.Count > 0`, `CompletedEmpty` when `WasEmptyAcknowledged`, else `Failed`
- Discovery: Resource Avro field order (critical for binary codec tests): `uri`, `contentType`, `name`, `channelSubscribable`, `customData`, `resourceType`, `hasChildren`, `uuid` (union[null,string]), `lastChanged`, `objectNotifiable`
- Discovery: Protocol 3 is negotiated as role `"customer"` in `EtpConnectionOptions.RequestedProtocols` by default
- Discovery: `EtpClientLog` discovery events 1007–1010: `DiscoverResourcesStarted`, `DiscoverResourcesComplete`, `DiscoverResourcesEmpty`, `DiscoverResourcesFailed`

## Recent Changes
- 004-add-etp-discovery: Added ETP Protocol 3 Discovery: `DiscoverResourcesAsync(string uri)` on `EtpClient`, multipart `GetResourcesResponse` aggregation, `Acknowledge`-as-empty mapping, `EtpDiscoveryException` for protocol failures, Protocol 3 auto-negotiated in `EtpConnectionOptions`, discovery log events 1007–1010, sample app discovers `eml://` after connect
- 003-support-avro-encoding: Added configurable binary/JSON ETP message encoding via `EtpMessageEncoding`, `IEtpSessionCodec` abstraction, frame-type mismatch detection, `EtpConnectionResult.MessageEncoding`, `EtpClientLog.EncodingSelected` (event 1006)
- 002-sample-console-app: Added C# with .NET 10 + `EtpClient` project reference, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console`


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
