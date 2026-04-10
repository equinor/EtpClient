# Quickstart: Channel Streaming Support

## Goal

Connect to an ETP server, describe streamable channels for a target URI, start a live Protocol 1 stream, and request a bounded historical range using the new typed client API.

## Prerequisites

- .NET 10 SDK installed
- A reachable ETP endpoint with valid credentials
- A server that supports Protocol 1 ChannelStreaming in the producer role
- A valid Protocol 1 target URI, or a URI discovered through the existing Discovery feature
- The default connection profile or a custom requested-protocol set that includes Protocol 1 in the consumer role

## 1. Connect to the server

Use the existing authenticated connection flow. Protocol 1 is already advertised by default.

```csharp
await using var client = new EtpClient(logger);
EtpConnectionResult session = await client.ConnectAsync(options, ct);
```

Expected outcome:

- The session establishes successfully.
- The negotiated protocol set is sufficient for Protocol 1 operations.

## 2. Describe channels for a target URI

Choose a URI that the producer supports for `ChannelDescribe`, for example a WITSML well, wellbore, log, or log-curve URI.

```csharp
ChannelDescriptionResult description = await client.DescribeChannelsAsync(
    new[] { targetUri },
    ct);
```

Expected outcome:

- `description.Channels` contains one or more typed channel definitions.
- Multipart metadata responses are already aggregated.
- If the producer rejects the request, a Protocol 1-specific exception is thrown.

## 3. Inspect returned channel definitions

```csharp
foreach (var channel in description.Channels)
{
    Console.WriteLine($"{channel.ChannelName} ({channel.ChannelUri})");
    Console.WriteLine($"  Id: {channel.ChannelId}");
    Console.WriteLine($"  Index: {channel.IndexType} / {channel.IndexDirection}");
    Console.WriteLine($"  Status: {channel.Status}");
}
```

Use these channel definitions as the basis for live or historical requests.

## 4. Start live streaming

Subscribe using one or more `ChannelSubscriptionInfo` values. Each subscription specifies a channel ID, whether to start from the latest value or a specific index, and whether to receive change notifications.

```csharp
var subscriptions = description.Channels.Select(ch =>
    new ChannelSubscriptionInfo(ch.ChannelId, startLatest: true, receiveChangeNotifications: false));

var events = new List<ChannelEvent>();
await foreach (var evt in client.StartChannelStreamingAsync(subscriptions, ct))
{
    events.Add(evt);
    Console.WriteLine($"[{evt.Kind}] channel={evt.ChannelId}");
    if (evt.Kind == ChannelEventKind.Remove)
        break; // server signalled end of stream
}
```

Expected outcome:

- `ChannelEvent` values of kind `Data`, `DataChange`, `StatusChange`, or `Remove` are yielded as the server sends them.
- The enumeration completes when a `ChannelRemove` is received or the cancellation token is triggered.
- Per-channel ordering is preserved as received.

## 5. Stop selected channels

```csharp
await client.StopChannelStreamingAsync(new[] { someChannelId }, ct);
```

Expected outcome:

- The selected channels stop streaming.
- The ETP session remains connected and reusable for other Protocol 1 or Discovery operations.

## 6. Request a bounded historical range

```csharp
var request = new ChannelRangeRequestModel
{
    ChannelIds = description.Channels.Select(ch => ch.ChannelId).ToList(),
    FromIndex = fromIndex,   // long — depth or time index
    ToIndex = toIndex,
};

ChannelRangeResult range = await client.RequestChannelRangeAsync(request, ct);
```

Expected outcome:

- `range.Samples` contains `ChannelDataItem` values correlated to the initiating range request.
- `range.WasMultipart` indicates whether the server sent the response in more than one frame.
- Multipart range responses are already aggregated before returning.
- If the producer rejects the request a `EtpChannelStreamingException` is thrown.

## 7. Handle Protocol 1 failures separately

```csharp
try
{
    var description = await client.DescribeChannelsAsync(new[] { targetUri }, ct);
}
catch (EtpChannelStreamingException ex)
{
    logger.LogWarning("Protocol 1 operation failed: {Message} (ETP error {Code})",
        ex.Message,
        ex.EtpErrorCode?.ToString() ?? "none");
}
catch (InvalidOperationException)
{
    logger.LogError("Protocol 1 operation called without an active connection.");
}
```

## 8. Verification

Run the automated tests that cover describe, live-stream, range, and sample-app workflows.

```bash
dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj
dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj --filter "FullyQualifiedName~Channel"
dotnet test tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj
```

If a live server is available, verify that the sample app can describe a valid target URI and either receive live data or fail with a secret-safe Protocol 1 error.
