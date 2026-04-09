# Feature Specification: ETP Basic Auth Connection

**Feature Branch**: `001-etp-basic-auth`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: User description: "Using the referenced ETP specification, create the minimal required for an ETPClient to connect to an endpoint using basic authentication"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Establish Authenticated Session (Priority: P1)

As a developer integrating an ETP client, I want to open a connection to an ETP endpoint with Basic authentication and complete the initial ETP session handshake so the client reaches a usable connected state.

**Why this priority**: Without a successful authenticated connection and Protocol 0 session establishment, the client provides no usable value.

**Independent Test**: Can be fully tested by configuring a reachable endpoint with valid Basic authentication credentials, opening a connection, and verifying that the client reaches a ready state only after the WebSocket connection is open and the ETP session is accepted.

**Acceptance Scenarios**:

1. **Given** a reachable endpoint, valid Basic authentication credentials, and valid session settings, **When** the developer opens the connection, **Then** the client opens the WebSocket connection, performs the documented Protocol 0 session exchange, and reports the session as connected.
2. **Given** a configured endpoint and credentials, **When** the endpoint accepts the connection and returns the expected session acceptance response, **Then** the client exposes the negotiated session details needed for later protocol use.

---

### User Story 2 - Fail Safely on Rejected Authentication or Session Setup (Priority: P2)

As a developer integrating an ETP client, I want authentication and session-establishment failures to be reported clearly so I can distinguish invalid credentials from transport or protocol problems.

**Why this priority**: Clear failure behavior is required to make the initial connection feature operable in real environments and to reduce debugging time.

**Independent Test**: Can be fully tested by attempting a connection with invalid credentials, unreachable transport, and invalid session negotiation expectations, and verifying that each case returns a distinct failure outcome without exposing secrets.

**Acceptance Scenarios**:

1. **Given** invalid Basic authentication credentials, **When** the developer opens the connection, **Then** the client fails the attempt with an authentication-specific error and does not report a connected session.
2. **Given** a reachable endpoint that does not complete the expected Protocol 0 session exchange, **When** the developer opens the connection, **Then** the client fails the attempt with a session-establishment error that is distinct from an authentication failure.

---

### User Story 3 - Close or Cancel Cleanly (Priority: P3)

As a developer integrating an ETP client, I want to cancel an in-progress connection or close an established session cleanly so the client does not leave orphaned sessions or ambiguous connection state.

**Why this priority**: Clean shutdown is lower priority than initial connectivity, but it is still required for reliable integration and repeatable tests.

**Independent Test**: Can be fully tested by canceling a connection attempt during setup and by closing an established session, then verifying that the client reports a closed state and no active session remains.

**Acceptance Scenarios**:

1. **Given** a connection attempt in progress, **When** the developer cancels it, **Then** the client stops the attempt, reports a closed or canceled state, and releases session resources.
2. **Given** an established session, **When** the developer closes the connection, **Then** the client terminates the session cleanly and reports that no active connection remains.

### Edge Cases

- The endpoint rejects Basic authentication before the WebSocket upgrade completes.
- The transport opens but the endpoint never sends the expected session-acceptance response.
- The endpoint sends an unexpected or duplicate Protocol 0 message during connection setup.
- The caller cancels the connection while authentication or session negotiation is still in progress.
- Diagnostics or error messages risk exposing usernames, passwords, or encoded authorization values.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The client MUST support configuration of the endpoint address, Basic authentication credentials, and the session settings required to initiate an ETP connection.
- **FR-002**: The client MUST initiate the connection by opening a WebSocket session to the configured endpoint and supplying Basic authentication in a form the endpoint can validate during connection establishment.
- **FR-003**: The client MUST perform the minimal Protocol 0 session exchange required to establish an ETP session, including sending a session request and treating the session as connected only after session acceptance is received.
- **FR-004**: The client MUST make the connection lifecycle observable through distinct states for connecting, connected, failed, canceled, and closed.
- **FR-005**: The client MUST validate required connection inputs before making a network attempt and reject incomplete or malformed configuration with a caller-visible validation error.
- **FR-006**: The client MUST report authentication failures, transport failures, and session-negotiation failures as distinct outcomes.
- **FR-007**: The client MUST allow the caller to cancel an in-progress connection attempt and to close an established session cleanly.
- **FR-008**: The client MUST prevent credentials and authorization values from appearing in logs, diagnostics, errors, or other externally visible state.
- **FR-009**: The client MUST document the referenced ETP session sequence that governs connection establishment, including the WebSocket open, `RequestSession`, and `OpenSession` flow.

### Key Entities *(include if feature involves data)*

- **Connection Profile**: The caller-supplied description of the endpoint, authentication material, and session request settings needed to start an ETP connection.
- **Session Attempt**: The in-progress or completed connection lifecycle instance, including current state, timestamps, and outcome.
- **Connection Failure**: The structured failure result that distinguishes validation, authentication, transport, protocol, and caller-cancelation outcomes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In automated acceptance testing, 100% of valid connection scenarios reach a connected state only after the WebSocket connection opens and the expected Protocol 0 session acceptance is received.
- **SC-002**: In automated acceptance testing, 100% of invalid-credential scenarios fail with an authentication-specific outcome and expose no secrets in logs or returned error text.
- **SC-003**: Developers can establish an authenticated ETP session using one documented connection flow without manually assembling protocol messages outside the client surface.
- **SC-004**: In automated acceptance testing, canceling or closing a connection leaves no active session and results in a final non-connected state in 100% of tested cases.

## Assumptions

- The endpoint supports Basic authentication for the client-initiated connection flow.
- The minimal scope of this feature is authenticated transport establishment and Protocol 0 session setup; later protocol operations and data subscriptions are out of scope.
- The local WITSML ETP implementation reference is used to ground the required session sequence, especially the WebSocket open followed by `RequestSession` and `OpenSession`.
- Secure transport and certificate trust are handled by the deployment environment rather than by this feature.
