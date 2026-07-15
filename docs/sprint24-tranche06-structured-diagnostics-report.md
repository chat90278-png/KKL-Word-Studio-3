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
- Excel navigation locates an exact key in the configured key column.
- It then remains on that row and selects the affected column.
- Semantic alias resolution is delegated to the existing canonical `ExcelSemanticFieldMatcher`; UI does not own Product/Serial/Quantity alias sets.
- Exact conflict headers such as `Tr İsim` and `NSN` resolve directly.
- WorkingData matches `SourceField`, original Excel column, display header and stable column ID.
- Raw Preview navigation compensates for the hidden `#` row-number metadata column, preventing one-column-left drift.
- A hidden affected WorkingData column is restored before navigation so fallback indexing cannot select an unrelated visible column.
- If an affected column cannot be resolved, navigation safely falls back to the matched key cell.

## Resolution feedback

- Group count and finding count are distinct concepts.
- Header displays `<problem type count> sorun türü · <occurrence count> açık bulgu`.
- Cards display `<n> açık bulgu`, not `<n> tekrar`.
- The true distinct-key count is retained even though only the first 25 keys are kept as the navigation window.
- Existing `NotifyReportContentChanged` remains the authoritative refresh path after WorkingData mutations.
- Fixing one affected cell reduces the open-finding count after Preview diagnostics rebuild.
- A card disappears only after every finding of that semantic type is resolved.

## Enter edit lifecycle correction

The full-scenario workbook produced the expected `7 sorun türü · 8 açık bulgu`, and editing affected cells reduced the counts correctly. A remaining WPF lifecycle defect was reproduced when the user pressed Enter after editing: `Specified argument was out of the range of valid values (Parameter 'index')` escaped through `DispatcherUnhandledException`, causing the generic startup-failure dialog and application shutdown.

Root cause and correction:

- `CellEditEnding` cancelled the visual edit and awaited a working-data mutation immediately.
- That mutation rebuilt `PreviewTable` while WPF DataGrid was still unwinding its edit transaction, leaving internal row/cell indexes stale.
- The handler now captures row, column and text, returns from `CellEditEnding`, then commits at `DispatcherPriority.Background`.
- The visual cell/row edit is cancelled only after the original edit event has completed.
- Cell commit errors are surfaced in the workspace status instead of escaping from an `async void` event path.
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
7. true distinct-key count beyond the 25-key navigation window.

The existing Sprint 20 architecture test identity is preserved and additionally requires:

- Warning Center to pass `KeyValues` and `AffectedColumn`;
- navigation to use the affected-column target;
- raw Preview column identity to compensate for hidden `#` metadata;
- runtime store to expose open-finding count;
- Enter commits to be deferred until after `CellEditEnding` through the dispatcher;
- DataGrid stale-selection restore to be bounds-safe.

## Expected test inventory

- Domain: `20`
- Application: `295`
- Engine: `68`
- Architecture: `126`
- Infrastructure: `146`

Expected total:

```text
655 / 655
```

## Supplied Windows evidence

The latest supplied command log before the Enter correction reported:

- Build: failed with `0 warnings / 2 errors`.
- Both errors were `CS0121` because a temporary diagnostic `IReadOnlyList.IndexOf` extension conflicted with the existing Quick Assembly extension.
- Domain: `20/20`.
- Application: `295/295`.
- Engine: `68/68`.
- Architecture: `125/126`; the only failure detected a complete Product/Serial/Quantity alias set in UI diagnostics.
- Infrastructure: `146/146`.

Corrections:

- Removed the duplicate diagnostic `IndexOf` extension and used a private non-extension list search.
- Removed role alias sets from UI and reused `ExcelSemanticFieldMatcher`.

The subsequent UI screenshots confirm:

- the scenario workbook generates exactly `7 sorun türü · 8 açık bulgu`;
- the seven expected cards are visible;
- editing an affected cell reduces open findings;
- pressing Enter reproduced the DataGrid index crash described above.

All supplied automated evidence belongs to superseded heads. The current exact-head Windows gate remains pending.

## Exact-head Windows gate

Current exact head before any further correction:

```text
f7dbb28bdeb0289ecaba77813bd04ee3ddecec51
```

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
0 warnings
0 errors
655 / 655 tests
```

## Required manual smoke

1. Open `KKL_Tum_Uyari_Senaryolari.xlsx` and confirm `7 sorun türü · 8 açık bulgu`.
2. Click the missing-quantity card and confirm the selected column is `Adet`, not `Parça Numarası`.
3. Enter a valid quantity and press Enter.
4. Confirm the application remains open and no startup/error dialog appears.
5. Confirm the open-finding count decreases after Preview refresh.
6. Click again and confirm navigation advances to another still-invalid `Adet` cell.
7. Click `Tr İsim`, `NSN`, duplicate-serial and serial-count cards; confirm each selects its true affected column.
8. Hide `Adet`, click a quantity card, and confirm the column is restored and the correct cell is selected.
9. Resolve all seven problem types and confirm the Warning Center reaches zero.
10. Export the healthy-control sheet and confirm Word generation is unchanged.

## Gate status

- Full-scenario warning generation: GREEN (`7` types / `8` findings).
- Warning recalculation after edit: GREEN in supplied smoke.
- Wrong-cell navigation: corrected in source; exact-head re-smoke pending.
- Enter edit crash: reproduced and corrected in source; exact-head re-smoke pending.
- Current exact-head Release build/test: pending.
- Final healthy export smoke: pending.
- PR remains draft.
