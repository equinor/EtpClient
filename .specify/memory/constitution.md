<!--
Sync Impact Report
Version change: 1.0.0 -> 1.0.1
Modified principles:
- I. Protocol Fidelity First -> I. Protocol Fidelity First
- Additional Constraints clarified to include the markdown protocol rendering
Added sections:
- None
Removed sections:
- None
Templates requiring updates:
- ✅ .specify/templates/plan-template.md (no change required)
- ✅ .specify/templates/spec-template.md (no change required)
- ✅ .specify/templates/tasks-template.md (no change required)
- ✅ .specify/templates/commands/*.md (no files present)
Follow-up TODOs:
- None
-->

# ETP Client Constitution

## Core Principles

### I. Protocol Fidelity First
All library behavior MUST align with the Enegistics Transfer Protocol v1.1 specification in
docs/ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.pdf,
docs/ETP_v1.1_for_WITSML_v1411_Imp_Spec_v1.0_Doc_v1.0.md, docs/ETP_Specification_v1.1_Doc_v1.1.pdf and docs/ETP_Specification_v1.1_Doc_v1.1.md. Every feature, bug fix, or
behavioral change MUST identify the relevant protocol clauses, message types, and expected
wire semantics before implementation. The PDF remains the canonical source, and the markdown
version is an approved working copy for search, citation, and planning. Intentional
deviations, unsupported features, and interop workarounds MUST be documented explicitly in
the feature spec and release notes.
Rationale: this library exists to be a dependable protocol client, so convenience cannot
override protocol correctness.

### II. Secure Session Handling
Basic authentication support MUST prevent credential leakage in code, logs, exceptions,
tests, and sample configuration. Credentials MUST be passed through explicit configuration
boundaries, never hard-coded, and only used with transport assumptions documented in the
feature plan. Connection setup, authentication failure, session shutdown, and reconnect
behavior MUST be deterministic and observable to callers.
Rationale: this client will operate against production ETP endpoints, and authentication is
part of the contract surface, not an implementation detail.

### III. Mandatory Test Coverage
Every protocol-facing change MUST include automated tests. Unit tests MUST cover parsing,
state transitions, and error handling. Contract or integration tests MUST cover connection
establishment, subscription requests, and live log measurement flows whenever behavior on
the wire changes. A change is not complete until the relevant tests fail before the fix and
pass after the fix.
Rationale: ETP streaming behavior is easy to regress silently, and test depth is the only
reliable guard against incompatible client behavior.

### IV. Async Streaming by Default
The public API MUST be asynchronous, cancellation-aware, and safe for long-lived streaming
workloads. Blocking calls, hidden thread creation, and unbounded buffering are prohibited
unless they are clearly justified in the plan and reviewed as an exception. Disposal,
subscription lifetimes, and message delivery semantics MUST be explicit in the API design.
Rationale: subscribing to live log measurements is a streaming concern, and the library must
behave predictably under cancellation, latency, and backpressure.

### V. Diagnosable Client Behavior
The library MUST emit actionable diagnostics for connection lifecycle events, protocol
failures, and subscription state changes without exposing secrets or raw credentials.
Exceptions MUST preserve the underlying cause and provide enough context for users to act on
them. Logging and telemetry hooks MUST favor structured data and stable terminology over
free-form strings.
Rationale: consumers need to troubleshoot endpoint interoperability and runtime failures
without inspecting the wire manually.

## Additional Constraints

The codebase MUST remain a C# client library first. Public APIs MUST be documented and typed
for endpoint connection, authentication configuration, session lifecycle, and live log
subscription usage. Dependencies SHOULD remain minimal and justified, especially around
networking, serialization, and logging. Protocol source material in the docs folder is the
authoritative reference for message behavior. The markdown rendering may be used for search,
annotation, and planning, but it MUST stay consistent with the PDF and may not contradict the
PDF when defining or changing behavior.

## Delivery Workflow

Each feature spec MUST state the relevant ETP clauses, the endpoint interaction being
implemented, the authentication assumptions, and the expected subscription lifecycle.
Implementation plans MUST pass a constitution check covering protocol fidelity, secure
credential handling, async API design, diagnostics, and test coverage before coding starts.
Task lists MUST include the exact test work needed for unit and integration or contract
coverage, plus any public API documentation or compatibility notes required by the change.
Reviews MUST block merges that introduce undocumented protocol deviations, missing
streaming-lifecycle semantics, or insufficient diagnostics.

## Governance

This constitution overrides conflicting local habits, ad hoc convenience changes, and sample
template text. Amendments MUST be made through a documented change to this file with an
updated Sync Impact Report and any required template updates completed in the same change.
Constitution versioning follows semantic versioning: MAJOR for incompatible governance
changes or principle removals, MINOR for new principles or materially expanded requirements,
and PATCH for clarifications that do not change enforcement. Every implementation plan,
feature spec, task list, and code review MUST include an explicit compliance check against
the current constitution version.

**Version**: 1.0.1 | **Ratified**: 2026-04-09 | **Last Amended**: 2026-04-09
