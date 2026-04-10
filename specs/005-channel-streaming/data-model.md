# Data Model: Channel Streaming Support

## ChannelDescribeRequest

- **Purpose**: Represents a caller request to describe one or more Protocol 1 target URIs.
- **Key fields**:
  - `Uris`: one or more ETP URIs to describe.
  - `MessageEncoding`: the selected session encoding used for the request.
  - `CorrelationMessageId`: the message identifier used to match the returned metadata.
- **Validation rules**:
  - At least one URI is required.
  - Each URI must be non-empty.
  - Request is valid only while the session is connected.

## ChannelDefinition

- **Purpose**: Public representation of one channel described by a Protocol 1 producer.
- **Key fields**:
  - `ChannelId`
  - `ChannelUri`
  - `ChannelName`
  - `DataType`
  - `Status`
  - `Uom`
  - `IndexType`
  - `IndexDirection`
  - `StartIndex`
  - `EndIndex`
  - `Metadata`
- **Validation rules**:
  - `ChannelId` and `ChannelUri` are required once returned by the producer.
  - Index metadata must preserve the producer-provided primary-index semantics.
  - Unknown metadata fields remain pass-through for compatibility.
- **Relationships**:
  - Belongs to one `ChannelDescriptionResult`.
  - Can be referenced by a `StreamingSubscriptionRequest` or `ChannelRangeRequestModel`.

## ChannelDescriptionResult

- **Purpose**: The complete logical result of one describe operation.
- **Key fields**:
  - `RequestedUris`
  - `Channels`: ordered list of `ChannelDefinition`
  - `MessageEncoding`
  - `WasMultipart`
- **Validation rules**:
  - `RequestedUris` is required.
  - Channel ordering is preserved in producer response order.
- **State transitions**:
  - `Pending` → `Completed`
  - `Pending` → `Failed`

## StreamingSubscriptionRequest

- **Purpose**: Represents the caller’s intent to start live streaming for one or more channels.
- **Key fields**:
  - `Channels`: targeted channel identifiers or URIs.
  - `StartIndex`: optional starting point per channel.
  - `ReceiveChangeNotifications`: whether change/status notifications are requested.
  - `IncludeStatusChanges`: whether status-change handling is enabled when supported.
  - `CorrelationMessageId`
- **Validation rules**:
  - At least one target channel is required.
  - Requested start indexes must respect the primary-index semantics of the described channels.

## StreamingSubscription

- **Purpose**: Represents an active live Protocol 1 stream lifecycle.
- **Key fields**:
  - `ActiveChannels`
  - `State`
  - `StartedAt`
  - `ReceiveChangeNotifications`
  - `MessageEncoding`
- **Validation rules**:
  - Exists only while the underlying session is connected and the live stream has not been fully stopped.
- **State transitions**:
  - `PendingStart` → `Active`
  - `Active` → `Stopping`
  - `Stopping` → `Stopped`
  - `Active` → `Faulted`

## ChannelEvent

- **Purpose**: Public representation of one producer-sent live event.
- **Variants**:
  - `ChannelDataEvent`
  - `ChannelDataChangeEvent`
  - `ChannelStatusChangeEvent`
  - `ChannelRemoveEvent`
- **Common fields**:
  - `ChannelId`
  - `ChannelUri`
  - `OccurredAt`
  - `CorrelationMessageId`
  - `EventKind`
- **Validation rules**:
  - Events must preserve producer ordering as received per channel.
  - `ChannelRemoveEvent` closes future live-data eligibility for that channel until the caller restarts it.

## ChannelRangeRequestModel

- **Purpose**: Represents a bounded historical-data request for one or more channels.
- **Key fields**:
  - `Channels`
  - `FromIndex`
  - `ToIndex`
  - `CorrelationMessageId`
  - `MessageEncoding`
- **Validation rules**:
  - `FromIndex` and `ToIndex` must be in channel index order.
  - Indexes reference the primary index for the channel.

## ChannelRangeResult

- **Purpose**: The complete logical result of one bounded historical-data request.
- **Key fields**:
  - `Request`
  - `Samples`: ordered channel data values correlated to the initiating request.
  - `WasMultipart`
  - `Completed`
- **Validation rules**:
  - Samples remain correlated to the originating request.
  - An incomplete multipart result is not treated as complete after a reconnect.
- **State transitions**:
  - `Pending` → `Completed`
  - `Pending` → `IncompleteAfterReconnect`
  - `Pending` → `Failed`

## ChannelStreamingFailure

- **Purpose**: Represents a secret-safe Protocol 1 failure surfaced to callers.
- **Key fields**:
  - `FailureCategory`
  - `RequestedUrisOrChannels`
  - `EtpErrorCode`
  - `SecretSafeMessage`
  - `CorrelationMessageId`
- **Validation rules**:
  - Must not include credentials or authorization values.
  - Must preserve enough context for callers to distinguish describe rejection, start/stop errors, invalid range requests, unexpected message sequences, and pre-existing transport or auth failures.
