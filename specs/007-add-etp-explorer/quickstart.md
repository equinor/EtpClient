# Quickstart: Add ETP Explorer

## Prerequisites

- .NET 10 SDK installed
- Access to an ETP server that supports Basic authentication and the Discovery / ChannelStreaming workflows used by the library

## Configure user secrets

From the repository root, configure the explorer’s server URL and credentials:

```bash
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:EndpointUri" "wss://your-server/etp"
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:Username" "your-username"
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:Password" "your-password"
```

Optional settings:

```bash
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:MessageEncoding" "Binary"
dotnet user-secrets --project samples/EtpExplorer/EtpExplorer.csproj set "Etp:ProtocolRequestTimeoutSeconds" "10"
```

## Run the explorer

```bash
dotnet run --project samples/EtpExplorer/EtpExplorer.csproj
```

## Expected flow

1. The explorer validates configuration and connects to the ETP server.
2. The explorer discovers the available root nodes and prompts you to choose one, such as `witsml14` or `witsml20`.
3. After you choose a root node, the explorer opens a fixed pane browser. The selected root's child nodes are shown in the left-most column.
4. Use the arrow keys to move within the active column. Press `Enter` to open the focused node and load its children into a new column on the right.
5. Press `Space` on the focused node to resolve streamable channels for that node and add them to the selection set when applicable.
6. Press `Esc` to return to the main menu for selection review, root changes, streaming, or exit.
7. Starting streaming opens a live view that renders endpoint-attributed output until you stop or exit.

## Verification targets

- After connect, the explorer prompts for the available root nodes instead of skipping directly into deeper browsing.
- Choosing `witsml14` or `witsml20` scopes the pane browser to that selected tree.
- Opening a node adds a new child column to the right instead of replacing the current list.
- Pressing `Space` on a streamable resource adds one or more selectable channels when the server supports them.
- The selection set retains multiple endpoints across browse actions.
- Live output labels each incoming entry with its source endpoint.
- Stopping streaming returns you to the connected interactive state without terminating the session unexpectedly.

## Acceptance validation for SC-001

1. Configure the required user secrets before starting the validation run.
2. Launch the explorer from a clean terminal session using the standard `dotnet run` command.
3. Connect to a server that exposes multiple root nodes.
4. Measure elapsed time from process start until a root node is selected and the first level of that root is visible.
5. Record the run as passing when the workflow completes in under 2 minutes without consulting protocol payloads or source code.
