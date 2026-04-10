# Research: Channel Streaming Support

## Decision 1: Implement the consumer-side Protocol 1 slice first

- **Decision**: Scope the feature to the client acting as the Protocol 1 consumer, covering channel description, live streaming start and stop, live lifecycle-event intake, and bounded range requests.
- **Rationale**: The user request is to add ChannelStreaming support to the client library. The repo already models the client as a consumer that connects to an external ETP server, so producer-side behavior is not needed for the first implementation slice.
- **Alternatives considered**:
  - Implement both consumer and producer roles together: rejected because it would expand the feature far beyond the existing client-library responsibilities.
  - Implement live streaming only and omit describe/range behavior: rejected because the specification defines all of these as core consumer operations, and callers need describe to discover valid channel targets.

## Decision 2: Model Protocol 1 metadata and data as typed public results, not raw protocol payloads

- **Decision**: Expose typed channel-definition, live-event, and range-result models through the public library surface rather than returning raw Avro records or JSON dictionaries.
- **Rationale**: Existing features already shield callers from wire-format details. ChannelStreaming should remain consistent so callers can work with strongly typed results and async workflows rather than protocol-frame decoding.
- **Alternatives considered**:
  - Return raw decoded payload dictionaries: rejected because it leaks encoding details into the public API and makes tests more brittle.
  - Expose only a callback-based raw message feed: rejected because it would not satisfy the need for stable description and range result contracts.

## Decision 3: Aggregate multipart `ChannelMetadata` and `ChannelRangeRequest` responses into logical results

- **Decision**: Continue receiving Protocol 1 responses until the final part is observed, collecting `ChannelMetadata` and range-correlated `ChannelData` parts into one logical result for the caller.
- **Rationale**: The ETP v1.1 specification explicitly allows multipart Protocol 1 metadata and range responses. Callers want complete results, not manual multipart assembly.
- **Alternatives considered**:
  - Expose each response part separately: rejected for the first slice because callers would need to reimplement protocol assembly logic.
  - Assume single-part responses: rejected because it would fail on valid producer behavior.

## Decision 4: Represent live streaming as an explicit asynchronous session/subscription contract

- **Decision**: Model live Protocol 1 streaming as an explicit async subscription/session object or equivalent lifecycle-aware contract with cancellation and stop semantics.
- **Rationale**: The constitution requires async, cancellation-aware streaming APIs with explicit lifetime semantics. A live stream is not a one-shot request; it needs controlled startup, consumption, and shutdown behavior.
- **Alternatives considered**:
  - Hide streaming behind synchronous polling: rejected because it conflicts with long-lived stream behavior and the constitution.
  - Couple live streaming to the whole client lifetime with no per-stream control: rejected because the spec allows selected channels to stop while the session remains open.

## Decision 5: Support both full Protocol 1 and SimpleStreamer-compatible sessions

- **Decision**: The feature will support the full consumer-side Protocol 1 workflow and the covered SimpleStreamer interaction model documented by the spec and WITSML guidance.
- **Rationale**: The spec explicitly calls out SimpleStreamer as a valid Protocol 1 profile. Interop would be incomplete if the client only handled the full describe/start/stop exchange.
- **Alternatives considered**:
  - Ignore SimpleStreamer until a later feature: rejected because it would leave a documented protocol mode unsupported despite being in scope for ChannelStreaming.
  - Treat SimpleStreamer as a separate feature: rejected because it is a Protocol 1 compatibility mode, not a different protocol.

## Decision 6: Preserve current connection defaults and reuse the existing protocol negotiation posture

- **Decision**: Keep Protocol 1 in the default requested-protocol list and build the new feature on the existing authenticated session setup, message encoding selection, and receive-loop infrastructure.
- **Rationale**: The repository already advertises Protocol 1 in the default connection profile. Reusing the current handshake and encoding infrastructure minimizes change surface and keeps Protocol 1 behavior consistent with existing Protocol 0 and Protocol 3 support.
- **Alternatives considered**:
  - Require callers to opt in to a separate connection profile for Protocol 1: rejected because Protocol 1 is already part of the default intended usage.
  - Add a parallel transport stack just for streaming: rejected because the existing session manager and codec seam already solve the transport and encoding concerns.

## Decision 7: Extend the sample app to demonstrate describe-first streaming rather than raw protocol traces

- **Decision**: Update the sample app to connect, identify at least one target URI, request `ChannelDescribe`, display returned channel definitions, and optionally start a live stream or bounded range request using the new typed API.
- **Rationale**: The sample should demonstrate the intended public workflow and validate that the feature is usable without requiring callers to inspect raw protocol frames.
- **Alternatives considered**:
  - Leave the sample app unchanged: rejected because the new feature would lack a working end-to-end example.
  - Make the sample app protocol-debug oriented: rejected because the repo’s sample is intended as a consumer example, not a wire inspection tool.
