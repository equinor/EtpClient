# Research: Add ETP Explorer

## Decision 1: Add a dedicated explorer sample project and test project

**Decision**: Create a new console application at `samples/EtpExplorer` and a matching test project at `tests/EtpExplorer.Tests` instead of extending the existing sample console.

**Rationale**: The current sample console is a linear demonstration of connect, describe, stream, and range flows driven by configuration. The requested feature is a second, interactive exploration experience with materially different UX and test seams. A separate project keeps that UI complexity isolated and preserves the current sample as a minimal reference.

**Alternatives considered**:
- Expand `samples/EtpClient.SampleConsole`: rejected because the linear sample would become harder to understand and maintain.
- Put the explorer under `src/EtpClient`: rejected because the explorer is a sample application, not library logic.
- Build the explorer without dedicated tests: rejected because the repo constitution requires automated coverage for user-facing changes.

## Decision 2: Use Spectre.Console for the interactive presentation layer

**Decision**: Build the explorer UI on `Spectre.Console` using prompts, selection widgets, tables, panels, status spinners, and a live-updating stream view.

**Rationale**: The user explicitly requested a more beautiful CLI experience. Spectre.Console provides a mature console UI toolkit for navigation and live rendering while keeping the application in plain .NET console territory.

**Alternatives considered**:
- Raw `System.Console`: rejected because it would make browsing, multi-selection, and live output harder to present cleanly.
- A full GUI or web front end: rejected because the requested scope is a secondary console application.
- Another terminal UI framework: rejected because Spectre.Console is the requested choice and fits the repo’s .NET stack cleanly.

## Decision 3: Load server URL and credentials explicitly from .NET user secrets

**Decision**: Give `EtpExplorer` its own strongly typed options model and explicit `AddUserSecrets<Program>()` configuration step, with the server URL and credentials stored under an `EtpExplorer` configuration section.

**Rationale**: The user explicitly wants server URL and credentials sourced from .NET secrets. The existing sample already uses this pattern successfully, and explicit loading avoids environment-dependent behavior for console apps.

**Alternatives considered**:
- Prompt for credentials interactively at startup: rejected because the requirement is to use .NET user secrets.
- Accept credentials on the command line: rejected because shell history and process listings are poor secret boundaries.
- Reuse checked-in appsettings for secrets: rejected because secrets must not live in source-controlled files.

## Decision 4: Reuse the existing library surface without adding new protocol-facing APIs in the first slice

**Decision**: Build the explorer on the current `EtpClient` methods: `ConnectAsync`, `DiscoverResourcesAsync`, `DescribeChannelsAsync`, `StartChannelStreamingAsync`, and `StopChannelStreamingAsync`.

**Rationale**: The library already exposes the async, typed workflows needed for the requested experience. Keeping the explorer additive avoids unnecessary public-API expansion and keeps protocol behavior centralized in the library.

**Alternatives considered**:
- Add explorer-specific convenience APIs to `EtpClient`: rejected because the sample can compose the existing primitives without changing the library contract.
- Reuse the existing sample’s `IEtpConnector` types directly: rejected as a first choice because those seams are sample-specific; the explorer can define its own app-local orchestration seam if testing needs one.

## Decision 5: Make browsing discovery-first, then resolve streamable endpoints through channel description

**Decision**: Use Protocol 3 Discovery first to enumerate the available root nodes, require the user to choose one root node such as `witsml14` or `witsml20`, then browse resources lazily within that selected branch and use Protocol 1 `ChannelDescribe` on the selected resource URI to resolve actual streamable channel definitions that can be added to the selection set.

**Rationale**: Discovery is the right protocol for traversal, and a live server may expose multiple top-level branches for different WITSML versions. Making root-node selection explicit keeps the user's context clear before deeper traversal begins. ChannelStreaming remains the right protocol for streamable channel metadata and session-scoped channel IDs once the user is browsing within the chosen branch.

**Alternatives considered**:
- Start browsing immediately from `eml://` without an explicit root-node choice: rejected because it obscures the version boundary between top-level branches and makes later navigation less intentional.
- Guess streamable channel URIs directly from discovery metadata alone: rejected because the explorer still needs channel IDs and channel metadata from Protocol 1.
- Describe every discovered URI eagerly: rejected because it adds unnecessary network traffic and slows navigation on large trees.
- Skip Discovery and require a known URI up front: rejected because the feature’s primary value is interactive browsing.

## Decision 6: Present the selected branch as a tree navigation experience

**Decision**: Model browsing after root selection as tree navigation, with the selected root node as the tree root, child resources rendered as navigable descendants, and clear affordances for moving deeper or back up the tree.

**Rationale**: After the user chooses a top-level root node, the remaining content is naturally hierarchical. A tree mental model makes the current location, parent/child relationships, and return paths clearer than a flat sequence of unrelated lists.

**Alternatives considered**:
- Treat each browse step as an isolated flat list with no tree context: rejected because users would lose track of where they are within the selected WITSML branch.
- Expand the entire tree eagerly on first load: rejected because large servers could make startup slow and the UI noisy.

## Decision 7: Maintain an explicit selection set and an explicit streaming mode

**Decision**: Separate the explorer flow into browse/select mode and active streaming mode. The user accumulates selected endpoints across one or more browsed resources, explicitly starts streaming, then explicitly stops or exits streaming before returning to browsing.

**Rationale**: This keeps navigation state stable, makes multi-selection understandable, and avoids mixing interactive menus with constantly updating live output in one ambiguous screen.

**Alternatives considered**:
- Start streaming immediately on every endpoint selection: rejected because it prevents deliberate multi-selection and makes partial failures harder to explain.
- Keep menus active while live output scrolls in the same view: rejected because it is harder to make robust and readable in a terminal UI.

## Decision 8: Test the explorer through orchestration and presentation seams, not a real terminal or live server

**Decision**: Add explorer tests that fake the ETP client/orchestrator inputs and verify navigation decisions, selection state, startup validation, stream lifecycle, and rendered output attribution using deterministic UI seams.

**Rationale**: The protocol wire behavior is already covered in the library. Explorer tests should prove that the new app uses those capabilities correctly and remains secret-safe and user-understandable.

**Alternatives considered**:
- Depend on live-server tests for explorer behavior: rejected because it would make the application tests slow and brittle.
- Skip presentation tests entirely: rejected because the feature’s core value is the interactive UX, not just underlying method calls.
