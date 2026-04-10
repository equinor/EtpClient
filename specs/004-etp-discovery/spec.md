# Feature Specification: ETP Discovery Traversal

**Feature Branch**: `004-add-etp-discovery`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "I want to add discovery from the ETP specification so that I can traverse the ETP server to find available resources for streaming"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enumerate Discovery Roots (Priority: P1)

As a developer connecting to an ETP server, I want to discover the top-level resource roots the server exposes so I can begin navigating the resource hierarchy instead of guessing URIs manually.

**Why this priority**: Without a reliable starting point for discovery, the client user cannot traverse the server structure or identify which data models and dataspaces are available.

**Independent Test**: Can be fully tested by connecting to an ETP endpoint, requesting discovery from `eml://`, and verifying that the client returns the resources the server exposes at the root.

**Acceptance Scenarios**:

1. **Given** an authenticated session with a server that supports Discovery, **When** the client requests resources for `eml://`, **Then** the client returns the top-level resources the server exposes for traversal.
2. **Given** a server that exposes dataspaces or multiple supported data-model roots, **When** the client requests resources for `eml://`, **Then** the client returns those roots in a form the caller can traverse further.

---

### User Story 2 - Traverse Child Resources (Priority: P2)

As a developer exploring an ETP server, I want to request child resources for any discovered URI so I can walk the hierarchy from protocol roots to concrete objects relevant for streaming.

**Why this priority**: Root discovery is only useful if the caller can continue traversing folders and object hierarchies to find the actual resources exposed by the server.

**Independent Test**: Can be fully tested by starting from a discovered URI, requesting its children, and verifying that the client returns the next level of the hierarchy, including empty folders when the server reports them.

**Acceptance Scenarios**:

1. **Given** a discovered folder resource, **When** the caller requests its children, **Then** the client returns the resources the server identifies directly under that URI.
2. **Given** a discovered URI with no children, **When** the caller requests resources for it, **Then** the client reports an empty result without treating the response as a transport or authentication error.
3. **Given** a server returns discovery results across multiple response parts, **When** the caller requests resources, **Then** the client combines those parts into one coherent traversal result.

---

### User Story 3 - Identify Streaming Candidates (Priority: P3)

As a developer preparing to stream data, I want discovery results to indicate which resources can lead to streamable channels so I can choose the next streaming request target confidently.

**Why this priority**: Traversal alone does not deliver value unless the caller can tell which discovered resources are relevant for later channel-description and streaming operations.

**Independent Test**: Can be fully tested by discovering resources from a server fixture and verifying that the client exposes stream-relevant metadata such as whether a resource is channel-subscribable and whether it has children.

**Acceptance Scenarios**:

1. **Given** discovery returns a resource that can be used with later streaming operations, **When** the caller inspects the result, **Then** the client exposes that resource as stream-relevant without requiring raw message parsing.
2. **Given** discovery returns both container and leaf resources, **When** the caller inspects the result, **Then** the client can distinguish resources that should be traversed further from resources that can be used directly for downstream streaming workflows.

### Edge Cases

- The server accepts session establishment but does not advertise or support the Discovery protocol.
- The caller requests discovery for an invalid, unsupported, or malformed URI.
- The server returns no children for a valid discovery URI.
- The server splits one discovery result across multiple response messages.
- The server limits the number of resources returned for a single discovery request.
- The server returns resources that are traversable but not valid inputs for later streaming operations.
- Discovery returns a mixture of dataspaces, protocol roots, folders, and concrete data-object resources.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The client MUST support the ETP Discovery protocol behavior needed to request child resources for a specified URI after session establishment.
- **FR-002**: The client MUST allow a caller to start discovery from `eml://` so the caller can enumerate the top-level traversal roots exposed by the server.
- **FR-003**: The client MUST allow a caller to request child resources for any previously discovered URI that the server exposes.
- **FR-004**: The client MUST return discovery results in a caller-usable structure that includes each resource's URI, content type, display name, resource type, child-presence indicator, and stream-relevance indicator.
- **FR-005**: The client MUST preserve enough discovery metadata for callers to distinguish URI-protocol roots, folders, and concrete data-object resources.
- **FR-006**: The client MUST expose whether a discovered resource is suitable for subsequent streaming-oriented operations when the server marks it as channel-subscribable.
- **FR-007**: The client MUST handle multi-part discovery responses as one logical result for a single request.
- **FR-008**: The client MUST return an empty discovery result when the server reports no child resources for a valid request.
- **FR-009**: The client MUST surface discovery-specific failures in a secret-safe way that callers can distinguish from authentication, transport, and session-establishment failures.
- **FR-010**: The client MUST surface protocol-limit or rejection behavior for discovery requests in a way that allows the caller to understand that traversal was incomplete or denied.
- **FR-011**: The feature MUST include automated coverage for root discovery, child traversal, stream-candidate identification, and discovery failure behavior.
- **FR-012**: The implementation work for this feature MUST identify the governing ETP v1.1 Discovery clauses and resource-record semantics so tests and behavior remain traceable to the specification.

### Key Entities *(include if feature involves data)*

- **Discovery Request**: A caller-initiated request to enumerate the child resources available beneath a specific URI.
- **Discovered Resource**: The metadata returned by the server for one traversable or directly usable item, including its URI, type, descriptive fields, and indicators such as whether it has children or is channel-subscribable.
- **Traversal Result**: The complete logical result of one discovery step, including the requested URI, the returned resources, and whether the traversal completed cleanly, returned no children, or ended in a protocol-level rejection.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can start from `eml://` and retrieve the server's top-level discovery roots without manually constructing server-specific URIs.
- **SC-002**: In automated acceptance testing, 100% of covered discovery traversal scenarios return the expected child-resource sets for the requested URIs, including empty results where appropriate.
- **SC-003**: In automated acceptance testing, 100% of covered discovery results expose enough metadata for a caller to determine whether to traverse deeper or use a resource for downstream streaming workflows.
- **SC-004**: In automated acceptance testing, 100% of covered discovery failures are reported as secret-safe, actionable outcomes distinguishable from authentication and transport failures.

## Assumptions

- Scope is limited to read-only discovery traversal needed to locate resources relevant to later streaming workflows; initiating actual streaming remains a separate feature flow.
- The authoritative protocol source is `docs/ETP_Specification_v1.1_Doc_v1.1.md`, with the WITSML implementation guide providing additional traversal examples for streaming-related resource discovery.
- The server supports authenticated session establishment before any Discovery messages are exchanged.
- Discovery traversal will be implemented against the server role of the Discovery protocol and the customer role of the client.
- The feature will cover only the Discovery messages and resource metadata required to traverse and identify streaming candidates, not full CRUD operations on discovered objects.
