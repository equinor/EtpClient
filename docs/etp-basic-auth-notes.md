# ETP Basic Auth — Implementation Notes

## Scope

This implementation covers the **minimal** path to establish an authenticated ETP 1.1 session:

1. Open a WebSocket connection with HTTP Basic authentication
2. Send `RequestSession` (Protocol 0, messageType=1)
3. Receive `OpenSession` (Protocol 0, messageType=2) — success path
4. Receive `ProtocolException` (Protocol 0, messageType=1000) — rejection path
5. Send `CloseSession` (Protocol 0, messageType=3) — clean close

**Out of scope**: data streaming, producer/consumer protocols (1–15), advanced session management, multi-frame messages beyond handshake.

---

## ETP Specification References

| Topic | Spec Location |
|---|---|
| WebSocket as transport | §3 Transport |
| WebSocket sub-protocol identifier | `etp12.energistics.org` |
| HTTP Basic Auth delivery | HTTP `Authorization` header on WS upgrade |
| Protocol 0 Core schema | §4.2 Protocol 0 — Core Messages |
| RequestSession schema | §4.2.3 RequestSession |
| OpenSession schema | §4.2.4 OpenSession |
| ProtocolException schema | §4.2.10 ProtocolException |
| Message header schema | §4.1.3 EtpMessageHeader |
| Avro encoding | §4.1.1 Binary Encoding |
| Max WebSocket frame size | 16,777,216 bytes (16 MiB) default |

---

## Avro Codec Notes

- Hand-rolled minimal codec (no Apache.Avro dependency). Only the types needed for Protocol 0 are implemented.
- Integers and longs use **zigzag + variable-length** encoding (standard Avro binary).
- Strings are length-prefixed with a zigzag long.
- Arrays use block-count encoding: non-zero count N followed by N items, then terminating 0.
- An **empty** array or map is encoded as a single `0x00` byte (block-count 0 = end immediately). Do **not** write an additional terminator with `WriteArrayEnd()` after `WriteArrayStart(0)`.
- Maps follow the same block-count pattern as arrays, with each entry being `(string key, DataValue value)`.

## Known Limitations

- `EtpSessionManager` buffers the entire OpenSession response in a single 64 KiB buffer. Servers that send unusually large OpenSession messages may require a larger buffer.
- The Avro `DataValue` skip helper (`SkipDataValue`) supports the 17-element union defined in ETP 1.1. A schema update to ETP would require updating `AvroReader.SkipDataValue`.
- `ClientWebSocketTransport.HttpStatusCode` is only populated when `ClientWebSocket.Options.CollectHttpResponseDetails = true`, which requires .NET 5+.
