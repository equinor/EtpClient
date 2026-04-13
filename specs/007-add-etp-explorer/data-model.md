# Data Model: Add ETP Explorer

## ExplorerOptions

Represents validated startup configuration loaded from the `EtpExplorer` configuration section.

**Fields**
- `EndpointUri` (`string`, required): absolute `ws` or `wss` URI for the ETP server.
- `Username` (`string`, required): Basic authentication username.
- `Password` (`string`, required): Basic authentication password.
- `MessageEncoding` (`EtpMessageEncoding`, optional, default `Binary`): connection encoding for the explorer session.
- `ProtocolRequestTimeoutSeconds` (`int`, optional, default > 0): timeout for describe/discovery requests that are expected to complete promptly.

**Validation Rules**
- `EndpointUri` must be present, absolute, and use `ws` or `wss`.
- `Username` and `Password` must be present but must never be echoed in output.
- `ProtocolRequestTimeoutSeconds` must be greater than 0.

## ExplorerSessionState

Tracks the user-visible interactive state for one explorer run.

**Fields**
- `ConnectionState` (`Disconnected|Connected|Streaming|Closing`): high-level explorer lifecycle state.
- `AvailableRootNodes` (`IReadOnlyList<RootNodeOption>`): top-level root nodes discovered immediately after connection.
- `SelectedRootNode` (`RootNodeOption?`): the currently selected root node that anchors tree navigation.
- `CurrentUri` (`string`): URI currently being browsed.
- `NavigationStack` (`IReadOnlyList<string>`): parent-to-child traversal history for back navigation.
- `LastDiscoveryResult` (`IReadOnlyList<BrowseableResource>`): resources currently shown to the user.
- `SelectionSet` (`IReadOnlyList<SelectedEndpoint>`): endpoints selected for the next stream session.
- `ActiveStreamChannels` (`IReadOnlyList<long>`): session-scoped channel IDs currently being streamed.
- `LastStatusMessage` (`string?`): latest non-secret status or warning shown to the user.

**State Transitions**
- `Disconnected -> Connected` after successful configuration validation and session connect.
- `Connected -> Connected` after root-node discovery and explicit root-node selection establish the initial tree context.
- `Connected -> Streaming` after explicit user start with a non-empty selection set.
- `Streaming -> Connected` after explicit stop, cancellation, or clean end-of-stream handling.
- `Connected|Streaming -> Closing -> Disconnected` during application shutdown.

## RootNodeOption

Represents one selectable top-level content branch discovered immediately after connection.

**Fields**
- `Name` (`string`, required): user-facing root node name such as `witsml14` or `witsml20`.
- `Uri` (`string`, required): top-level URI or URI fragment that becomes the root of subsequent tree navigation.
- `Description` (`string?`): optional user-facing explanation of the branch.

**Validation Rules**
- Root node names must be unique within one connected explorer session.
- The explorer must not enter deeper browse mode until one `RootNodeOption` has been selected.

## BrowseableResource

Represents one item in the interactive discovery browser.

**Fields**
- `Uri` (`string`, required): discovered ETP URI.
- `Name` (`string`, required): display name for the menu.
- `ResourceType` (`string`, required): discovery resource type for visual labeling.
- `ContentType` (`string`, required): server-reported content type.
- `HasChildren` (`bool|unknown`): whether deeper traversal is available.
- `ChannelSubscribable` (`bool`): whether the resource indicates potential streamability.
- `Depth` (`int`): current browse depth for display and navigation.
- `ParentUri` (`string?`): parent node URI used to preserve tree relationships and back navigation.

**Relationships**
- Many `BrowseableResource` records can be shown in one `ExplorerSessionState`, all under one `SelectedRootNode` at a time.
- A `BrowseableResource` may resolve to zero or more `ResolvedStreamableEndpoint` records after channel description.

## ResolvedStreamableEndpoint

Represents a channel definition that the explorer can add to the selection set.

**Fields**
- `SourceResourceUri` (`string`, required): browse URI that produced the describe request.
- `ChannelId` (`long`, required): session-scoped channel identifier used for live streaming.
- `ChannelUri` (`string`, required): channel URI returned by `ChannelDescribe`.
- `ChannelName` (`string`, required): user-facing channel label.
- `DataType` (`string`, required): value type label.
- `IndexType` (`string`, required): primary index kind for display context.
- `Status` (`string`, required): channel status used to exclude closed/non-streamable channels.

**Validation Rules**
- Only endpoints with valid channel IDs and non-closed status are eligible for selection.
- `SourceResourceUri` must be preserved so output can be traced back to the browse context.

## SelectedEndpoint

Represents one user-selected stream target kept in the pending selection set.

**Fields**
- `SelectionKey` (`string`, required): stable UI key derived from source URI and channel URI.
- `Endpoint` (`ResolvedStreamableEndpoint`, required): resolved streamable endpoint metadata.
- `SelectedAtUtc` (`DateTimeOffset`, required): timestamp used for deterministic ordering and auditability in tests.

**Validation Rules**
- `SelectionKey` must be unique within a selection set.
- Duplicate selection attempts should be idempotent.

## RenderedStreamEvent

Represents one live row or panel entry shown while streaming is active.

**Fields**
- `ChannelId` (`long`, required): streamed channel identifier.
- `ChannelName` (`string`, required): display name for the originating endpoint.
- `SourceResourceUri` (`string`, required): browse context or channel URI shown to the user.
- `PrimaryIndexText` (`string`, required): formatted index text prepared for display.
- `ValueText` (`string`, required): formatted value text prepared for display.
- `EventKind` (`ChannelEventKind`, required): data, change, remove, or status indicator.
- `ObservedAtUtc` (`DateTimeOffset`, required): time the explorer rendered the event.

**Relationships**
- Many `RenderedStreamEvent` records can be emitted during one `ExplorerSessionState` in `Streaming` state.
