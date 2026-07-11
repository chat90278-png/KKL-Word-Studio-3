# Sprint 20 — Diagnostics Center and Cross-Navigation

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `0645b5f872e27ea9818fc4ef9df9f6312a78fa19`
- Working branch: `sprint20/diagnostics-cross-navigation`
- Windows restore/build/test output remains execution truth.

## Product problem

Preview warnings are currently flattened into one long single-line string in the Preview toolbar. This makes the toolbar unreadable and gives the user no direct path to the affected report table or Excel source row.

## P0-A — Dedicated diagnostics dock page

- add `Uyarılar` next to `İçindekiler` and `Özellikler` in the right Context Dock;
- render warnings as separate compact cards with title, message and source context;
- show a count badge;
- remove the long warning concatenation from the Preview toolbar;
- keep only short interaction/reference/front-matter status text in the toolbar;
- add a compact warning-count action in Preview that opens the diagnostics page.

## P0-B — Structured preview diagnostics

- extend `PreviewSnapshot` with structured diagnostics;
- preserve the original human-readable warning text;
- attach report element id/name when the warning originates from a table;
- attach Excel source candidates (data source name, source path, worksheet and optional range);
- extract `PN/key '<value>'` as an optional navigation key without changing the existing composition warning contract;
- include generic layout/front-matter warnings even when no report/source target exists;
- deduplicate diagnostics deterministically.

## P0-C — Preview navigation

Clicking a warning with an element target must:

- select the report element in the shared workspace;
- scroll Preview to the first fragment of the element;
- bring the relevant page into view;
- use existing selection overlay as the visual highlight;
- keep Engine geometry authoritative and avoid repagination in WPF.

## P0-D — Excel source navigation

When source metadata exists, clicking a warning must:

- activate/open the relevant workbook;
- activate the bound worksheet;
- reuse the existing Excel preview-loading path;
- search the current source for the extracted key;
- select, scroll to and focus the first matching cell;
- preserve the existing DataGrid keyboard-navigation behavior;
- fall back to the source/sheet even when the exact key cannot be located.

For multi-source tables, source candidates are tried in persisted table-source order until a matching key is found.

## Closure gate

1. Exact current-head Windows:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - 0 warnings / 0 errors and no skipped/weakened tests.
2. Warning-center smoke:
   - long Preview toolbar warning text is gone;
   - `Uyarılar (n)` shows one card per warning;
   - generic warnings remain readable.
3. Navigation smoke:
   - clicking a PN/key warning selects and scrolls to the affected Preview table;
   - the related workbook/sheet becomes active;
   - the matching Excel cell becomes selected and focused;
   - arrow-key navigation continues without another click.
4. No Preview/Word output semantics change.

## Non-goals

- automatic data repair;
- suppressing or weakening existing composition warnings;
- persistent warning history;
- changing Serial/Quantity grouping decisions;
- moving pagination into UI;
- modifying original Excel files.
