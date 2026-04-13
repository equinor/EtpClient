# Feature Specification: Add ETP Explorer

**Feature Branch**: `007-add-etp-explorer`  
**Created**: 2026-04-12  
**Status**: Draft  
**Input**: User description: "I want a secondary console application to be created (f.ex EtpExplorer). This console application should provide an interactive way to browse an ETP server, select one or more streamable endpoints, start streaming and then render the streamed output"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Select a Root Node and Browse Its Tree (Priority: P1)

As a developer connecting to an ETP server, I want the explorer to prompt me to choose one of the available root nodes and then browse the selected branch as a tree so I can stay within the correct WITSML version context while discovering what can be streamed.

**Why this priority**: Browsing and discovery are the entry point for every other workflow. Without a usable exploration flow, the application does not provide value beyond the existing sample console.

**Independent Test**: Can be fully tested by connecting the explorer to a server that exposes multiple root nodes, verifying that the user is first prompted to choose one root node such as `witsml14` or `witsml20`, and then navigating the selected branch as a tree without using raw protocol payloads or hard-coded channel identifiers.

**Acceptance Scenarios**:

1. **Given** a reachable ETP server with multiple discovered root nodes, **When** the user opens the explorer after connecting, **Then** the application prompts the user to choose one available root node before deeper browsing begins.
2. **Given** available root nodes such as `witsml14` and `witsml20`, **When** the user selects one root node, **Then** the application scopes subsequent browsing to that selected root node.
3. **Given** a selected root node, **When** the user opens a resource that exposes child resources or channels, **Then** the application shows the next level of available content as part of the same tree without losing the user's navigation context.
4. **Given** a resource that does not expose streamable content, **When** the user navigates to it within the selected tree, **Then** the application clearly indicates that no streamable endpoints are available from that selection.
5. **Given** a selected root node and an active browse context, **When** the user returns to the root-node selection and chooses a different root node, **Then** the application resets the browsing context to the newly selected root without requiring a restart.

---

### User Story 2 - Select Streamable Endpoints (Priority: P2)

As a developer exploring an ETP server, I want to select one or more streamable endpoints from the interactive browser so I can prepare a focused live stream session from the content I care about.

**Why this priority**: The ability to choose stream targets is the core differentiator between passive browsing and a useful interactive explorer.

**Independent Test**: Can be fully tested by navigating to streamable content, selecting one or multiple endpoints, and verifying that the application keeps an accurate selection set before streaming begins.

**Acceptance Scenarios**:

1. **Given** a list of streamable endpoints, **When** the user selects one endpoint, **Then** the application records that endpoint for streaming.
2. **Given** a list of streamable endpoints, **When** the user selects multiple endpoints, **Then** the application preserves all selected endpoints in the pending stream set.
3. **Given** one or more previously selected endpoints, **When** the user deselects an endpoint, **Then** the pending stream set updates immediately and accurately.

---

### User Story 3 - Start and Observe Streaming Output (Priority: P3)

As a developer validating live ETP data, I want to start streaming from my selected endpoints and see rendered output in the interactive console so I can monitor incoming measurements in real time.

**Why this priority**: Streaming is the main payoff after browsing and selection, but it depends on those earlier flows being in place.

**Independent Test**: Can be fully tested by selecting streamable endpoints, starting a stream session, and verifying that incoming events are rendered clearly and remain attributable to their source endpoints.

**Acceptance Scenarios**:

1. **Given** one or more selected streamable endpoints, **When** the user starts streaming, **Then** the application opens a live stream session for the selected endpoints.
2. **Given** live events are received from multiple selected endpoints, **When** the application renders those events, **Then** each rendered entry identifies its source endpoint clearly enough for the user to distinguish concurrent streams.
3. **Given** the live stream is active, **When** the user stops or exits the stream session, **Then** the application ends streaming cleanly and returns the user to an interactive state or exits without leaving the session ambiguous.

### Edge Cases

- The server connection succeeds, but discovery returns no root nodes or no streamable content.
- The server exposes multiple root nodes, but one advertised root node returns no deeper content after selection.
- The user wants to switch from one root node, such as `witsml14`, to another root node, such as `witsml20`, without restarting the application.
- The server exposes resources that can be browsed but not streamed.
- The user starts the application without selecting any endpoints.
- One selected endpoint starts streaming while another fails or becomes unavailable.
- The stream ends because of cancellation, server disconnect, or endpoint removal while the user is still in the interactive session.
- The number of available resources or endpoints is large enough that the interactive flow must remain understandable and navigable.

## Protocol Compliance

Relevant ETP clauses and message families for this feature:

- Discovery behavior follows the ETP v1.1 Discovery interfaces and sequences, including `GetResources`, `GetResourcesResponse`, and empty-result acknowledgement handling.
- ChannelStreaming behavior follows the ETP v1.1 consumer-side describe and streaming semantics, including `ChannelDescribe`, `ChannelMetadata`, `ChannelStreamingStart`, `ChannelStreamingStop`, `ChannelData`, `ChannelDataChange`, and `ChannelRemove`.
- Message header correlation and multipart semantics are used through the existing client library behavior and are not redefined by this feature.

