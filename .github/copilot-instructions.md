# EtpClient Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-14

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
- C# with .NET 10 + existing Avro helpers, `System.Runtime.CompilerServices.EnumeratorCancellation` for `IAsyncEnumerable` (005-add-channel-streaming)
- ETP Protocol 1 (ChannelStreaming): `DescribeChannelsAsync`, `StartChannelStreamingAsync`, `StopChannelStreamingAsync`, `RequestChannelRangeAsync` on `EtpClient` (005-add-channel-streaming)
- `EtpChannelStreamingException` carries `RequestedTarget` and optional `EtpErrorCode?`; thrown on `ProtocolException` or unexpected message in Protocol 1 operations (005-add-channel-streaming)
- C# with .NET 10 + `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, existing internal Avro reader/writer helpers, sample console infrastructure in `Microsoft.Extensions.Hosting` (006-format-channel-indexes)
- C# with .NET 10 + `EtpClient` project reference, `Spectre.Console`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console`; test-time use of xUnit and NSubstitute, with optional `Spectre.Console.Testing` only if the chosen UI seams need i (007-add-etp-explorer)
- YAML (GitHub Actions), C# / .NET 10 + GitHub Actions built-in (`actions/checkout@v4`, `actions/setup-dotnet@v4`); `dorny/paths-filter@v3` for path-change detection (see research.md) (009-ci-github-workflows)

## Project Structure

```text
src/
  EtpClient/
    Models/          # EtpConnectionOptions, EtpConnectionResult, EtpMessageEncoding, DiscoveryModels, ChannelStreamingModels, ...
    Connection/      # EtpSessionManager, IWebSocketTransport, ClientWebSocketTransport
    Protocol/        # IEtpSessionCodec, BinaryEtpSessionCodec, JsonEtpSessionCodec, GetResourcesMessage, ChannelDescribeMessage, ChannelStreamingStartMessage, ChannelStreamingStopMessage, ChannelRangeRequestMessage, ChannelDataMessage, Avro helpers
    Diagnostics/     # EtpClientLog (structured log events 1001-1020)
samples/
  EtpClient.SampleConsole/   # Console sample using EtpClient
tests/
  EtpClient.UnitTests/       # Unit tests (xUnit + NSubstitute)
  EtpClient.IntegrationTests/# In-process WebSocket server tests (TestHost)
  EtpClient.SampleConsole.Tests/
specs/
  003-support-avro-encoding/ # spec, plan, tasks, contracts, quickstart, research
  004-etp-discovery/         # spec, plan, tasks, contracts, quickstart, research, data-model
  005-channel-streaming/     # spec, plan, tasks, contracts, quickstart, research, data-model
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
- `EtpClientLog` uses `[LoggerMessage]` source gen; event IDs are sequential (1001–1020)
- Transport `SendAsync` signature: `(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct)`
- JSON ETP encoding wire format: `[header_object, body_object]` two-element JSON array over WebSocket text frames
- Discovery: `EtpClient.DiscoverResourcesAsync(uri)` requires `State == Connected`; throws `InvalidOperationException` otherwise
- Discovery: `EtpDiscoveryException` (not `InvalidOperationException`) is thrown for `ProtocolException` or unexpected protocol responses; catch separately
- Discovery: `DiscoveryResult.State` is computed — `CompletedWithResources` when `Resources.Count > 0`, `CompletedEmpty` when `WasEmptyAcknowledged`, else `Failed`
- Discovery: Resource Avro field order (critical for binary codec tests): `uri`, `contentType`, `name`, `channelSubscribable`, `customData`, `resourceType`, `hasChildren`, `uuid` (union[null,string]), `lastChanged`, `objectNotifiable`
- Discovery: Protocol 3 is negotiated as role `"customer"` in `EtpConnectionOptions.RequestedProtocols` by default
- Discovery: `EtpClientLog` discovery events 1007–1010: `DiscoverResourcesStarted`, `DiscoverResourcesComplete`, `DiscoverResourcesEmpty`, `DiscoverResourcesFailed`
- ChannelStreaming: `StartChannelStreamingAsync(IEnumerable<ChannelSubscriptionInfo>, CancellationToken)` returns `IAsyncEnumerable<ChannelEvent>`; requires `[EnumeratorCancellation]` attribute on CT
- ChannelStreaming: `ChannelSubscriptionInfo(channelId, startLatest, receiveChangeNotifications)` — `startLatest=true` maps to StreamingStartIndex union[0]=null
- ChannelStreaming: `StopChannelStreamingAsync(IEnumerable<long> channelIds, CancellationToken)` — fire-and-forget stop; session stays Connected
- ChannelStreaming: `RequestChannelRangeAsync(ChannelRangeRequestModel, CancellationToken)` — aggregates correlated multipart `ChannelData` by `correlationId == messageId`
- ChannelStreaming: `WasMultipart` on range result is `true` only when a non-final frame was received (i.e., more than 1 frame); a single-frame final-part response gives `WasMultipart=false`
- ChannelStreaming: `ChannelDataItem.Indexes` is a list of `long`; `ChannelDataItem.Value` is `object?` (null, double, float, int, long, string, bool, byte[])
- ChannelStreaming: DataValue Avro union indices: `0=null, 1=double, 2=float, 3=int(zigzag), 4=long(zigzag), 5=string, 6=ArrayOfDouble, 7=boolean, 8=bytes`
- ChannelStreaming: StreamingStartIndex union indices: `0=null/latestValue, 1=int/indexCount, 2=long/indexValue`
- ChannelStreaming: `EtpClientLog` channel streaming events 1011–1020
- ChannelStreaming: Protocol 1 message types: `Start=0`, `ChannelDescribe=1`, `ChannelMetadata=2`, `ChannelData=3`, `ChannelStreamingStart=4`, `ChannelStreamingStop=5`, `ChannelDataChange=6`, `ChannelRemove=8`, `ChannelRangeRequest=9`, `ChannelStatusChange=10`
- ChannelStreaming: `EtpChannelStreamingException` (not `InvalidOperationException`) for `ProtocolException` or unexpected protocol messages in Protocol 1 operations

## Recent Changes
- 009-ci-github-workflows: Added YAML (GitHub Actions), C# / .NET 10 + GitHub Actions built-in (`actions/checkout@v4`, `actions/setup-dotnet@v4`); `dorny/paths-filter@v3` for path-change detection (see research.md)
- 007-add-etp-explorer: Added C# with .NET 10 + `EtpClient` project reference, `Spectre.Console`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Console`; test-time use of xUnit and NSubstitute, with optional `Spectre.Console.Testing` only if the chosen UI seams need i
- 006-format-channel-indexes: Added C# with .NET 10 + `System.Net.WebSockets.ClientWebSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, existing internal Avro reader/writer helpers, sample console infrastructure in `Microsoft.Extensions.Hosting`


<!-- MANUAL ADDITIONS START -->
## Manual Coding Conventions

- Avoid explicit `global::` alias usage; prefer relative namespace resolution unless disambiguation is genuinely required.
- Prefer primary constructors for new C# types when they fit the design and improve clarity.
- In xUnit tests, use `ITestOutputHelper` for diagnostic output instead of `Console.Write` or `Console.WriteLine`.
- Make variable, constant, parameter, property, and field names follow standard C# naming conventions and remain compliant with Roslyn analyzers.
- When a feature changes user-visible behavior, setup, samples, or documented client workflows, update the root `README.md` in the same change whenever its guidance is affected.
<!-- MANUAL ADDITIONS END -->
