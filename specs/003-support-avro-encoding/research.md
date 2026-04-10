# Research: Support Avro Encoding

## Decision 1: Add an explicit public encoding-selection model with binary as the default

**Decision**: Introduce a caller-facing encoding selection in the public connection options and preserve binary as the default when callers do not opt into JSON.

**Rationale**: The feature requirement is explicit user control over binary vs JSON behavior. Preserving binary as the default keeps current integrations stable and makes the change additive rather than breaking.

**Alternatives considered**:
- Infer encoding from endpoint behavior: rejected because it makes behavior implicit and harder to diagnose.
- Switch the default to JSON: rejected because the current client is binary-only and existing users should not change behavior silently.
- Expose separate connect methods per encoding: rejected because one option on the existing connection contract is clearer and scales better.

## Decision 2: Separate message encoding from WebSocket transport concerns

**Decision**: Treat encoding choice as a codec concern layered on top of transport, while extending the WebSocket transport abstraction only enough to support the required frame mode for the selected encoding.

**Rationale**: The current implementation hardwires binary framing and Avro binary codecs together. Supporting both modes cleanly requires the session manager to select a codec and frame mode based on the same option, rather than duplicating connection logic.

**Alternatives considered**:
- Duplicate the full session manager path for JSON: rejected because it would create parallel connection flows that can drift.
- Keep the current transport interface binary-only and wrap JSON in byte arrays: rejected because it hides a real protocol distinction and complicates diagnostics.
- Move encoding choice entirely into the transport layer: rejected because encoding affects message serialization and parsing, not just frame delivery.

## Decision 3: Scope initial dual-encoding support to the currently implemented Protocol 0 session flow

**Decision**: Implement binary and JSON support first for the existing authenticated Protocol 0 handshake path (`RequestSession`, `OpenSession`, `ProtocolException`) and require the selected encoding to be used consistently within that session.

**Rationale**: The current client scope is session establishment. Extending the encoding choice across the currently implemented flow delivers immediate interoperability value while keeping the feature aligned with the existing codebase and constitution.

**Alternatives considered**:
- Define support for future protocols now: rejected because those behaviors are not yet implemented in the client.
- Limit JSON support to outgoing messages only: rejected because a half-duplex encoding feature would not satisfy interoperability or user expectations.

## Decision 4: Make encoding-related failures observable but secret-safe

**Decision**: Extend diagnostics and externally visible failure behavior so callers can tell which encoding was selected and whether a failure was caused by transport, protocol, or an encoding mismatch, without exposing credentials or raw authorization values.

**Rationale**: Dual-mode interoperability is only useful if failures are diagnosable. Encoding selection should appear in structured diagnostics and failure paths the same way endpoint and category information already do.

**Alternatives considered**:
- Reuse generic protocol failure text only: rejected because it makes encoding mismatches too hard to diagnose.
- Log raw frames for debugging: rejected because it increases secret and payload leakage risk.

## Decision 5: Cover both encodings with unit and integration tests, while keeping live tests opt-in

**Decision**: Add deterministic automated tests for binary and JSON selection, codec behavior, and session establishment/failure handling, and keep live endpoint tests opt-in through user secrets or environment variables.

**Rationale**: The constitution requires protocol-facing automated coverage. Deterministic tests should validate both modes without depending on external servers, while live tests remain useful for real interoperability debugging.

**Alternatives considered**:
- Rely only on live endpoint validation: rejected because it is too brittle for core regression coverage.
- Test only the default binary path and spot-check JSON manually: rejected because the feature’s value is support for both modes.
