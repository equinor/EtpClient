# Quickstart: ETP Discovery Traversal

## Goal

Connect to an ETP server, resolve the top-level discovery roots from `eml://`, and inspect the returned resources to choose the next traversal or streaming target.

## Prerequisites

- .NET 10 SDK installed
- A reachable ETP endpoint with valid credentials
- A server that supports Discovery (Protocol 3)
- The client configured for a protocol set that includes Discovery in the customer role (this is the default; no extra configuration required)

## 1. Connect to the server

Use the existing authenticated client connection flow. Discovery (Protocol 3) is advertised automatically.

```csharp
var client = new EtpClient(logger);
var result = await client.ConnectAsync(options, ct);
// result.State == EtpConnectionState.Connected
```

Expected outcome:

- The session establishes successfully.
- The negotiated protocol set is sufficient for Discovery requests.

## 2. Request top-level resources

Issue Discovery for `eml://` immediately after the connection is established.

```csharp
DiscoveryResult discovery = await client.DiscoverResourcesAsync("eml://", ct);
```

Expected outcome:

- `discovery.State == DiscoveryResultState.CompletedWithResources` — the server returned one or more resources.
- `discovery.State == DiscoveryResultState.CompletedEmpty` — the server acknowledged the URI but returned no children.
- If the server rejects the request, `EtpDiscoveryException` is thrown.

## 3. Handle an empty acknowledgement

```csharp
if (discovery.State == DiscoveryResultState.CompletedEmpty)
{
    Console.WriteLine("No children found for eml://");
    return;
}
```

## 4. Traverse child resources

Call `DiscoverResourcesAsync` for any discovered URI that has children.

```csharp
foreach (var resource in discovery.Resources.Where(r => r.HasChildren != 0))
{
    DiscoveryResult children = await client.DiscoverResourcesAsync(resource.Uri, ct);
    foreach (var child in children.Resources)
        Console.WriteLine($"  {child.Uri} ({child.ResourceType})");
}
```

`HasChildren` values: `-1` = unknown, `0` = none, positive = child count.

## 5. Inspect stream-relevant metadata

For each returned resource, inspect:

- `Uri` — the ETP URI for further discovery or streaming
- `ResourceType` — classifies the node (e.g., dataspace, folder, object)
- `HasChildren` — whether to recurse (`-1` or `> 0`)
- `ChannelSubscribable` — whether the resource can be used as a channel-streaming target

```csharp
foreach (var r in discovery.Resources)
{
    Console.WriteLine($"{r.Uri}");
    Console.WriteLine($"  Type:                {r.ResourceType}");
    Console.WriteLine($"  HasChildren:         {r.HasChildren}");
    Console.WriteLine($"  ChannelSubscribable: {r.ChannelSubscribable}");
}
```

## 6. Handle discovery failures

`EtpDiscoveryException` is thrown on `ProtocolException` (invalid URI, permission denied, unsupported) or unexpected responses. Catch it separately from `InvalidOperationException` (not connected).

```csharp
try
{
    var discovery = await client.DiscoverResourcesAsync("eml://", ct);
}
catch (EtpDiscoveryException ex)
{
    // Secret-safe: logs the server-supplied error, not the URI or credentials
    logger.LogWarning("Discovery failed for {Uri}: {Message} (ETP error {Code})",
        ex.RequestedUri, ex.Message, ex.EtpErrorCode?.ToString() ?? "none");
}
catch (InvalidOperationException)
{
    logger.LogError("DiscoverResourcesAsync called without an active connection.");
}
```

## 7. Sample application behavior

The sample app connects, resolves `eml://`, and prints the discovered top-level resources plus stream-relevant metadata. If discovery fails, it logs a warning and still completes the run successfully.

## Verification

Run the automated tests that cover:

- root discovery (`DiscoverResourcesAsync("eml://")`)
- child traversal (arbitrary URI after initial discovery)
- empty-child acknowledgement handling (`WasEmptyAcknowledged = true`)
- multipart response aggregation
- discovery failure behavior (`EtpDiscoveryException`)

```bash
dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj
dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj --filter "FullyQualifiedName~DiscoverResourcesAsync"
dotnet test tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj
```

If a live server is available, verify that the sample app prints the top-level URIs without exposing credentials or authorization values in output or logs.
