# Feature Specification: Stream Channel Range

**Feature Branch**: `012-stream-channel-range`  
**Created**: 2026-05-19  
**Status**: Draft  

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Iterate channel range data as it arrives (Priority: P1)

A library consumer wants to process historical channel data for a bounded index range without waiting for every response frame to arrive. Today `RequestChannelRangeAsync` blocks until the server sends the final-part `ChannelData` message and then returns a single aggregated `ChannelRangeResult`. For large ranges the server may send dozens of frames over several seconds; the consumer cannot act on early frames until all frames are complete. After this change the method returns `IAsyncEnumerable<ChannelDataItem>`, and each item is yielded to the caller as soon as the frame it belongs to is decoded, frame by frame.

**Why this priority**: Removes the main practical obstacle that caused the live integration test to fail with a network timeout — the server may drop the connection mid-stream, and with streaming the caller has already consumed the data it received instead of losing everything.

**Independent Test**: Connect to a live or in-process stub server, call `RequestChannelRangeAsync`, and `await foreach` over the result. Verify that items are received progressively and that the enumeration ends when the server sends the final-part flag.

**Acceptance Scenarios**:

1. **Given** a connected session and a valid `ChannelRangeRequestModel`, **When** `RequestChannelRangeAsync` is called and iterated with `await foreach`, **Then** each `ChannelDataItem` yielded matches what the server sent in the corresponding `ChannelData` frame.
2. **Given** a server that sends three `ChannelData` frames before the final-part frame, **When** the consumer iterates, **Then** items from all three frames are yielded in arrival order and the enumeration completes after the final-part frame.
3. **Given** a server that sends an empty final-part `ChannelData` frame (no data in range), **When** the consumer iterates, **Then** the enumeration completes immediately without yielding any items.
4. **Given** the caller passes a `CancellationToken` that is cancelled mid-iteration, **When** cancellation fires, **Then** the enumeration stops without throwing (consistent with the existing `StartChannelStreamingAsync` pattern).

---

### User Story 2 — Error propagation during streaming (Priority: P2)

When the server sends a `ProtocolException` in response to the range request, or when an unexpected message type is received, the consumer should receive a typed `EtpChannelStreamingException` — just as with the current `Task<ChannelRangeResult>` implementation.

**Why this priority**: Correct error behaviour is required for production use but can be validated independently of the streaming happy path.

**Independent Test**: Configure a stub server to respond with a `ProtocolException` after the range request message. Assert that iterating the `IAsyncEnumerable` throws `EtpChannelStreamingException`.

**Acceptance Scenarios**:

1. **Given** a server that responds with a `ProtocolException`, **When** the consumer iterates, **Then** `EtpChannelStreamingException` is thrown with the ETP error code set.
2. **Given** a server that responds with an unexpected message type correlated to the request, **When** the consumer iterates, **Then** `EtpChannelStreamingException` is thrown.
3. **Given** a WebSocket disconnection mid-stream, **When** the consumer iterates, **Then** the underlying `WebSocketException` propagates (not silently swallowed).

---

### Edge Cases

