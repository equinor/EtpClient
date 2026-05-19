# EtpClient

[![CI](https://github.com/equinor/EtpClient/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/equinor/EtpClient/actions/workflows/ci.yml)

`EtpClient` is a .NET 10 ETP client library focused on authenticated session setup, Discovery traversal, and Protocol 1 ChannelStreaming workflows against ETP servers.

The repository contains:

- the reusable client library in `src/EtpClient/`
- sample applications in `samples/`
- unit, integration, and sample tests in `tests/`
- feature quickstarts and design notes in `specs/`

## What the library supports

- Connect to an ETP server over `ws` or `wss` using Basic authentication.
- Complete Protocol 0 session negotiation.
- Discover resources with Protocol 3 using `DiscoverResourcesAsync`.
- Describe streamable channels with Protocol 1 using `DescribeChannelsAsync`.
- Start live subscriptions with `StartChannelStreamingAsync`.
- Stop live subscriptions with `StopChannelStreamingAsync`.
- Request bounded historical ranges with `RequestChannelRangeAsync`.
- Use either binary or JSON ETP message encoding.

## OpenTelemetry instrumentation

`EtpClient` can emit traces and metrics via the .NET `ActivitySource` and `Meter` APIs.
Wire them into your OpenTelemetry pipeline with the two extension methods:

```csharp
services.AddOpenTelemetry()
    .WithTracing(b => b.AddEtpInstrumentation())
    .WithMetrics(b => b.AddEtpInstrumentation());
```

### Spans

| Span name | Triggered by |
|---|---|
| `etp.connect` | `ConnectAsync` |
| `etp.disconnect` | `CloseAsync` |
| `etp.discovery` | `DiscoverResourcesAsync` |
| `etp.channel.describe` | `DescribeChannelsAsync` |
| `etp.channel.range_request` | `RequestChannelRangeAsync` |

All spans carry `server.address` and `server.port` attributes. Operation spans additionally set
`etp.uri` / `etp.channel_target`, `etp.resource_count` / `etp.channel_count` on success, and
`etp.error_code` on ETP protocol errors.

### Metrics

| Instrument | Kind | Unit | Description |
|---|---|---|---|
| `etp.client.active_connections` | UpDownCounter | `{connection}` | Currently active sessions |
| `etp.client.operation.duration` | Histogram | `s` | Duration of each operation |
| `etp.client.operation.errors` | Counter | `{error}` | Failed operations |
| `etp.client.messages.sent` | Counter | `{message}` | Outbound WebSocket frames |
| `etp.client.messages.received` | Counter | `{message}` | Inbound WebSocket frames |

## Work in progress:

- (Sample) Complete the EtpExplorer sample application. This is an interactive console application for navigating a ETP server which supports streaming data to the console. This is still a bit rough in the edges

## Quick overview

The typical flow is:

1. Create `EtpConnectionOptions` with endpoint URI, username, password, and optional message encoding.
2. Connect with `ConnectAsync`.
3. Discover resources from `eml://` or another target URI.
4. Choose a resource URI and describe its channels.
5. Subscribe to one or more channel IDs and process streamed events.
6. Close the session when finished.

## Basic usage

```csharp
using EtpClient;
using EtpClient.Models;

await using var client = new EtpClient();

var options = new EtpConnectionOptions(
    new Uri("wss://your-server/etp"),
    username: "your-username",
    password: "your-password",
    messageEncoding: EtpMessageEncoding.Binary);

EtpConnectionResult connection = await client.ConnectAsync(options, ct);

DiscoveryResult discovery = await client.DiscoverResourcesAsync("eml://", ct);

var targetUri = discovery.Resources
    .FirstOrDefault(resource => resource.ChannelSubscribable)
    ?.Uri;

if (targetUri is not null)
{
    ChannelDescriptionResult description = await client.DescribeChannelsAsync([targetUri], ct);

    var channelsById = description.Channels.ToDictionary(channel => channel.ChannelId);

    var subscriptions = description.Channels
        .Select(channel => new ChannelSubscriptionInfo(
            channel.ChannelId,
            startLatest: true,
            receiveChangeNotifications: false))
        .ToList();

    await foreach (var evt in client.StartChannelStreamingAsync(subscriptions, ct))
    {
        if (evt.Kind == ChannelEventKind.Data)
        {
            foreach (var item in evt.DataItems)
            {
                var channelName = channelsById.TryGetValue(item.ChannelId, out var channel)
                    ? channel.ChannelName
                    : item.ChannelId.ToString();

                var indexText = string.Join(", ", item.Indexes);
                Console.WriteLine($"[{channelName}] index={indexText} value={item.Value}");
            }
        }

        if (evt.Kind == ChannelEventKind.Remove)
            break;
    }
}

await client.CloseAsync(ct);
```

## Starting from a specific index

By default the examples above use `startLatest: true`, which asks the server to start each
channel stream from its latest measured value (the ETP wire value is a null
`StreamingStartIndex`). In practice, this typically means you receive the current/latest sample
immediately, followed by subsequent updates.

To replay historical data from a known index position, use the overload that accepts a
`startIndexValue` instead:

```csharp
// Example: 2023-11-14T22:13:20Z expressed as Unix epoch microseconds
long startUs = 1_700_000_000_000_000L;

var subscriptions = description.Channels
    .Select(channel => new ChannelSubscriptionInfo(
        channel.ChannelId,
        startIndexValue: startUs,
        receiveChangeNotifications: false))
    .ToList();
```

The meaning of `startIndexValue` depends on the channel's **index type**, reported in
`ChannelDefinition.IndexType` from `DescribeChannelsAsync`:

- **`"Time"`** — raw value is microseconds from the Unix epoch (1970-01-01T00:00:00Z), unless
  `channel.IndexTimeDatum` is set, in which case it is microseconds from that ISO 8601 datum.
- **`"Depth"`** — raw value is a scaled integer. Divide by `10^channel.IndexScale` to get the
  physical depth in the units reported by `channel.IndexUom`.

The value must match the **producer's index units exactly** — always check `IndexType`, `IndexUom`,
`IndexScale`, and `IndexTimeDatum` on the `ChannelDefinition` before constructing the index.

The server streams all recorded data points whose primary index is **≥ `startIndexValue`**, and
then continues with live data until the subscription is stopped.

## Requesting a bounded historical range

`RequestChannelRangeAsync` returns an `IAsyncEnumerable<ChannelDataItem>` that yields each
data point as it arrives from the server:

```csharp
var request = new ChannelRangeRequestModel
{
    ChannelIds = description.Channels.Select(c => c.ChannelId).ToList(),
    FromIndex  = 1_700_000_000_000_000L,
    ToIndex    = 1_700_100_000_000_000L,
};

await foreach (var item in client.RequestChannelRangeAsync(request, ct))
{
    var indexText = string.Join(", ", item.Indexes);
    Console.WriteLine($"[channel {item.ChannelId}] index={indexText} value={item.Value}");
}
```

Index units follow the same conventions as live streaming — check `IndexType`, `IndexUom`,
`IndexScale`, and `IndexTimeDatum` on the `ChannelDefinition` before constructing the range.

## Configuration notes

The sample applications bind settings from the `Etp` configuration section. The required keys are:

- `Etp:EndpointUri`
- `Etp:Username`
- `Etp:Password`

Optional keys used by the samples include:

- `Etp:MessageEncoding`
- `Etp:ProtocolRequestTimeoutSeconds`
- `Etp:ChannelUri`
- `Etp:ChannelRangeFromIndex`
- `Etp:ChannelRangeToIndex`
- `Etp:SkipDiscovery`
- `Etp:ShowSessionDetails`

Example user-secret setup for the sample console:

```bash
dotnet user-secrets --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj set "Etp:EndpointUri" "wss://your-server/etp"
dotnet user-secrets --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj set "Etp:Username" "your-username"
dotnet user-secrets --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj set "Etp:Password" "your-password"
```

Example user-secret setup for the explorer:

```bash
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:EndpointUri" "wss://your-server/etp"
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:Username" "your-username"
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:Password" "your-password"
```

## Available samples

### `samples/EtpClient.SampleConsole`

Guided sample for the core library workflow:

- validate configuration
- connect to an ETP server
- optionally run discovery
- optionally describe channels
- optionally start live streaming
- optionally request a historical range

Run it with:

```bash
dotnet run --project samples/EtpClient.SampleConsole/EtpClient.SampleConsole.csproj
```

### `samples/EtpExplorer`

Interactive console explorer for browsing an ETP server:

- connect and discover root nodes
- browse resources in a pane-style interface
- select streamable endpoints
- start live streaming with a **fixed-row view**: one persistent row per selected endpoint,
  sorted alphabetically, showing `Waiting for data` until the first measurement arrives and
  updating the index, value, and status fields in place as new events come in

Run it with:

```bash
dotnet run --project samples/EtpExplorer/EtpExplorer.csproj
```

## Quickstarts

The `specs/` folder contains focused quickstarts for the major library slices:

| Quickstart | Summary |
|---|---|
| `specs/001-etp-basic-auth/quickstart.md` | Basic authenticated connection and Protocol 0 handshake |
| `specs/002-sample-console-app/quickstart.md` | Sample console app setup and execution flow |
| `specs/003-support-avro-encoding/quickstart.md` | Binary vs JSON ETP message encoding |
| `specs/004-etp-discovery/quickstart.md` | Resource discovery and traversal with Protocol 3 |
| `specs/005-channel-streaming/quickstart.md` | Channel describe, live streaming, and range requests |
| `specs/006-format-channel-indexes/quickstart.md` | Interpreting and formatting channel index values |
| `specs/007-add-etp-explorer/quickstart.md` | Interactive explorer sample usage |
| `specs/008-search-column-filter/quickstart.md` | Planned explorer column search and filtering workflow |
| `specs/010-fix-streaming-list/quickstart.md` | Fixed-row streaming list validation steps |
| `specs/011-otel-instrumentation/quickstart.md` | OpenTelemetry tracing and metrics setup |

## Build and test

Build the repository:

```bash
dotnet build
```

Run the main test projects:

```bash
dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj
dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj
dotnet test tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj
dotnet test tests/EtpExplorer.Tests/EtpExplorer.Tests.csproj
```

## Repository layout

```text
src/EtpClient/                     # reusable client library
samples/EtpClient.SampleConsole/   # guided sample console
samples/EtpExplorer/               # interactive explorer sample
tests/EtpClient.UnitTests/         # unit tests
tests/EtpClient.IntegrationTests/  # transport and protocol integration tests
tests/EtpClient.SampleConsole.Tests/
tests/EtpExplorer.Tests/
specs/                             # feature specs, plans, and quickstarts
docs/                              # ETP specification references and notes
```
