# Sprint 24 Tranche 06 — Structured Diagnostics Contract

## Baseline

- Base: `main@c2fb360b1071c099aaeceab8993b4e480cb01020`
- Branch: `sprint24/06-structured-diagnostics-contract`
- Closed/unmerged PR #18 is not reused or cherry-picked.
- Previous merged-main Windows gate: build `0/0`, tests `648/648`, pagination smoke GREEN.

## Goal

Replace message-text-owned diagnostics with stable semantic identity and make every warning card navigate to the actual affected Excel cell without changing the accepted Warning Center layout or creating a second validator.

## Structured diagnostics

- Added stable table-composition diagnostic codes for configuration, quantity, merge-conflict and serial findings.
- Added original message, record key and affected-column metadata.
- Existing `Warnings` / `CompositionWarnings` strings remain available for compatibility and support logs.
- Frozen Sprint 15 `TableRowCompositionResult` remains exactly `Rows`, `CellSpans`, `RowGroups`, `Warnings`.
- Structured projection is exposed at `TableContentNode.CompositionDiagnostics`.
- `PreviewDiagnosticFactory` consumes structured findings directly and no longer derives title/key from warning text.
- Production grouping uses `Code + GroupingKey`, not localized message templates.
- Different problem codes on the same table remain separate cards.
- Unrelated unknown legacy messages remain separate.

## Precise source navigation

The first UI smoke exposed that grouped cards navigated only by record key. For an `Adet` diagnostic this selected the `Parça Numarası` key cell rather than the actual invalid `Adet` cell. Editing the highlighted key could reduce the warning count indirectly while corrupting source identity.

Correction:

- Warning Center passes all available group keys and `AffectedColumn` to the Excel workspace.
- Excel navigation locates a trimmed exact key in the configured key column.
- Contains-search fallback is forbidden for diagnostics, so key `55` cannot navigate to `9555`.
- When a configured key column is available, the match must be in that column; an exact occurrence in another field is not accepted.
- Missing preferred matches are checked by collection count; a default `WorkingDataCell(0,0)` can never be mistaken for a real match.
- Navigation remains on the matched row and selects the affected column.
- Semantic alias resolution is delegated to the existing canonical `ExcelSemanticFieldMatcher`; UI does not own Product/Serial/Quantity alias sets.
- Exact conflict headers such as `Tr İsim` and `NSN` resolve directly.
- WorkingData matches `SourceField`, original Excel column, display header and stable column ID.
- Raw Preview navigation compensates for the hidden `#` row-number metadata column, preventing one-column-left drift.
- A hidden affected WorkingData column is restored before navigation so fallback indexing cannot select an unrelated visible column.
- If an affected column cannot be resolved, navigation safely falls back to the already validated key cell.

## Resolution feedback

- Group count and finding count are distinct concepts.
- Header displays `<problem type count> sorun türü · <occurrence count> açık bulgu`.
- Cards display `<n> açık bulgu`, not `<n> tekrar`.
- The true distinct-key count is retained even though only the first 25 keys are kept as the navigation window.
- Existing `NotifyReportContentChanged` remains the authoritative refresh path after WorkingData mutations.
- Fixing one affected cell reduces the open-finding count after Preview diagnostics rebuild.
- A card disappears only after every finding of that semantic type is resolved.

## Dash placeholder semantics

A supplied real-workbook smoke showed an NSN conflict for key `2354` where one row contained a real NSN and another row contained `-`. The composer previously treated the dash as a real value, so `554-415` and `-` were incorrectly classified as two conflicting NSNs.

Correction:

- For mergeable non-role fields such as NSN and descriptive columns, empty text and dash-only placeholders are treated as missing values.
- Supported placeholder forms are `-`, `–`, `—`, `‑` and `‒`.
- A real value plus a dash placeholder selects the real value and does not create a conflict card.
- Two different real values still create the existing merge-conflict diagnostic.
- Quantity and serial validation rules are unchanged; this is not a general warning suppression rule.

## Enter edit lifecycle correction

The full-scenario workbook produced the expected `7 sorun türü · 8 açık bulgu`, and editing affected cells reduced the counts correctly. A remaining WPF lifecycle defect was reproduced when the user pressed Enter after editing: `Specified argument was out of the range of valid values (Parameter 'index')` escaped through `DispatcherUnhandledException`, causing the generic startup-failure dialog and application shutdown.

Root cause and correction:

