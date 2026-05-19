# Public API Contract: RequestChannelRangeAsync

**Feature**: 012-stream-channel-range

---

## `IEtpClient` — updated method signature

```csharp
/// <summary>
/// Requests historical channel data for a bounded primary-index range using Protocol 1.
/// Yields each <see cref="ChannelDataItem"/> as it is received from the server.
/// Enumeration completes when the server sends the final-part <c>ChannelData</c> message.
/// </summary>
/// <param name="request">Range request identifying channels, start index, and end index.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// An async sequence of <see cref="ChannelDataItem"/> values streamed from the server.
/// </returns>
/// <exception cref="InvalidOperationException">
/// Thrown when the session is not <see cref="EtpConnectionState.Connected"/>.
/// </exception>
/// <exception cref="EtpChannelStreamingException">
/// Thrown when the server returns a <c>ProtocolException</c> or an unexpected message type
/// during the range exchange.
/// </exception>
IAsyncEnumerable<ChannelDataItem> RequestChannelRangeAsync(
    ChannelRangeRequestModel request,
    CancellationToken ct = default);
```

---

## Removed from `IEtpClient` (and `EtpClient`)

The following types are removed from the public API:

- `ChannelRangeResult` class
- `ChannelRangeResultState` enum

---

## Behaviour contract

| Condition | Observable outcome |
|---|---|
| Server sends one or more `ChannelData` frames, last has `FinalPart` flag | All items yielded in arrival order; enumeration completes normally |
| Server sends `ChannelData` with `FinalPart` but zero items | Enumeration completes without yielding anything |
| `CancellationToken` is cancelled mid-iteration | Enumeration stops cleanly; no exception thrown to caller |
| Server sends `ProtocolException` correlated to the request | `EtpChannelStreamingException` thrown |
| Server sends unexpected message type correlated to the request | `EtpChannelStreamingException` thrown |
| Frame with non-matching `CorrelationId` received | Frame skipped silently |
| Session is not `Connected` at call time | `InvalidOperationException` thrown before any `ChannelRangeRequest` is sent |

---

## `IEtpConnector` (sample interface — not library API)

```csharp
IAsyncEnumerable<ChannelDataItem> RequestChannelRangeAsync(
    ChannelRangeRequestModel request,
    CancellationToken ct);
```
