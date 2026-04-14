# Phase 0 Research: Search Active Explorer Column

## Decision 1: Keep search/filter state on each browse column

- **Decision**: Extend the browse-column model so each `ExplorerBrowseColumn` carries its own search/filter state and can derive the currently visible items from its full resource list.
- **Rationale**: The spec requires filtering to affect only the currently selected column while preserving the rest of the browse context. Column-local state preserves that scope naturally and survives focus changes without inventing a global filter registry.
- **Alternatives considered**:
  - Keep a single search term on `ExplorerSessionState`. Rejected because it makes per-column preservation awkward and risks unintended cross-column coupling.
  - Recompute filtered results only inside `SpectreExplorerUi`. Rejected because selection/index behavior would become hard to test and harder for `ExplorerApp` to reason about.

## Decision 2: Preserve the full resource list and derive filtered visibility locally

- **Decision**: Keep the full discovered resource list for each column and derive filtered visibility from it instead of replacing the source list when a term is applied.
- **Rationale**: Clearing a term must restore the original list immediately without forcing another discovery request. Preserving the full list also makes wildcard refinement deterministic and keeps the feature purely local to the UI/session state.
- **Alternatives considered**:
  - Replace the column resource list with filtered results. Rejected because clearing the term would require cached copies elsewhere or another discovery call.
  - Rediscover from the server after every clear/refine action. Rejected because the feature is not a protocol operation and should not create extra network traffic.

## Decision 3: Support `*` as a simple wildcard with case-insensitive matching

- **Decision**: Treat `*` as the only wildcard token and apply case-insensitive matching over the item text presented in the active column.
- **Rationale**: This matches the spec precisely while keeping the matching model easy to explain and test. Case-insensitive matching is the least surprising behavior for interactive terminal search and avoids punishing minor casing differences in server-provided names.
- **Alternatives considered**:
  - Full regular expressions. Rejected because it exceeds the requested scope and complicates user feedback and escaping rules.
  - SQL-like `%` or `?` wildcards. Rejected because the spec explicitly names `*`.
  - Case-sensitive matching. Rejected because it creates avoidable misses in exploratory workflows.

## Decision 4: Reuse the current UI/test seams instead of introducing a new interaction layer

- **Decision**: Implement the behavior through the existing `SpectreExplorerUi` browse loop and extend `FakeExplorerUi`-based tests in `EtpExplorer.Tests` for deterministic coverage.
- **Rationale**: The current explorer already centralizes browse interaction in `PromptBrowseWorkspaceAsync` and captures browse snapshots in tests. Extending those seams is the lowest-risk path and keeps implementation aligned with the current architecture.
- **Alternatives considered**:
  - Introduce a separate search service or state store. Rejected because the feature scope is small and the current browse/session models are sufficient.
  - Add terminal-integration tests against the real Spectre console. Rejected because the repository already has a more stable fake UI seam for workflow verification.

## Decision 5: Define selection behavior explicitly when filtering changes visibility

- **Decision**: Preserve the current selection if the selected item remains visible after filtering; otherwise clamp selection to the first visible match or to an empty selection when there are no matches.
- **Rationale**: The spec requires consistent selection behavior. This hybrid rule keeps the user anchored when possible and gives deterministic recovery when the selected item falls out of the filtered set.
- **Alternatives considered**:
  - Always reset selection to the first row whenever the term changes. Rejected because it discards user context unnecessarily.
  - Preserve raw indices even when they no longer refer to visible items. Rejected because it creates invalid focused-state behavior.