- `CellEditEnding` previously rebuilt `PreviewTable` while WPF DataGrid was still unwinding its edit transaction, leaving internal row/cell indexes stale.
- The handler now captures the row object, column identity and text, returns from `CellEditEnding`, then commits at `DispatcherPriority.Background`.
- The current display row index is resolved from the captured row object immediately before the mutation; an index captured before dispatcher deferral is never reused.
- Visual cell/row cancellation is best-effort cleanup and cannot prevent the authoritative working-data mutation from being attempted.
- Cell commit errors are surfaced in workspace status instead of escaping from an `async void` event path.
- Queued diagnostic/focus restoration validates the current item/column projection through `TryApplyGridCell`.
- A stale restore that reaches a rebuilt grid catches `ArgumentOutOfRangeException` / transient `InvalidOperationException` and is ignored rather than terminating the application.

## Boundaries

- No Domain or persistence changes.
- No automatic data repair.
- No warning suppression or persisted history.
- No second validator, renderer, paginator or exporter.
- Preview renderer, deterministic layout engine and Word writer remain unchanged.
- No rejected PR #18 export/control redesign is reintroduced.

## Regression coverage

Application coverage includes:

1. stable code/key/affected-column classification;
2. frozen composition-result compatibility;
3. factory semantic identity propagation;
4. grouping across localized messages;
5. separation of different codes on one table;
6. separation of unrelated unknown legacy warnings;
7. true distinct-key count beyond the 25-key navigation window;
8. dash placeholders ignored for mergeable fields while two real values still conflict.

The existing Sprint 20 architecture test identity is preserved and additionally requires:

- Warning Center to pass `KeyValues` and `AffectedColumn`;
- navigation to use the affected-column target;
- diagnostic keys to use trimmed exact matching without contains fallback;
- configured key-column matching to reject missing/default struct results;
- raw Preview column identity to compensate for hidden `#` metadata;
- runtime store to expose open-finding count;
- Enter commits to be deferred until after `CellEditEnding` through the dispatcher;
- deferred edits to resolve the current row index from stable row-object identity;
- DataGrid stale-selection restore to be bounds-safe.

## Expected test inventory

- Domain: `20`
- Application: `296`
- Engine: `68`
- Architecture: `126`
- Infrastructure: `146`

Expected total:

```text
656 / 656
```

## Supplied Windows evidence

The latest supplied Debug run reported:

- Build: `0 warnings / 0 errors`.
- Domain: `20/20`.
- Application: `295/295`.
- Engine: `68/68`.
- Architecture: `126/126`.
- Infrastructure: `146/146`.
- Total: `655/655`.

That run did not include `git rev-parse HEAD`, used Debug commands, and predates the dash-placeholder production change and its new Application test. It is therefore valid evidence for the superseded head only, not the current exact-head gate.

The supplied UI evidence confirms:

- the scenario workbook generates the expected grouped warning cards;
- editing an affected cell reduces open findings;
- the original NSN card for key `2354` was caused by comparing a real NSN with `-` as though both were real values.

The current exact-head Windows gate remains pending. The authoritative head is the PR head reported by GitHub at verification time.

## Exact-head Windows gate

```bat
git fetch origin
git checkout sprint24/06-structured-diagnostics-contract
git reset --hard origin/sprint24/06-structured-diagnostics-contract

git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

```text
git status --short: empty
0 warnings
0 errors
656 / 656 tests
```

## Required manual smoke

1. Open `KKL_Tum_Uyari_Senaryolari.xlsx` and confirm its intentional warning scenarios still appear.
2. Click the missing-quantity card and confirm the selected column is `Adet`, not `Parça Numarası`.
3. Enter a valid quantity and press Enter.
4. Confirm the application remains open and no startup/error dialog appears.
5. Confirm the open-finding count decreases after Preview refresh.
6. Click again and confirm navigation advances to another still-invalid `Adet` cell.
7. Click `Tr İsim`, real `NSN`, duplicate-serial and serial-count cards; confirm each selects its true affected column.
8. In the real workbook, confirm key `2354` no longer produces an NSN conflict when its group contains one real NSN and one `-` placeholder.
9. Change that placeholder to a different real NSN and confirm the NSN conflict card returns.
10. Hide `Adet`, click a quantity card, and confirm the column is restored and the correct cell is selected.
11. Resolve all intentional problem types and confirm the Warning Center reaches zero.
12. Export the healthy-control sheet and confirm Word generation is unchanged.
13. For a key-collision check, ensure a card for key `55` never navigates to a row containing only `9555`.

## Gate status

- Full-scenario warning generation: GREEN on supplied smoke.
- Warning recalculation after edit: GREEN on supplied smoke.
- Wrong-cell navigation: corrected in source; current exact-head re-smoke pending.
- Enter edit crash: reproduced and corrected in source; current exact-head re-smoke pending.
- Dash-only NSN placeholder false positive: reproduced and corrected in source; current exact-head re-smoke pending.
- Current exact-head Release build/test: pending.
- Final healthy export smoke: pending.
- PR remains draft.
