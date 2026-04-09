---
description: "Task list for ETP Basic Auth Connection"
---

# Tasks: ETP Basic Auth Connection

**Branch**: `001-etp-basic-auth`
**Input**: Design documents from `/specs/001-etp-basic-auth/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Tests**: Tests are REQUIRED for protocol-facing work. Unit tests and integration tests for wire behavior.

## Path Conventions

- **C# client library**: `src/EtpClient/`, `tests/EtpClient.UnitTests/`, `tests/EtpClient.IntegrationTests/`

---

## Phase 1: Setup

**Purpose**: Initialize solution structure and establish build tooling.

- [X] T001 Create .NET 10 solution, library project (src/EtpClient), and test projects (tests/EtpClient.UnitTests, tests/EtpClient.IntegrationTests)
- [X] T002 Add NuGet package references: Microsoft.Extensions.Logging.Abstractions to library; xUnit and test SDK to both test projects
- [X] T003 [P] Verify .gitignore covers .NET build outputs (bin/, obj/, *.user)

**Checkpoint**: `dotnet build EtpClient.sln` exits 0 with no warnings.

---

## Phase 2: Foundation Models

**Purpose**: Define the data contracts that tests and implementation both depend on.

- [X] T004 [P] Create EtpConnectionState enum and EtpConnectionFailureCategory enum in src/EtpClient/Models/
- [X] T005 [P] Create SupportedProtocol, ProtocolVersion, NegotiatedSessionInfo records in src/EtpClient/Models/
- [X] T006 Create EtpConnectionOptions with validation (raises on blank URI, invalid scheme, missing credentials) in src/EtpClient/Models/
- [X] T007 Create EtpConnectionException (secret-safe, distinguishes categories) and EtpConnectionResult in src/EtpClient/Models/

---

## Phase 3: Protocol Layer

**Purpose**: Implement the minimal Avro binary codec and Protocol 0 message types.

### Tests for Protocol Layer (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T008 [P] Unit tests for AvroWriter/AvroReader: int zigzag, long zigzag, string, bytes, fixed, bool, empty array, empty map in tests/EtpClient.UnitTests/Protocol/AvroEncodingTests.cs
- [X] T009 [P] Unit tests for EtpMessageHeader round-trip, RequestSession serialization, OpenSession deserialization in tests/EtpClient.UnitTests/Protocol/Protocol0MessageTests.cs

### Implementation for Protocol Layer

- [X] T010 [P] Implement AvroWriter (zigzag varlen int/long, string, bytes, fixed, bool, array blocks, map blocks) in src/EtpClient/Protocol/AvroWriter.cs
- [X] T011 [P] Implement AvroReader (matching decode paths including skip-DataValue for map values) in src/EtpClient/Protocol/AvroReader.cs
- [X] T012 Create EtpMessageType constants and EtpMessageHeader encode/decode in src/EtpClient/Protocol/
- [X] T013 Implement RequestSessionMessage.Encode using AvroWriter (ETP Protocol 0, messageType=1) in src/EtpClient/Protocol/
- [X] T014 Implement OpenSessionMessage.Decode using AvroReader (ETP Protocol 0, messageType=2) in src/EtpClient/Protocol/

**Checkpoint**: Protocol layer unit tests green.

---

## Phase 4: Connection and Public API

**Purpose**: Implement the WebSocket transport abstraction, the session state machine, and the public EtpClient surface.

### Tests for Connection (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T015 [P] Unit tests for EtpConnectionOptions validation (missing URI, invalid scheme, blank credentials) in tests/EtpClient.UnitTests/Models/EtpConnectionOptionsTests.cs
- [X] T016 [P] Unit tests for EtpConnectionException: message is secret-safe, category is correct for each failure type in tests/EtpClient.UnitTests/Models/EtpConnectionExceptionTests.cs
- [X] T017 [P] Unit tests for EtpSessionManager state transitions (Connecting→Connected, Connecting→Failed, Connecting→Canceled) using mock transport in tests/EtpClient.UnitTests/Connection/EtpSessionManagerTests.cs

### Implementation for Connection

- [X] T018 Define IWebSocketTransport interface in src/EtpClient/Connection/
- [X] T019 Implement ClientWebSocketTransport: sets Authorization header before ConnectAsync, adds ETP subprotocol, collects HTTP response details for 401 detection in src/EtpClient/Connection/
- [X] T020 Implement EtpSessionManager: state machine, ConnectAsync (open WS → send RequestSession → await OpenSession/ProtocolException), CloseAsync, Dispose in src/EtpClient/Connection/
- [X] T021 Implement EtpClientLog (secret-safe LoggerMessage statics for connect, session-established, auth-failed, session-error, close events) in src/EtpClient/Diagnostics/
- [X] T022 Implement public EtpClient class: State property, ConnectAsync, CloseAsync, IAsyncDisposable in src/EtpClient/

**Checkpoint**: Unit tests green.

---

## Phase 5: Integration Tests

**Purpose**: Validate the full handshake flow end-to-end with an in-process test server.

- [X] T023 [P] [US1] Integration test: valid credentials → client reaches Connected state and NegotiatedSessionInfo is populated in tests/EtpClient.IntegrationTests/Connection/ConnectAsyncTests.cs
- [X] T024 [P] [US2] Integration test: invalid credentials (401) → EtpConnectionException with Category=Authentication, no secret in message
- [X] T025 [P] [US3] Integration test: cancellation during handshake → state transitions to Canceled
- [X] T026 [P] [US3] Integration test: CloseAsync on connected client → state transitions to Closed, WebSocket closed cleanly

**Checkpoint**: All integration tests green. `dotnet test EtpClient.sln` exits 0.

---

## Phase 6: Polish

- [X] T027 Add XML doc comments to all public types and members in src/EtpClient/
- [X] T028 Record ETP clause references and scope boundary note in docs/etp-basic-auth-notes.md
- [X] T029 Verify complete test run and mark all tasks complete
