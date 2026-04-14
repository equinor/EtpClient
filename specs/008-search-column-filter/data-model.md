# Data Model: Search Active Explorer Column

## ExplorerSessionState

- **Purpose**: Captures the user-visible state for an explorer session across connection, browsing, endpoint selection, and streaming.
- **Relevant fields**:
  - `BrowseColumns`
  - `FocusedBrowseColumnIndex`
  - `NavigationStack`
  - `CurrentUri`
  - `LastStatusMessage`
- **Relationships**:
  - Owns zero or more `ExplorerBrowseColumn` instances.
  - References the currently active column by `FocusedBrowseColumnIndex`.
- **Validation rules**:
  - `FocusedBrowseColumnIndex` must remain within the available browse-column range.
  - Search/filter actions may only target the focused column.

## ExplorerBrowseColumn

- **Purpose**: Represents one visible browse pane and its underlying resource set.
- **Fields**:
  - `Title`
  - `ParentUri`
  - `Resources`: the full discovered set for this column.
  - `SelectedIndex`: the currently selected visible item, or `-1` when nothing is selectable.
  - `SearchTerm` or equivalent filter text state.
  - `VisibleResources` or equivalent derived view used for rendering and navigation.
- **Relationships**:
  - Belongs to one `ExplorerSessionState`.
  - Contains zero or more `BrowseableResource` items.
- **Validation rules**:
  - Clearing the search term restores `VisibleResources` to the full `Resources` set.
  - `SelectedIndex` must be clamped to the visible resource count after every filter update.
  - Search/filter state is independent per column.
- **State transitions**:
  - `Unfiltered` -> `Filtered` when a non-empty search term is applied.
  - `Filtered` -> `Unfiltered` when the term is cleared.
  - `FilteredWithMatches` -> `FilteredNoMatches` when the refined term removes all visible items.

## BrowseableResource

- **Purpose**: Represents one explorer item that may be opened or used to resolve endpoints.
- **Fields already present**:
  - `Uri`
  - `Name`
  - `ResourceType`
  - `ContentType`
  - `HasChildren`
  - `ChannelSubscribable`
  - `Depth`
  - `ParentUri`
- **Relationships**:
  - Appears in one or more derived visible lists within an `ExplorerBrowseColumn`.
- **Validation rules**:
  - Matching is evaluated against text already associated with the resource, primarily the displayed name and any chosen supporting text.

## ColumnFilterState

- **Purpose**: Logical state of a search/filter term applied to one browse column.
- **Fields**:
  - `Term`: raw user-entered search text.
  - `HasWildcard`: whether the term contains `*`.
  - `MatchCount`: number of currently visible items.
  - `IsActive`: whether the column is filtered.
  - `NoResults`: whether the current term yields zero matches.
- **Relationships**:
  - Derived from and attached to one `ExplorerBrowseColumn`.
- **Validation rules**:
  - `*` is the only wildcard token recognized.
  - Empty or whitespace-only terms are treated as no active filter.

## Selection Visibility Outcome

- **Purpose**: Defines what happens to the focused item when filtering changes the visible result set.
- **Cases**:
  - `Preserved`: the previously selected item still matches and stays focused.
  - `Reassigned`: the previous item no longer matches, so focus moves to the first visible result.
  - `Cleared`: no visible results remain, so the column has no focused item.
- **Usage**:
  - Applied whenever the user changes or clears the active column's search term.
