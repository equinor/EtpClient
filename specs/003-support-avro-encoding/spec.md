# Feature Specification: Support Avro Encoding

**Feature Branch**: `003-support-avro-encoding`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "The ETP specification states that \"The Avro specification supports the use of both binary and JSON (JavaScript Object Notation) encoding of data. ETP also supports the use of both, with the following caveats:\"\n\nI want the ETP Client to support both variations, and the client user should be able to toggle which to use in the options"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose Message Encoding (Priority: P1)

As a developer integrating with an ETP endpoint, I want to choose whether the client uses binary or JSON message encoding so I can connect to servers that require one specific encoding mode.

**Why this priority**: Without explicit encoding selection, the client cannot interoperate with endpoints that support only one of the allowed ETP transport encodings.

**Independent Test**: Can be fully tested by configuring the client for binary in one run and JSON in another, then verifying that each run uses the selected encoding and can complete session establishment when the endpoint supports that mode.

**Acceptance Scenarios**:

1. **Given** a client user configuring a connection, **When** they select binary encoding, **Then** the client uses binary ETP message encoding for the connection flow.
2. **Given** a client user configuring a connection, **When** they select JSON encoding, **Then** the client uses JSON ETP message encoding for the connection flow.

---

### User Story 2 - Get Clear Failure Behavior (Priority: P2)

As a developer diagnosing an interoperability problem, I want the client to fail clearly when the selected encoding is unsupported or rejected so I can distinguish encoding mismatches from authentication, transport, or protocol errors.

**Why this priority**: Supporting both encodings only helps if failures remain diagnosable when a server, proxy, or endpoint policy rejects the chosen format.

**Independent Test**: Can be fully tested by selecting an encoding that the target endpoint does not accept and verifying that the client reports an actionable failure without exposing secrets.

**Acceptance Scenarios**:

1. **Given** a client user selects an encoding the endpoint does not accept, **When** the connection attempt fails, **Then** the client reports that the failure is related to the requested message encoding or the endpoint's response to it.
2. **Given** a connection fails for another reason, **When** the user inspects the error details, **Then** the selected encoding is still observable in diagnostics without leaking credentials or authorization values.

---

### User Story 3 - Use Encoding Choice Consistently (Priority: P3)

As a developer using the client library as a reusable component, I want the selected encoding option to behave consistently across sample usage, tests, and future protocol operations so I can rely on one clear connection contract.

**Why this priority**: A partially applied encoding option would create hidden incompatibilities and make the client harder to adopt safely.

**Independent Test**: Can be fully tested by reviewing the public client usage flow and verifying through automated tests that the selected encoding governs connection establishment and subsequent protocol message handling consistently.

**Acceptance Scenarios**:

1. **Given** a client user selects an encoding mode, **When** the client establishes a session and exchanges protocol messages, **Then** the same encoding mode is used consistently for that session.
2. **Given** a developer inspects the public client configuration surface, **When** they look for encoding control, **Then** they find one clear option for selecting the desired ETP message encoding.

### Edge Cases

- The client user does not specify an encoding mode and the client must apply its default behavior.
- The endpoint accepts the transport connection but rejects the selected encoding during session establishment.
- The endpoint returns a message encoded differently from the mode the client selected.
- The selected encoding works for connection establishment but fails on later protocol messages in the same session.
- Diagnostics for encoding mismatch risk exposing request metadata that should remain secret-safe.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The client MUST support both binary and JSON ETP message encoding variations allowed by the relevant ETP v1.1 clauses for the scoped client behaviors.
- **FR-002**: The client MUST allow the caller to choose the desired ETP message encoding through the public connection configuration surface.
- **FR-003**: The client MUST apply the caller's selected encoding consistently throughout a session once the connection attempt begins.
- **FR-004**: The client MUST preserve a defined default encoding behavior when the caller does not explicitly choose an encoding mode.
- **FR-005**: The client MUST fail with actionable, secret-safe diagnostics when the selected encoding cannot be used successfully with the target endpoint.
- **FR-006**: The client MUST distinguish encoding-related failures from authentication, transport, and other protocol failures in its externally observable behavior.
- **FR-007**: The sample usage flow and automated tests MUST cover successful and unsuccessful use of both supported encoding modes.
- **FR-008**: The client MUST identify the ETP v1.1 clauses that govern the supported encoding behavior so that tests and behavior remain traceable to the specification.

### Key Entities *(include if feature involves data)*

- **Encoding Selection**: The caller-provided choice that determines whether a session uses binary or JSON ETP message encoding.
- **Connection Configuration**: The set of user-provided connection inputs that now includes encoding choice alongside endpoint and authentication data.
- **Encoding-Aware Session Outcome**: The observable result of a connection attempt or active session, including success or failure details tied to the selected encoding mode.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can configure the client for either binary or JSON encoding using one documented connection flow without modifying client internals.
- **SC-002**: In automated acceptance testing, 100% of covered binary-mode and JSON-mode happy-path scenarios use the requested encoding and complete the scoped session flow when the endpoint supports that encoding.
- **SC-003**: In automated acceptance testing, 100% of covered encoding-mismatch scenarios report a secret-safe failure that is distinguishable from authentication and transport failures.
- **SC-004**: Developers reviewing the client configuration surface can identify the encoding-selection behavior and its default without consulting internal implementation details.

## Assumptions

- The current client behavior is binary-only, so existing binary behavior remains the compatibility baseline unless the caller explicitly chooses another mode.
- Scope is limited to the client behaviors already covered by the existing authenticated session flow and the protocol operations included during this feature's implementation.
- Target endpoints may differ in which encoding modes they support, so the client must make the selected mode explicit rather than inferring it from endpoint behavior alone.
- The ETP specification content in `docs/ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md` is the authoritative source for encoding-related behavior and caveats.
