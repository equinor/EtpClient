# Data Model: ETP Discovery Traversal

## DiscoveryRequest

- **Purpose**: Represents a caller request to enumerate child resources for one URI.
- **Key fields**:
  - `Uri`: the traversal target, such as `eml://`, `eml://witsml20`, or a deeper object/folder URI.
  - `MessageEncoding`: the selected session encoding used for the request.
  - `CorrelationMessageId`: the message identifier used to match responses.
- **Validation rules**:
  - URI must be non-empty.
  - Root traversal must accept `eml://`.
  - Request is only valid while the session is connected.

## DiscoveredResource

- **Purpose**: Public representation of one `Resource` record returned by Discovery.
- **Key fields**:
  - `Uri`
  - `ContentType`
  - `Name`
  - `ResourceType`
  - `HasChildren`
  - `ChannelSubscribable`
  - `Uuid`
  - `ObjectNotifiable`
  - `CustomData`
- **Validation rules**:
  - `Uri` is required.
  - `ContentType` may be required by the server for object/folder nodes.
  - `ResourceType` should be one of the spec-recognized categories such as `UriProtocol`, `Folder`, or `DataObject`, but unknown values must remain pass-through for compatibility.
- **Relationships**:
  - Belongs to one `DiscoveryResult`.
  - May be used as the input URI for a subsequent `DiscoveryRequest` when `HasChildren` indicates further traversal.
  - May be used by later streaming-oriented workflows when `ChannelSubscribable` is true.

## DiscoveryResult

- **Purpose**: The logical result of one Discovery traversal step.
- **Key fields**:
  - `RequestedUri`
  - `Resources` (ordered list of `DiscoveredResource`)
  - `WasEmptyAcknowledged` (true when the server responded with `Acknowledge` for a valid URI with no children)
  - `MessageEncoding`
- **Validation rules**:
  - `RequestedUri` is required.
  - `Resources` may be empty only when the server returned an empty-child acknowledgement or no child resources.
- **State transitions**:
  - `Pending` → `CompletedWithResources`
  - `Pending` → `CompletedEmpty`
  - `Pending` → `Failed`

## DiscoveryFailure

- **Purpose**: Represents a discovery-specific failure outcome surfaced to callers.
- **Key fields**:
  - `RequestedUri`
  - `FailureCategory`
  - `EtpErrorCode`
  - `SecretSafeMessage`
  - `MessageEncoding`
- **Validation rules**:
  - Must not include credentials or authorization values.
  - Must preserve enough context for callers to distinguish invalid URI, unsupported protocol, resource-limit rejection, and transport/auth/session errors.
