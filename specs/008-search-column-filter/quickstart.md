# Quickstart: Search Active Explorer Column

## Goal

Verify that the explorer can search and filter the currently focused browse column, including `*` wildcard matching, without changing broader browse context.

## Prerequisites

- .NET 10 SDK installed.
- Existing explorer sample builds successfully.
- Existing explorer tests are runnable from the repository root.

## Manual Validation Flow

1. Build the explorer sample.
2. Start the explorer and connect using the existing configuration flow.
3. Select a root node and navigate to a browse column with several visible items.
4. Enter a plain-text search term for a known item and verify that the active column narrows or highlights results without changing other columns.
5. Enter a wildcard pattern using `*` and verify that matching items follow the pattern.
6. Confirm that a previously selected item stays selected when it still matches the term.
7. Apply a term that hides the selected item and verify that focus moves predictably or clears when there are no results.
8. Clear the term and verify that the full original list returns without reconnecting or rediscovering.
9. Continue browsing after clearing the term to confirm that normal navigation still works.

## Automated Validation Flow

1. Run the explorer test project.
2. Confirm coverage includes:
   - plain-text match behavior
   - wildcard match behavior
   - no-result feedback
   - clearing/restoring the full list
   - active-column-only scoping
   - selection preservation or reassignment during filtering

## Expected Outcome

- Search/filter feels local to the active column.
- Wildcard `*` support behaves consistently.
- Existing browse, endpoint selection, and streaming workflows remain intact.

## Acceptance Validation for SC-001

SC-001 requires a user to locate a known item in a populated active column in under 15 seconds.

**Steps (timed)**:

1. Start a stopwatch.
2. Connect to any ETP server with the explorer and navigate to a browse column that contains at least five items.
3. Press `/` to open the search prompt.
4. Type a search term for a known item (e.g., part of its name).
5. Press Enter to apply the search.
6. Confirm the matching item appears and is focused in the active column.
7. Stop the stopwatch.

**Pass criterion**: The known item is visible and reachable in under 15 seconds from the moment the user presses `/`.
