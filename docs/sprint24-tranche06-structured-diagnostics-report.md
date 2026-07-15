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
- Quantity aliases resolve to `Adet` / `Miktar` / `Quantity` / `Qty`.
- Serial aliases resolve to `Seri No` / `Seri Numarası` / English equivalents.
- Exact conflict headers such as `Tr İsim` and `NSN` resolve directly.
- WorkingData matches `SourceField`, original Excel column, display header and stable column ID.
- Raw Preview navigation compensates for the hidden `#` row-number metadata column, preventing one-column-left drift.
- A hidden affected WorkingData column is restored before navigation so fallback indexing cannot select an unrelated visible column.
- If an affected column cannot be resolved, navigation safely falls back to the matched key cell.

## Resolution feedback

- Group count and finding count are now distinct concepts.
- Header displays `<problem type count> sorun türü · <occurrence count> açık bulgu`.
- Cards display `<n> açık bulgu`, not `<n> tekrar`.
- The true distinct-key count is retained even though only the first 25 keys are kept as the navigation window.
- Existing `NotifyReportContentChanged` remains the authoritative refresh path after WorkingData mutations.
- Fixing one affected cell must reduce the open-finding count after Preview diagnostics rebuild.
- A card disappears only after every finding of that semantic type is resolved.

## Boundaries

- No Domain or persistence changes.
- No automatic data repair.
- No warning suppression or persisted history.
- No second validator, renderer, paginator or exporter.
- Preview renderer, deterministic layout engine and Word writer remain unchanged.
- No rejected PR #18 export/control redesign is reintroduced.

## Regression coverage

Application coverage now includes:

1. stable code/key/affected-column classification;
2. frozen composition-result compatibility;
3. factory semantic identity propagation;
4. grouping across localized messages;
5. separation of different codes on one table;
6. separation of unrelated unknown legacy warnings;
7. true distinct-key count beyond the 25-key navigation window.

The existing Sprint 20 architecture test identity is preserved and now additionally requires:

- Warning Center to pass `KeyValues` and `AffectedColumn`;
- navigation to use the affected-column target;
- raw Preview column identity to compensate for hidden `#` metadata;
- runtime store to expose open-finding count.

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

## Supplied Windows evidence before precise-cell correction

The latest supplied Debug run reported:

- Build: `0 warnings / 0 errors`
- Domain: `20/20`
- Application: `294/294`
- Engine: `68/68`
- Architecture: `126/126`
- Infrastructure: `146/146`
- Total: `654/654`

That automated result belongs to the earlier head. The subsequent UI evidence invalidated the product gate because warning cards selected key cells rather than affected cells.

The screenshots showed `245 → 244` after editing the selected `Parça Numarası` cell. This was not a valid warning resolution; it demonstrated the wrong-cell navigation defect.

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
0 warnings
0 errors
655 / 655 tests
```

## Required manual smoke

1. Click the missing-quantity card.
2. Confirm the selected row belongs to the shown PN/key and the selected column is `Adet`, not `Parça Numarası`.
3. Enter a valid quantity in that selected `Adet` cell.
4. Confirm the card's `açık bulgu` count decreases after Preview refresh.
5. Click again and confirm navigation advances to another still-invalid `Adet` cell.
6. Click the `Tr İsim` conflict card and confirm the `Tr İsim` cell is selected.
7. Click the `NSN` conflict card and confirm the `NSN` cell is selected.
8. Hide `Adet`, click the quantity card, and confirm the column is restored and the correct cell is selected.
9. Confirm the header distinguishes problem types from total open findings.
10. Confirm Preview report-element navigation still works.
11. Export a healthy document and confirm Word generation is unchanged.

## Gate status

- Previous Debug build/test: `0/0`, `654/654` on superseded head.
- Wrong-cell navigation: reproduced and corrected in source.
- Current exact-head Release build/test: pending.
- Correct affected-cell navigation smoke: pending.
- Warning resolution smoke: pending.
- Final export smoke: pending.
- PR remains draft.