- Frames with a `CorrelationId` that does not match the request message ID must be skipped silently (same as current behaviour).
- Cancellation during the initial `ChannelStreamingStart` send must propagate as `OperationCanceledException`.
- Callers that never iterate the returned `IAsyncEnumerable` must not cause any send to the server or hang; the send only happens once iteration begins.
- The `[EnumeratorCancellation]` attribute must be applied to the `CancellationToken` parameter, consistent with `StartChannelStreamingAsync`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `RequestChannelRangeAsync` on `IEtpClient`, `EtpClient`, and `EtpSessionManager` MUST return `IAsyncEnumerable<ChannelDataItem>` instead of `Task<ChannelRangeResult>`.
- **FR-002**: Each `ChannelDataItem` in a received `ChannelData` frame MUST be yielded to the caller before the next frame is awaited.
- **FR-003**: The enumeration MUST complete normally (without throwing) when the server sends a `ChannelData` frame with the `FinalPart` flag set.
- **FR-004**: Cancellation via the `CancellationToken` parameter is handled in two sub-cases: (a) mid-iteration cancellation (token cancelled after enumeration begins) MUST stop enumeration cleanly via `yield break`, without throwing; (b) a token that is already cancelled when the first `MoveNextAsync()` is called MUST propagate `OperationCanceledException` naturally from the first internal `await`, without sending any frame to the server.
- **FR-005**: `ProtocolException` responses correlated to the request MUST cause `EtpChannelStreamingException` to be thrown from the enumerator.
- **FR-006**: Unexpected message types correlated to the request MUST cause `EtpChannelStreamingException` to be thrown from the enumerator.
- **FR-007**: Frames whose `CorrelationId` does not match the request message ID MUST be skipped.
- **FR-008**: OpenTelemetry instrumentation (activity, duration metric, error recording) MUST be preserved. The activity spans the lifetime of the enumeration from first item request to final-part receipt or exception. If the caller disposes the enumerator before the final-part frame arrives (early break), the span MUST be closed with status OK — early abandonment is not a fault.
- **FR-009**: `EtpClientLog.RangeRequestStarted`, `RangeRequestCompleted`, and `RangeRequestFailed` structured log events MUST be preserved with equivalent semantics. Because sample count is only known at enumeration end, `RangeRequestCompleted` is emitted after the final-part frame.
- **FR-010**: The `ChannelRangeResult` class, `ChannelRangeResultState` enum, and `WasMultipart` concept MUST be removed from the public API, as they are rendered redundant by the streaming model.
- **FR-011**: `ChannelRangeRequestModel` MUST be retained unchanged as the request parameter type.
- **FR-012**: The live integration test `LiveRequestChannelRangeAsyncTests` MUST be updated to use `await foreach` and assert over the yielded items.

### Key Entities

- **`ChannelRangeRequestModel`**: Unchanged. Identifies the channels and index bounds for the request.
- **`ChannelDataItem`**: Unchanged. Represents one data point returned from the server.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing unit and integration tests that cover Protocol 1 channel range behaviour pass after the change.
- **SC-002**: The live integration test iterates and prints yielded items progressively; the test no longer fails with a WebSocket timeout on partial data.
- **SC-003**: No public API surface refers to `ChannelRangeResult` or `ChannelRangeResultState` after the change.
- **SC-004**: Calling `await foreach` on the result with a pre-cancelled token throws `OperationCanceledException` immediately, without sending a frame to the server.
- **SC-005**: OpenTelemetry duration metrics and structured log events are emitted at the same lifecycle points as before (request start, completion, failure).

## Assumptions

- The method signature change is a breaking API change; no backward-compatibility shim is provided.
- `ChannelRangeResult`, `ChannelRangeResultState`, and `WasMultipart` are not referenced by any consumer outside this repository.
- The ETP server always sends `ChannelData` frames with a `CorrelationId` matching the request message ID; frames without a matching `CorrelationId` may arrive (e.g., live stream data from an earlier subscription) and are filtered silently.
- The `FinalPart` flag on a `ChannelData` frame is the sole signal that the range response is complete; no separate `Acknowledge` message is used for range requests.
- OTel instrumentation wraps the entire enumeration, not individual frames; a `finally` block at the end of the iterator method handles cleanup.

## Clarifications

### Session 2026-05-19

- Q: Should iterating with an already-cancelled token silently yield break (no exception) or throw OperationCanceledException? → A: Throw OperationCanceledException — propagates naturally from the first internal await on the cancelled token, consistent with StartChannelStreamingAsync.
- Q: When the caller breaks out of await foreach before the FinalPart frame arrives, should the OTel span status be OK or Error? → A: OK — early abandonment is a valid caller decision, not a fault; only EtpChannelStreamingException and unexpected exceptions record Error status.
