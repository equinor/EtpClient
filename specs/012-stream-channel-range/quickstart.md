# Quickstart: Stream Channel Range

**Feature**: 012-stream-channel-range

---

## What changes

`RequestChannelRangeAsync` previously returned `Task<ChannelRangeResult>`, blocking until every
`ChannelData` response frame arrived before delivering aggregated results. It now returns
`IAsyncEnumerable<ChannelDataItem>`, yielding each data point as soon as the frame it belongs
to is decoded.

---

## Typical usage

```csharp
using EtpClient;
using EtpClient.Models;

await using var client = new EtpClient();
await client.ConnectAsync(options, ct);

var description = await client.DescribeChannelsAsync([targetUri], ct);
var channelIds = description.Channels.Select(c => c.ChannelId).ToList();

var request = new ChannelRangeRequestModel
{
    ChannelIds = channelIds,
    FromIndex  = fromIndexMs, // e.g. DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds()
    ToIndex    = toIndexMs,   // e.g. DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
};

await foreach (var item in client.RequestChannelRangeAsync(request, ct))
{
    Console.WriteLine($"Channel {item.ChannelId}  index={item.Indexes[0]}  value={item.Value}");
}
```

---

## Collecting all items (migration from `ChannelRangeResult.Samples`)

```csharp
var samples = new List<ChannelDataItem>();
await foreach (var item in client.RequestChannelRangeAsync(request, ct))
    samples.Add(item);
// samples now contains all items — equivalent to the old ChannelRangeResult.Samples
```

---

## Error handling

```csharp
try
{
    await foreach (var item in client.RequestChannelRangeAsync(request, ct))
    {
        // process item
    }
}
catch (EtpChannelStreamingException ex)
{
    Console.Error.WriteLine($"Range request failed: {ex.Message} (ETP error {ex.EtpErrorCode})");
}
```

---

## Breaking changes

| Before | After |
|---|---|
| `Task<ChannelRangeResult> RequestChannelRangeAsync(...)` | `IAsyncEnumerable<ChannelDataItem> RequestChannelRangeAsync(...)` |
| `result.Samples` — aggregated list | Iterate with `await foreach` |
| `result.WasMultipart` — bool | Not tracked; multipart delivery is transparent |
| `result.State == Completed` | Normal enumeration completion |
| `ChannelRangeResultState` enum | Removed |
| `ChannelRangeResult` class | Removed |