Endpoint interaction implemented by this feature:

- The explorer connects through the existing `EtpClient` session workflow.
- The explorer discovers available root nodes and browseable resources through discovery operations.
- The explorer resolves streamable endpoints through channel description workflows before any live subscription begins.
- The explorer starts and stops live subscriptions only through the existing ChannelStreaming APIs in the client library.

Authentication assumptions:

- The explorer uses the same explicit connection inputs as the existing sample workflows.
- Endpoint URI, username, and password are loaded through .NET configuration and user secrets for local development.
- The feature does not permit credentials to be entered into checked-in files, echoed in console output, or included in logs, exceptions, or tests.

Expected subscription lifecycle:

- The user connects first, then selects a root node, then browses to a streamable endpoint, then explicitly starts streaming.
- Streaming remains cancellation-aware and user-controlled.
- Stopping a stream returns the explorer to a clear interactive state or exits cleanly.
- Partial endpoint failures must be surfaced without hiding which endpoints remained active or failed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The solution MUST provide a second console application, separate from the existing sample console, dedicated to interactive ETP exploration and streaming.
- **FR-002**: The interactive console application MUST allow a user to connect to an ETP server using the connection inputs required by the existing library workflows.
- **FR-003**: After connecting, the application MUST discover and present the available root nodes and require the user to select one root node before deeper browsing begins.
- **FR-004**: After a root node is selected, the application MUST let the user browse discoverable ETP resources and related streamable content within that selected branch through an interactive tree-based console flow.
- **FR-005**: The application MUST clearly distinguish between selectable root nodes, browseable resources, streamable endpoints, and items that are not available for streaming.
- **FR-006**: The application MUST allow the user to return to the root-node selection and choose a different root node without restarting the application.
- **FR-007**: The application MUST allow the user to select one or more streamable endpoints before starting a stream.
- **FR-008**: The application MUST allow the user to review and modify the current selection set before streaming begins.
- **FR-009**: The application MUST start live streaming for the selected endpoints on explicit user action.
- **FR-010**: The application MUST render streamed output in a way that lets the user identify which selected endpoint produced each rendered event.
- **FR-011**: The application MUST provide a clear user path to stop streaming, return to browsing or selection, or exit the application cleanly.
- **FR-012**: The application MUST present understandable feedback when root-node discovery finds no choices, when deeper discovery finds no content, when no endpoints are selected, and when selected endpoints cannot be streamed.
- **FR-013**: The application MUST handle partial streaming failures so that users can understand which selected endpoints succeeded and which did not.
- **FR-014**: The feature MUST include automated coverage for root-node selection, tree navigation, endpoint selection, stream start behavior, rendered output attribution, and clean stream shutdown.

### Key Entities *(include if feature involves data)*

- **Explorer Session**: The user-visible interactive session that manages connection state, the available root nodes, the selected root node, current navigation location, selected endpoints, and stream state.
- **Root Node**: A top-level discovered content branch, such as `witsml14` or `witsml20`, that anchors the rest of the explorer's tree navigation.
- **Browseable Resource**: A discovered ETP resource that can appear in the interactive navigation flow and may expose child resources or streamable endpoints.
- **Streamable Endpoint**: A selectable target that the user can add to the pending stream set and use to start a live stream session.
- **Selection Set**: The current set of user-selected streamable endpoints that will be used when streaming starts.
- **Rendered Stream Event**: A user-visible live output entry associated with a specific selected endpoint during an active stream session.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, a user can move from application start to choosing one of the available root nodes and viewing the first level of that selected tree in under 2 minutes without consulting protocol payloads or source code.
- **SC-002**: In acceptance testing, 100% of covered single-endpoint and multi-endpoint selection scenarios preserve the correct selection set before streaming begins.
- **SC-003**: In acceptance testing, 100% of covered streaming scenarios render each received event with enough source context for the user to distinguish concurrent endpoint output.
- **SC-004**: In acceptance testing, 100% of covered no-content, no-selection, and partial-failure scenarios surface understandable user feedback rather than silent failure or ambiguous state.
- **SC-005**: In acceptance testing, a user can stop an active stream and return to an interactive state or exit cleanly in 100% of covered shutdown scenarios.

## Assumptions

- The new explorer will rely on capabilities already available in the existing ETP client library for connection, discovery, channel description, and channel streaming.
- The connected server may expose multiple top-level root nodes, such as `witsml14` and `witsml20`, and the explorer should treat them as mutually selectable starting points for deeper navigation.
- The initial scope is an interactive console experience for developers and operators rather than a graphical desktop application.
- Browsing may combine resource discovery and channel inspection as needed to help the user reach streamable endpoints through a coherent console workflow, but the first browse step after connect is explicit root-node selection followed by tree navigation within that branch.
- Rendering streamed output should be human-readable and attributable to the originating endpoint, but it does not need to include historical playback or data export in this feature.
- The feature will add the tests needed to verify the primary browse, selection, streaming, feedback, and shutdown flows.
