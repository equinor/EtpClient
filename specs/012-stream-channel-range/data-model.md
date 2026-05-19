# Data Model: Stream Channel Range

**Feature**: 012-stream-channel-range  
**Phase**: 1 — Design artifacts

---

## Models removed

### `ChannelRangeResult` (removed)

Previously aggregated the full response of a range request.

| Property | Type | Notes |
|---|---|---|
| `Request` | `ChannelRangeRequestModel` | Carried the original request |
| `Samples` | `IReadOnlyList<ChannelDataItem>` | All decoded items |
| `WasMultipart` | `bool` | Whether >1 frame was received |
| `State` | `ChannelRangeResultState` | Completion/failure state |

**Reason for removal**: Redundant with the streaming model. Callers receive items directly via `IAsyncEnumerable<ChannelDataItem>`; completion is signaled by enumeration end; errors are thrown.

---

### `ChannelRangeResultState` (removed)

Enum with values `Completed`, `IncompleteAfterReconnect`, `Failed`.

**Reason for removal**: All three states are now expressed through the `IAsyncEnumerable` contract:
- `Completed` → enumeration ends normally
- `IncompleteAfterReconnect` → no longer modelled (reconnect not yet in scope)
- `Failed` → `EtpChannelStreamingException` thrown from the enumerator

---

## Models unchanged

### `ChannelRangeRequestModel` (unchanged)

| Property | Type | Notes |
|---|---|---|
| `ChannelIds` | `IReadOnlyList<long>` | Channel IDs from a prior `DescribeChannelsAsync` |
| `FromIndex` | `long` | Start of the index range (inclusive) |
| `ToIndex` | `long` | End of the index range (inclusive) |

---

### `ChannelDataItem` (unchanged)

| Property | Type | Notes |
|---|---|---|
| `ChannelId` | `long` | Which channel the data point belongs to |
| `Indexes` | `IReadOnlyList<long>` | Primary (and any secondary) index values |
| `Value` | `object?` | Decoded value: null, double, float, int, long, string, bool, or byte[] |

---

## SampleConsole model changes

`SampleRunOutcome` in `samples/EtpClient.SampleConsole/` (internal to the sample, not part of the library API):

| Old | New |
|---|---|
| `ChannelRangeResult? ChannelRangeResult` | _(removed)_ |
| _(new)_ | `ChannelRangeRequestModel? RangeRequest` |
| _(new)_ | `IReadOnlyList<ChannelDataItem>? RangeSamples` |

`FromSuccess` factory parameters updated to match.
