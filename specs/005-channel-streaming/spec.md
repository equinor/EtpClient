# Feature Specification: Channel Streaming Support

**Feature Branch**: `005-add-channel-streaming`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "Add ChannelStreaming (protocol 1) support per the specification"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Describe Streamable Channels (Priority: P1)

As a developer connected to an ETP server, I want to describe one or more channel-oriented resources so I can learn which channels are available for streaming before I request live or historical data.

**Why this priority**: Channel streaming is not usable until the caller can identify the available channels, their identifiers, and the metadata needed to interpret incoming values correctly.

**Independent Test**: Can be fully tested by connecting to a Protocol 1 producer, describing a valid resource URI, and verifying that the client returns the complete set of channel definitions the producer exposes for that URI.

**Acceptance Scenarios**:

1. **Given** an authenticated session and a valid describable resource URI, **When** the caller requests channel description, **Then** the client returns the channel definitions the producer exposes beneath that URI.
2. **Given** a producer returns channel metadata across multiple response parts, **When** the caller requests channel description, **Then** the client combines those parts into one complete channel-description result.
3. **Given** a producer rejects a requested URI for channel description, **When** the caller requests channel description, **Then** the client reports a protocol-level failure that is distinguishable from authentication and transport failures.

---

### User Story 2 - Start and Control Live Streaming (Priority: P2)

As a developer consuming real-time data, I want to start streaming one or more described channels, receive ongoing channel events, and stop selected streams without closing the session.

**Why this priority**: Once channels are known, the main value of Protocol 1 is receiving real-time data and handling lifecycle changes while the session remains active.

**Independent Test**: Can be fully tested by describing channels, starting a live stream, observing ordered channel events, issuing a stop request, and verifying that the affected channels stop streaming while the session remains usable.

**Acceptance Scenarios**:

1. **Given** described channels and valid streaming parameters, **When** the caller starts live streaming, **Then** the client receives live channel data for the requested channels in the order defined for those channels.
2. **Given** an active live stream with change notifications requested, **When** the producer sends change, status, or removal events, **Then** the client surfaces those events as part of the stream lifecycle for the affected channels.
3. **Given** an active live stream, **When** the caller stops one or more channels, **Then** the client discontinues those live streams without forcing the underlying ETP session to close.
4. **Given** a producer operates as a SimpleStreamer, **When** the caller starts Protocol 1 streaming, **Then** the client can receive the metadata and live data that the producer sends without requiring full describe or channel-control exchanges.

---

### User Story 3 - Request Historical Channel Ranges (Priority: P3)

As a developer analyzing prior measurements, I want to request data over a specific index range for one or more channels so I can retrieve bounded historical results in addition to live streaming.

**Why this priority**: Range retrieval is a standard consumer-side Protocol 1 capability and is necessary when the caller needs historical context instead of only forward-moving live updates.

**Independent Test**: Can be fully tested by issuing a range request for described channels and verifying that the client returns the correlated channel data in index order, including multipart responses.

**Acceptance Scenarios**:

1. **Given** described channels and a valid index range, **When** the caller requests historical channel data, **Then** the client returns the resulting channel data correlated to that specific range request.
2. **Given** a producer returns range data in multiple response parts, **When** the caller requests historical channel data, **Then** the client combines those parts into one logical range result.
3. **Given** a session reconnect occurs before a multipart range response is complete, **When** the caller inspects the incomplete result, **Then** the client does not treat that prior range request as complete and allows the caller to reissue it.

### Edge Cases

