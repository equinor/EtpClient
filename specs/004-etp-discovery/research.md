# Research: ETP Discovery Traversal

## Decision 1: Implement Protocol 3 `GetResources` / `GetResourcesResponse` as the first Discovery slice

- **Decision**: Scope the feature to Protocol 3 Discovery using `GetResources` requests and `GetResourcesResponse` records, with `Acknowledge` handling for valid URIs that have no children.
- **Rationale**: The user goal is to traverse the ETP server to find available resources for streaming. The spec states that Discovery is the protocol used to enumerate child URIs, and that applications can use Discovery to determine what channels are available before using ChannelStreaming.
- **Alternatives considered**:
  - Jump directly to `ChannelDescribe`: rejected because it assumes the caller already knows traversable URIs.
  - Add Store or object retrieval in the same slice: rejected because the feature is about traversal and stream-target discovery, not CRUD access.

## Decision 2: Treat `Acknowledge` on Protocol 3 as an empty-result success case

- **Decision**: When a valid Discovery request URI has no children and the server returns `Acknowledge` with Protocol 3 and the request correlation ID, map that to an empty traversal result rather than an error.
- **Rationale**: The ETP spec explicitly requires `Acknowledge` for a valid URI with no children. Treating that as failure would misclassify a normal traversal outcome.
- **Alternatives considered**:
  - Treat any non-`GetResourcesResponse` message as failure: rejected because it would violate the Discovery protocol behavior.
  - Return `null` instead of an empty result: rejected because callers need a deterministic, enumerable response shape.

## Decision 3: Aggregate multipart `GetResourcesResponse` messages into one logical traversal result

- **Decision**: Continue reading responses until the final part flag is observed, collecting each `Resource` into a single logical traversal result for the caller.
- **Rationale**: `GetResourcesResponse` is explicitly multipart in the spec, and servers may split large enumerations across multiple messages.
- **Alternatives considered**:
  - Expose raw message streaming for Discovery responses: rejected for the first slice because callers want traversal results, not protocol-frame management.
  - Impose a single-message assumption: rejected because it would break on valid multipart responses.

## Decision 4: Expose discovery metadata through typed models rather than raw protocol records

- **Decision**: Create public discovery result models that surface the subset of `Resource` fields callers need for traversal and streaming-target selection: URI, content type, display name, resource type, `hasChildren`, `channelSubscribable`, `uuid`, and `objectNotifiable`.
- **Rationale**: The sample app and library consumers need a typed, stable API surface rather than manual inspection of Avro or JSON protocol payloads.
- **Alternatives considered**:
  - Return raw JSON or raw Avro-decoded dictionaries: rejected because it would leak transport details into the public API.
  - Return only URIs: rejected because callers also need traversal and streaming cues from the metadata.

## Decision 5: Update the default requested-protocol profile to include Discovery alongside streaming-oriented usage

- **Decision**: The discovery-capable connection flow will advertise Discovery (Protocol 3, customer role) in addition to the current streaming-oriented protocol request shape so the session can legally issue Discovery requests after connection.
- **Rationale**: The server already rejected unsupported protocol sets during earlier handshake work. Discovery must be included in `requestedProtocols` to make post-session traversal valid.
- **Alternatives considered**:
  - Require callers to always provide custom requested protocols manually: rejected because the sample and default developer experience should support the main traversal scenario directly.
  - Replace the streaming-oriented default entirely with Discovery-only: rejected because the user explicitly wants discovery in service of later streaming.

## Decision 6: Use the sample app to resolve and print top-level URIs only

- **Decision**: For this feature, the sample app will connect and resolve `eml://`, then print the returned top-level resources and stream-relevant metadata.
- **Rationale**: That gives a concrete, demoable workflow aligned with the feature request while keeping the sample focused.
- **Alternatives considered**:
  - Implement recursive traversal in the sample immediately: rejected because the feature request only requires resolving top-level URIs for now.
  - Omit sample updates: rejected because the user explicitly asked for the sample application to resolve the top-level URIs.

---

## Implementation Notes (post-implementation verification)

### Protocol 3 negotiation
- Protocol 3 (`Discovery`) is advertised in `EtpConnectionOptions.RequestedProtocols` with role `"customer"` by default.
- This ensures any connection attempt via `EtpClient.ConnectAsync` is discovery-capable without requiring explicit caller configuration.

### Message ID sequencing
- `RequestSession` (the handshake message) uses `messageId = 1` statically.
- All post-handshake messages including `GetResources` use `Interlocked.Increment(ref _nextMessageId)` starting at 2.

### Multipart `GetResourcesResponse` aggregation
- The `FinalPart` flag (`0x02`) is set in the message header's `messageFlags` field.
- Single-resource responses also set `FinalPart`; the receive loop handles both single-part and multipart identically by collecting resources until `FinalPart` is seen.

### `Acknowledge` mapping
- `Acknowledge` (Protocol 3, messageType 1001) with the correlation ID matching the sent `GetResources` messageId is mapped to an empty `DiscoveryResult` with `WasEmptyAcknowledged = true`.
- `DiscoveryResult.State` auto-computes as `CompletedEmpty` when `WasEmptyAcknowledged` is `true` and `Resources` is empty.

### `ProtocolException` mapping
- On receiving messageType 1000 (Protocol Exception), `EtpSessionManager` decodes the error code and message, then throws `EtpDiscoveryException` with `RequestedUri` and optional `EtpErrorCode?`.
- Secret safety: the exception message is the server-supplied error string, not derived from the URI or session credentials.

### Resource Avro field order (binary codec)
Exact field order required when encoding/decoding `Resource` records in binary mode:
1. `uri` (string)
2. `contentType` (string)
3. `name` (string)
4. `channelSubscribable` (bool)
5. `customData` (map of string→bytes)
6. `resourceType` (string)
7. `hasChildren` (int — not bool; -1 = unknown, 0 = none, >0 = count)
8. `uuid` (union[null, string] — discriminator long: 0 = null, 1 = string value)
9. `lastChanged` (long, Unix ms timestamp)
10. `objectNotifiable` (bool)

### `EtpClientLog` discovery event IDs
- 1007: `DiscoverResourcesStarted`
- 1008: `DiscoverResourcesComplete`
- 1009: `DiscoverResourcesEmpty`
- 1010: `DiscoverResourcesFailed`
