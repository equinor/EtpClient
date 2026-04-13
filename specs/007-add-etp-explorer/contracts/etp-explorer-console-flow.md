# Contract: ETP Explorer Console Flow

## Purpose

Define the user-visible interaction contract for the `EtpExplorer` console application.

## Startup Configuration Contract

The application reads configuration from the `EtpExplorer` section via .NET configuration with explicit user-secrets loading.

**Required keys**
- `EtpExplorer:EndpointUri`
- `EtpExplorer:Username`
- `EtpExplorer:Password`

**Optional keys**
- `EtpExplorer:MessageEncoding`
- `EtpExplorer:ProtocolRequestTimeoutSeconds`

**Startup behavior**
- If any required key is missing or invalid, the explorer must stop before connecting and present a secret-safe setup message showing the missing key names and example `dotnet user-secrets set` commands.
- Secrets must never be printed back to the terminal.

## Navigation Contract

After a successful connection, the explorer must first present the discovered root nodes and require the user to select one root node before deeper browsing begins.

For a server like the current live environment, examples of root-node choices are `witsml14` and `witsml20`.

After a root node has been selected, the explorer presents a top-level interactive menu with these actions:

1. Browse current URI
2. Change root node
2. Review selected endpoints
3. Start streaming selected endpoints
4. Exit

While browsing within the selected root node, the explorer must support these actions:

1. Open a child resource
2. Resolve streamable endpoints for the current resource
3. Add one or more resolved endpoints to the selection set
4. Return to the parent URI
5. Return to the main menu

Navigation behavior must preserve tree context:

- The selected root node is the tree root for the current browse session.
- The explorer must show enough tree context for the user to understand the current node and its parent path.
- Returning to the parent URI must move one level up within the selected tree rather than resetting the whole session.
- Changing the root node must take the user back to root-node selection and rebuild the tree context for the newly selected branch.

## Selection Contract

- The selection set must show every selected endpoint with enough context to distinguish source resource, channel name, and channel URI.
- Selecting the same endpoint twice must not create duplicates.
- The user must be able to remove individual endpoints and clear the full selection set before streaming begins.
- Starting a stream with an empty selection set must be blocked with an understandable message.

## Streaming Contract

When the user starts streaming:

- The explorer converts the current selection set into Protocol 1 subscriptions.
- The explorer enters a streaming-focused view and renders incoming events in a live-updating format.
- Every rendered event must identify its source endpoint clearly enough to distinguish concurrent streams.
- The user must have a clear stop action that cancels live streaming and returns to the connected interactive state.
- If some selected endpoints fail before or during stream startup, the explorer must surface which endpoints failed and continue only with the endpoints that remain valid when that is safe to do so.

## Shutdown Contract

- Exiting from a non-streaming state closes the ETP session cleanly and terminates the process.
- Exiting from an active streaming state must first stop or cancel streaming, then close the session cleanly.
- Connection, discovery, describe, and streaming failures must be attributed to the failing workflow stage without leaking credentials.
