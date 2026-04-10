# Contract: ETP Client Channel Streaming API

## Purpose

Defines the public client contract for consumer-side Protocol 1 operations: describing channels, starting and stopping live streams, receiving channel lifecycle events, and requesting bounded historical ranges.

## Public API Contract

The library exposes asynchronous Protocol 1 operations after session establishment.

### Required behavior

- The caller can describe one or more ETP resource URIs that are valid Protocol 1 targets.
- The caller can start live streaming for one or more described channels.
- The caller can stop selected channels without closing the entire ETP session.
- The caller can request historical data for a bounded primary-index range.
- All operations are asynchronous and cancellation-aware.
- All returned results are typed rather than raw protocol payloads.
- Protocol 1 operations are valid only while the client session is connected.

## Channel Description Contract

### Request inputs

- One or more ETP resource URIs
- Existing authenticated session context
- Selected session message encoding

### Request semantics

1. The caller requests channel description for one or more URIs.
2. The client sends Protocol 1 `ChannelDescribe`.
3. The client receives one or more `ChannelMetadata` messages, or a `ProtocolException` if the request is invalid or unsupported.
4. Multipart metadata responses are aggregated into one logical result.

### Result semantics

- The result exposes typed channel definitions.
- Channel ordering is preserved in producer response order.
- The result contains enough metadata for downstream live or historical requests.

## Live Streaming Contract

### Start inputs

- One or more channel identifiers or URIs derived from description results
- Optional starting indexes or equivalent stream-start parameters
- Change-notification preferences when supported by the producer

### Start semantics

1. The caller starts live streaming for selected channels.
2. The client sends `ChannelStreamingStart`, or `Start` where the producer profile requires it.
3. The client surfaces incoming `ChannelData`, `ChannelDataChange`, `ChannelStatusChange`, and `ChannelRemove` events through a typed stream lifecycle contract.

### Stop semantics

- The caller can stop selected channels by sending `ChannelStreamingStop`.
- Stopping selected channels does not force the underlying ETP session to close.
- If the producer sends `ChannelRemove`, the client marks that channel as no longer active.

## Range Request Contract

### Request inputs

- One or more channel identifiers or URIs
- `FromIndex`
- `ToIndex`
- Existing authenticated session context

### Request semantics

1. The caller requests a bounded historical range.
2. The client sends `ChannelRangeRequest`.
3. The client receives one or more correlated `ChannelData` messages.
4. Multipart responses are aggregated into one logical range result.

### Result semantics

- Returned samples remain correlated to the initiating range request.
- Samples preserve Protocol 1 index ordering.
- A multipart range response interrupted by reconnect is not treated as complete.

## SimpleStreamer Compatibility Contract

- When a producer identifies itself as a SimpleStreamer, the client can still receive producer-sent metadata and live data as documented by the covered Protocol 1 flow.
- The client does not require undocumented producer-specific behavior beyond what the spec defines for the SimpleStreamer mode.

## Failure Contract

The client must distinguish Protocol 1 failures from connection-establishment failures.

### Examples of failure behavior

- Unsupported or invalid describe URI → protocol failure with secret-safe detail
- Invalid range indexes or rejected stream-start parameters → protocol failure with actionable context
- Unexpected Protocol 1 message sequence → protocol failure with correlation context
- Authentication, transport, or disconnected-session issues before Protocol 1 exchange → existing connection failure behavior remains in effect

## Sample-App Contract

The sample app demonstrates a describe-first Protocol 1 workflow and exposes the resulting channel information and stream outcomes in a human-readable form.

### Required sample behavior

- Connect using the existing authenticated flow.
- Request channel description for a configured or discovered target URI.
- Print returned channel definitions in a readable form.
- Demonstrate starting and stopping a live stream, or request a bounded range when live streaming is unavailable in the environment.
- Fail secret-safely when Protocol 1 operations are rejected or unsupported.

## Test Contract

Automated tests for this feature must verify:

- typed channel-description results for valid URIs
- multipart `ChannelMetadata` aggregation
- live `ChannelData` delivery and per-channel ordering preservation
- handling of `ChannelDataChange`, `ChannelStatusChange`, and `ChannelRemove`
- selected-channel stop behavior without closing the session
- correlated `ChannelRangeRequest` results, including multipart range responses
- reconnect-sensitive incomplete range behavior
- covered SimpleStreamer compatibility behavior
- secret-safe protocol failure behavior for unsupported or invalid Protocol 1 requests
