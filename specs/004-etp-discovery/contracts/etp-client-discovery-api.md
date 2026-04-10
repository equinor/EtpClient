# Contract: ETP Client Discovery Traversal

## Purpose

Defines the public client contract for issuing Discovery requests and receiving typed traversal results that callers can use to locate resources for downstream streaming workflows.

## Public API Contract

The library exposes an asynchronous discovery traversal operation after session establishment.

### Required behavior

- The caller can request discovery for an arbitrary URI, including the required root URI `eml://`.
- The operation is asynchronous and cancellation-aware.
- The result is returned as a typed traversal result rather than raw protocol payloads.
- The operation is valid only when the client session is connected.

## Discovery Request Contract

### Request inputs

- Traversal URI
- Existing authenticated session context
- The selected session message encoding

### Request semantics

1. The caller requests discovery for a URI.
2. The client sends Protocol 3 `GetResources` for that URI.
3. The client receives one or more `GetResourcesResponse` messages, or an `Acknowledge` when the URI has no children, or a `ProtocolException` when the request is invalid or denied.

## Discovery Result Contract

Each returned resource exposes enough metadata for traversal and stream-target identification.

### Required resource fields

- `Uri`
- `ContentType`
- `Name`
- `ResourceType`
- `HasChildren`
- `ChannelSubscribable`
- `Uuid`
- `ObjectNotifiable`

### Result semantics

- Multipart responses are aggregated into one logical result.
- Empty-child acknowledgements are surfaced as an empty successful result.
- Resource ordering is preserved in response order.

## Failure Contract

The client must distinguish discovery-specific failures from connection-establishment failures.

### Examples of failure behavior

- Invalid or unsupported traversal URI → protocol failure with secret-safe detail
- Store-imposed resource limit → protocol failure, typically `EPERMISSION_DENIED` (`6`)
- Discovery unsupported by server or not negotiated → protocol failure with actionable context
- Authentication/transport/session issues before discovery → existing connection failure behavior remains in effect

## Sample-App Contract

The sample app resolves `eml://` after connecting and prints the returned top-level resources in a human-readable form.

### Required sample behavior

- Print the top-level URIs returned by Discovery.
- Indicate whether each resource has children.
- Indicate whether each resource is channel-subscribable.
- Fail secret-safely when discovery is rejected or unsupported.

## Test Contract

Automated tests for this feature must verify:

- root discovery for `eml://`
- child traversal for a discovered folder URI
- empty-child acknowledgement handling
- multipart discovery aggregation
- secret-safe protocol failure behavior for invalid, unsupported, or limited requests
- typed exposure of stream-relevant metadata such as `channelSubscribable` and `hasChildren`

---

## Implemented API Reference

### `EtpClient.DiscoverResourcesAsync`

```csharp
/// <summary>
/// Requests the child resources for <paramref name="uri"/> from the connected server.
/// </summary>
/// <param name="uri">The ETP URI to discover children for (e.g., "eml://").</param>
/// <param name="ct">Optional cancellation token.</param>
/// <returns>A <see cref="DiscoveryResult"/> with the discovered resources and result state.</returns>
/// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
/// <exception cref="EtpDiscoveryException">Thrown when the server returns a ProtocolException or an unexpected message.</exception>
public Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct = default)
```

### `DiscoveryResult` record

```csharp
public record DiscoveryResult(
    string RequestedUri,
    IReadOnlyList<DiscoveredResource> Resources,
    bool WasEmptyAcknowledged,
    EtpMessageEncoding MessageEncoding)
{
    // Computed: CompletedWithResources | CompletedEmpty | Failed
    public DiscoveryResultState State { get; }
}
```

### `DiscoveredResource` record

```csharp
public record DiscoveredResource(
    string Uri,
    string ContentType,
    string Name,
    string ResourceType,
    int HasChildren,           // -1=unknown, 0=none, >0=count
    bool ChannelSubscribable,
    string? Uuid,
    bool ObjectNotifiable,
    long LastChanged,
    IReadOnlyDictionary<string, byte[]> CustomData)
```

### `EtpDiscoveryException`

```csharp
public class EtpDiscoveryException : Exception
{
    public string RequestedUri { get; }
    public int? EtpErrorCode { get; }
}
```

### `DiscoveryResultState` enum

```csharp
public enum DiscoveryResultState
{
    CompletedWithResources,
    CompletedEmpty,
    Failed
}
```