- The caller describes a URI that the producer does not support for Protocol 1 channel description.
- The producer returns channel metadata in multiple parts, and the final part has not yet arrived.
- Live channel data arrives for more than one channel in the same stream while maintaining channel-specific ordering rules.
- The producer sends change, status, or removal events after streaming has started.
- The caller stops a subset of active channels while leaving other channels streaming.
- A reconnect occurs after metadata has been partially received or while a multipart range response is still in progress.
- The producer identifies itself as a SimpleStreamer and does not require the full describe and stream-control flow.
- The producer rejects a range request because the requested indexes are invalid or not in channel index order.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST implement the consumer-side behaviors of ETP Protocol 1 needed to describe channels, start streaming, stop streaming, and request channel ranges after session establishment.
- **FR-002**: The feature MUST identify the governing ETP v1.1 and WITSML guidance clauses for each supported Protocol 1 behavior so tests and behavior remain traceable to the specification.
- **FR-003**: The client MUST allow a caller to request channel descriptions for one or more ETP resource URIs and receive a caller-usable description result for the channels the producer exposes.
- **FR-004**: The client MUST preserve channel-definition details needed for downstream use, including each channel's identifier, descriptive metadata, data interpretation metadata, index characteristics, and current status information when provided by the producer.
- **FR-005**: The client MUST aggregate multipart channel-description responses into one logical description result before reporting that description as complete.
- **FR-006**: The client MUST allow a caller to start live streaming for one or more channels and receive live channel data in the index order required for those channels.
- **FR-007**: The client MUST surface producer-sent channel lifecycle events, including data changes, status changes, and channel removals, in a way that callers can associate with the affected channels.
- **FR-008**: The client MUST allow a caller to stop streaming for one or more channels without requiring the entire ETP session to close.
- **FR-009**: The client MUST allow a caller to request channel data for a bounded index range and return the resulting data as a logical result correlated to that specific range request.
- **FR-010**: The client MUST aggregate multipart range responses into one logical result and preserve the protocol rule that an incomplete multipart range response is not considered complete after a reconnect.
- **FR-011**: The client MUST support the SimpleStreamer interaction model by allowing a caller to start Protocol 1 streaming and consume producer-sent metadata and data when the producer uses that profile.
- **FR-012**: The client MUST surface secret-safe, actionable outcomes for Protocol 1 rejections, unsupported URIs, invalid range requests, and unexpected message sequences so callers can distinguish protocol failures from authentication or transport failures.
- **FR-013**: The feature MUST include automated coverage for channel description, live streaming, stop behavior, lifecycle-event handling, range retrieval, multipart responses, reconnect-sensitive cases, and SimpleStreamer compatibility.

### Key Entities *(include if feature involves data)*

- **Channel Description Request**: A consumer request that identifies one or more ETP resource URIs for which the producer should return streamable channel definitions.
- **Channel Definition**: The producer-provided description of a channel, including its identifier, descriptive fields, index information, data interpretation details, and status-related metadata.
- **Streaming Subscription**: The caller's active request to receive live events for one or more channels, including start conditions, active channels, and stop behavior.
- **Channel Range Result**: The complete logical result of a bounded historical-data request, including the initiating request context and all correlated channel data returned for that range.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can describe at least one valid Protocol 1 resource and obtain stream-ready channel definitions without manually decoding raw protocol messages.
- **SC-002**: In acceptance testing, 100% of covered live-stream scenarios deliver ordered channel data for the requested channels and allow selected channels to stop without closing the session.
- **SC-003**: In acceptance testing, 100% of covered lifecycle-event scenarios correctly expose producer-sent change, status, and removal events for the affected channels.
- **SC-004**: In acceptance testing, 100% of covered range-request scenarios return data correlated to the initiating request, including multipart responses and reconnect-sensitive incomplete-response handling.
- **SC-005**: In acceptance testing, 100% of covered protocol-level failures are reported as secret-safe outcomes that are distinguishable from authentication and transport failures.
- **SC-006**: A consumer can interoperate with both full Protocol 1 producers and covered SimpleStreamer scenarios without changing public feature intent or requiring undocumented manual steps.

## Assumptions

- Scope is limited to the client acting in the Protocol 1 consumer role against an ETP server acting as producer; producer-side server behavior is out of scope.
- The authoritative references are `docs/ETP_Specification_v1.1_Doc_v1.1.md` and `docs/ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md`.
- Discovery or other resource-navigation steps may be used to find candidate URIs before Protocol 1 operations begin, but this feature starts once the caller has an active session and at least one candidate resource URI or SimpleStreamer session.
- Scope covers Protocol 1 ChannelStreaming behavior only; separate Protocol 2 ChannelDataFrame optimization behavior is out of scope for this feature.
- Automatic reconnect policy is not defined by this feature, but if a reconnect occurs the client must preserve the protocol semantics for incomplete metadata and range responses.
