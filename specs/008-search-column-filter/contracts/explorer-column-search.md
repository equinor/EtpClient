# Explorer Column Search Contract

## Scope

This contract defines the user-facing behavior for searching and filtering within the currently focused browse column in the explorer application.

## Interaction Rules

- Search/filter operates only on the currently focused browse column.
- Other visible columns remain unchanged while a term is active.
- The user can refine, replace, or clear the term without restarting the browse workflow.

## Matching Rules

- Plain text matches item text in the focused column.
- `*` is supported as a wildcard token in the search term.
- Matching is case-insensitive.
- Empty input clears the current search/filter state.

## Result Rules

- Matching items remain selectable and openable.
- If the previously selected item still matches, it remains selected.
- If the previously selected item no longer matches and results remain, focus moves to the first visible match.
- If no items match, the column reports a no-results state and clears the focused item for that column.

## Feedback Rules

- The UI must make it clear when a filter is active on the focused column.
- The UI must distinguish between an empty browse column and a no-match filter result.
- Clearing the term restores the original visible list for that column without a new discovery request.
