# Contract: Explorer Fixed Stream View

## Purpose

Define the user-visible and testable interaction contract for the explorer’s fixed streaming list.

## Stream Startup Contract

When the user starts streaming from a non-empty selection set:

1. The explorer converts the current selection set into subscriptions.
2. The explorer creates one persistent row for each selected endpoint.
3. The explorer sorts those rows alphabetically by channel name before first render.
4. The explorer enters a streaming-focused view that keeps this row set fixed for the remainder of the session.

## Fixed Row Contract

Each row must expose enough information to identify and monitor one selected endpoint:

- channel name
- source resource or equivalent endpoint attribution
- latest primary index
- latest value
- dedicated status field

The row set must not grow or reorder in response to live updates.

## Initial Row State Contract

Before a channel receives its first data event:

- the row is already visible in the fixed list
- the status field shows `Waiting for data`
- index and value fields remain separate from status and are not replaced by lifecycle text

## Live Update Contract

When a `ChannelData` event arrives for a known row:

- the explorer updates that row in place
- the row’s latest primary index is replaced with the newest index
- the row’s latest value is replaced with the newest value
- the status field transitions to the live state or equivalent active indication

When repeated data events arrive for the same row:

- only the existing row changes
- no extra row is created
- row order remains unchanged

## Lifecycle Event Contract

When a `ChannelStatusChange` event arrives:

- the existing row remains visible
- the dedicated status field is updated to reflect the new channel status
- index/value remain readable as independent fields

When a `ChannelDataChange` event arrives:

- the existing row remains visible
- the dedicated status field reflects that a change event occurred

When a `ChannelRemove` event arrives:

- the existing row remains visible until the streaming session ends
- the dedicated status field changes to an ended or removed state
- the last known index/value remain attributable to that row
- no extra row is created and no row is deleted mid-session

## Stop/Shutdown Contract

When the user stops streaming or the session ends naturally:

- the explorer stops further row updates immediately
- the fixed stream view exits cleanly
- the explorer returns to the connected interactive state
- the stream snapshot is not reused as if it were still live

## Test Contract

Automated coverage for this feature must prove:

1. initial row creation matches the selected endpoint count
2. initial row order is alphabetical by channel name
3. rows begin in `Waiting for data`
4. repeated data events update existing rows in place
5. lifecycle events update the dedicated status field of the correct row
6. removed rows remain visible until the session ends
7. stopping streaming halts further updates and returns the explorer to non-streaming mode
