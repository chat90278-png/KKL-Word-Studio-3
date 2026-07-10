# Sprint 15 Integration Checklist — Serial–Quantity Grouped Table Semantics

Status values: `PASS`, `FAIL`, `PENDING`, `N/A` only. A `FAIL` on data preservation, contract drift, source mutability, or baseline regression blocks integration.

## 1. Baseline regression gate

- [ ] Use `tests/KKL.WordStudio.Architecture.Tests/TestData/sprint15-contract-baseline-tests.json` as the exact Sprint 15 Contract Baseline inventory.
- [ ] Baseline manifest reports **231 test methods / 43 test files / 0 skipped tests**.
- [ ] Report total integrated `[Fact]` / `[Theory]` methods.
- [ ] Report total skipped tests and list every skipped method.
- [ ] Report removed baseline files. Required result: none.
- [ ] Report removed or renamed baseline methods. Required result: none.
- [ ] Do not delete, rename, skip, or weaken an existing test to make integration green.

## 2. Frozen contract gate

- [ ] `TableElement.SerialQuantityGrouping` still exists and remains nullable.
- [ ] `SerialQuantityGrouping` role identity is three stable `Guid` column IDs: `MatchKeyColumnId`, `SerialNumberColumnId`, `QuantityColumnId`.
- [ ] `WasAutoDetected` remains `bool`.
- [ ] No persisted displayed-header, raw column-index, or Excel-letter grouping identity is added.
- [ ] `TableCellSpan` remains vertical-only with `RowIndex`, `ColumnIndex`, `RowSpan`.
- [ ] `TableRowGroup` remains `StartRowIndex`, `RowCount`, `KeepTogetherWhenPossible`.
- [ ] `TableRowCompositionResult` remains `Rows`, `CellSpans`, `RowGroups`, `Warnings`.
- [ ] `ITableContentRowComposer.Compose(TableElement, IReadOnlyList<IReadOnlyList<string>>)` remains synchronous.
- [ ] `TableContentNode.Rows` remains the semantic row matrix and retains `CellSpans`, `RowGroups`, `CompositionWarnings`.
- [ ] `TablePageBlockPayload.CellSpans` remains fragment-local.
- [ ] No duplicate Page/Table/Span/RowGroup contract is introduced in Engine, UI, or Infrastructure.

## 3. Team A review — table composition and auto-configuration

### Ownership

- [ ] Changed production files stay within `src/KKL.WordStudio.Application/TableComposition/**`, `ExcelReportTransferService.cs`, and the allowed Application DI registration exception.
- [ ] Team A tests are only new `*Sprint15*` Application tests.
- [ ] No frozen contract/bootstrap source file is edited.
- [ ] No Engine, WPF, OpenXML, Word writer, shell, or DOCX reverse-engineering implementation is present.

### Orchestration and role identity

- [ ] `ReportContentBuilder` still depends on `ITableContentRowComposer`; no serial/quantity alias set is added there.
- [ ] Single-source rows are sorted/projected before the composer call.
- [ ] Static detail rows pass through the composer.
- [ ] Multi-source rows are normalized through source-specific field mappings and appended in source order before one composer call.
- [ ] A multi-source `SourceError` partial node does not compose partial rows.
- [ ] Application DI has one effective `ITableContentRowComposer` registration and the bootstrap passthrough registration is replaced, not duplicated.
- [ ] Auto-detection examines `TableColumn.Header` and `SourceField` but persists only `TableColumn.Id` roles.
- [ ] Alias matching is case-insensitive and punctuation/space-insensitive, but not fuzzy `contains` matching.
- [ ] Missing or ambiguous role detection leaves `SerialQuantityGrouping` null.
- [ ] New table / Replace Columns detect against the new columns.
- [ ] Rebind preserves a valid existing grouping; otherwise detection may run.
- [ ] Add As Source preserves grouping and does not merge source-specific field mappings into role identity.
- [ ] Header rename after detection does not break grouping.

### Data-loss adversarial cases

Reject the implementation immediately if any of these are observed:

- [ ] `Quantity 100 + 0 serial` does **not** generate 100 rows.
- [ ] `Quantity 100 + 1 serial` does **not** generate 100 rows.
- [ ] `Quantity 3 + 2 serial` does **not** fabricate a third blank serial row.
- [ ] Conflicting Product Name/NSN for one PN/key does **not** choose an arbitrary first value.
- [ ] Conflicting valid quantity values do **not** choose an arbitrary first quantity.
- [ ] Malformed non-empty quantity does not silently collapse rows.
- [ ] Empty PN/key rows remain independent and are never grouped together.
- [ ] Duplicate serial values are not treated as distinct units.
- [ ] Unsafe/conflicting groups preserve original rows in original order.
- [ ] Aggregated serial values preserve source-row order, then line order within a cell.
- [ ] Serial splitting is newline-only; commas are preserved as data.
- [ ] Duplicate comparison is trimmed `OrdinalIgnoreCase`.
- [ ] Quantity accepts positive whole numbers and mathematically whole decimal representations under deterministic culture/invariant parsing.
- [ ] Quantity `<= 0` is not a valid grouping count.
- [ ] Zero/one serial token produces one row and no count-mismatch warning merely for being below Quantity.
- [ ] More than one serial with count mismatch produces one aggregated row, newline-preserved serials, no span/group, and a visible Turkish warning.
- [ ] Exact `Quantity > 1` plus equal distinct serial-token count produces one semantic row per serial and spans every safe non-serial column.
- [ ] Exact grouped rows emit one `TableRowGroup` with `KeepTogetherWhenPossible = true`.
- [ ] Group output order follows first appearance of non-empty match key; no implicit PN/key sort is added.

### Team A behavioral test review

- [ ] Exact 2/2 and 3/3 serial grouping tests assert rows, spans, groups, and serial order.
- [ ] Quantity 100/0 and 100/1 tests assert exactly one output row.
- [ ] Quantity 3/2 test asserts one row, two-line serial cell, warning, no fabricated row.
- [ ] Duplicate serial test asserts no grouped layout, duplicate occurrence preservation, warning.
- [ ] Conflicting product-data and conflicting-quantity tests assert original row preservation.
- [ ] Empty-key rows test asserts independence.
- [ ] Multi-source test proves grouping after normalization, not per source.
- [ ] Header rename test proves stable `TableColumn.Id` identity.

## 4. Team B review — Engine pagination and Preview rowspan

### Ownership and architecture

- [ ] Changes stay within assigned Engine Layout and Preview-only UI paths plus `*Sprint15*` Engine tests.
- [ ] No frozen contract edits.
- [ ] Engine has no `System.Windows`, OpenXML, provider/Excel access, `TableElement.Binding`, or `TableElement.Sources` access.
- [ ] Engine/Preview contain no Product/Serial/Quantity alias matching.
- [ ] Preview does not calculate page breaks or re-compose grouped rows.
- [ ] Preview does not calculate span intersection from complete-table indexes.
- [ ] Layout result collections are not mutated as report state.

### Engine row-group pagination

- [ ] Engine consumes both `TableContentNode.RowGroups` and `CellSpans`.
- [ ] If a `KeepTogetherWhenPossible` group fits a fresh body page but not current remainder, a new page starts before the group.
- [ ] If a group exceeds fresh body capacity, it splits only at physical row boundaries.
- [ ] Oversized group splitting always advances at least one row; no infinite-loop path exists.
- [ ] Cancellation is checked in outer fragment loops and inner row-measurement/split loops.
- [ ] Repeated table header remains on continuation fragments.
- [ ] Caption appears on the first table fragment only.
- [ ] Same semantic table fragments preserve the same `ElementId`.
- [ ] Complete semantic `TableContentNode` rows/spans/groups are never mutated.
- [ ] `CompositionWarnings` are surfaced in `DocumentLayoutResult.Warnings`; warnings do not become `SourceError`.

### Span clipping/restart

For every semantic span intersecting a page fragment:

- [ ] Intersection is computed in Engine, not UI.
- [ ] Continuation fragment copies the original semantic anchor-cell value into its first local intersecting row/cell.
- [ ] Fragment-local `TableCellSpan.RowIndex` is relative to `payload.Rows`.
- [ ] Every fragment-local span index is within `payload.Rows` and table column bounds.
- [ ] Every emitted fragment-local `RowSpan >= 2`.
- [ ] A one-row intersection emits no span but retains copied continuation anchor identity when applicable.
- [ ] Fragment payload spans originate from Engine projection, not direct un-clipped complete-table reuse.

### Preview renderer

- [ ] Table data-row rendering no longer relies on `UniformGrid` for rowspan.
- [ ] A focused WPF `Grid`/equivalent uses one row definition per payload row and one column definition per table column.
- [ ] Span anchors use `Grid.RowSpan` or equivalent true WPF grid spanning.
- [ ] Covered continuation cells are not rendered as duplicate visible cells.
- [ ] Merged content is vertically centered and borders remain visually continuous.
- [ ] Serial rows remain individually bordered.
- [ ] Data cells remain read-only.
- [ ] Displayed table-header editing remains.
- [ ] Caption editing remains.
- [ ] Split fragments keep the same `ElementId` selection/drag/delete behavior.
- [ ] Local property values do not override merge/selection style triggers.
- [ ] Dependency properties have the correct owner and CLR/WPF type.
- [ ] XAML event handlers exist and converter/control resources resolve.
- [ ] Custom table control does not capture mouse in a way that breaks parent block selection or drag.

### Team B behavioral test review

- [ ] Fresh-page keep-together test.
- [ ] Oversized-group progress/split test.
- [ ] Continuation anchor identity copy test.
- [ ] Fragment-local span-index bounds test.
- [ ] One-row continuation intersection test.
- [ ] Repeated header and first-fragment-only caption regression tests.
- [ ] Composition warning propagation test.

## 5. Team C review — Word vertical merge

### Ownership and semantic path

- [ ] Production change is limited to `WordTableWriter.cs`; tests are new Infrastructure `*Sprint15*` tests.
- [ ] No frozen contract edits.
- [ ] `WordTableWriter` consumes `TableContentNode.CellSpans` directly.
- [ ] No Product/Serial/Quantity alias matching exists in Word code.
- [ ] No `DocumentLayoutResult` or page-fragment span consumption is introduced.
- [ ] `WordExporter` still builds/consumes the shared `ReportContentDocument` semantic path.
- [ ] `SourceError` refusal remains.
- [ ] `altChunk` front-matter composition remains unchanged; no raw source `Body` append is introduced.
- [ ] Explicit Styles/Header/Footer save behavior remains.

### OpenXML table semantics

- [ ] Span anchor cell emits `w:vMerge` restart.
- [ ] Covered continuation cells emit `w:vMerge` continue (or SDK-equivalent correct continuation serialization).
- [ ] Semantic row index excludes the repeated Word header row; header is not accidentally merged into data spans.
- [ ] Every physical semantic row retains a `TableCell` for every table column, including covered continuation positions.
- [ ] Continuation cells are not removed from XML.
- [ ] Repeated table header remains.
- [ ] Full-width `5000` pct behavior remains.
- [ ] Fixed table layout remains.
- [ ] Equal/deterministic column strategy remains.
- [ ] Caption behavior remains.
- [ ] Word table remains editable.

### Team C behavioral test review

- [ ] Two-row span asserts one restart and one continue in the expected column.
- [ ] Three-row span asserts one restart and two continues.
- [ ] Multiple non-serial column spans are serialized independently.
- [ ] Serial column has no merge marker.
- [ ] Header row is not counted as semantic row 0 for span lookup.
- [ ] Every generated data row has full `TableCell` count.
- [ ] No-span tables retain prior XML shape/behavior.

## 6. Team D review — QA branch

- [ ] Changed files stay only in `tests/KKL.WordStudio.Architecture.Tests/**` and Sprint 15 Team D docs.
- [ ] No `src/**` production change.
- [ ] Sprint 15 baseline manifest was generated from the exact Contract Baseline before Team D additions.
- [ ] Frozen contract reflection guards compile.
- [ ] ReportContentBuilder orchestration guards compile.
- [ ] Full three-role alias-set confinement guard compiles.
- [ ] Engine/Preview/Word alias prohibition guard compiles.
- [ ] Quantity-driven row explosion consumer guard compiles.
- [ ] Engine RowGroups/CellSpans consumer gate compiles.
- [ ] Preview fragment-span/true-rowspan gate compiles.
- [ ] UI semantic span-intersection prohibition gate compiles.
- [ ] Word semantic span/VerticalMerge gate compiles.
- [ ] No test is skipped or weakened.

## 7. Cross-branch integration gate

- [ ] No duplicate serial/quantity composer exists outside Application TableComposition.
- [ ] No grouping occurs before all multi-source rows are normalized/appended.
- [ ] Preview and Word consume the same `TableContentNode` semantic span intent through their designated complete/fragment-local contracts.
- [ ] Engine alone owns span clipping/restart across page boundaries.
- [ ] Preview does not fake merge by blanking repeated values without true WPF rowspan.
- [ ] Word does not re-derive grouping from headers or serial/quantity counts.
- [ ] Grouping remains configured by stable `TableColumn.Id` after displayed-header rename.
- [ ] DI registrations are unique and complete.
- [ ] Existing Sprint 14 pagination/front-matter/selection/drag/delete behavior remains.
- [ ] Original XLSX/XLSM access remains read-only.
- [ ] Imported DOCX preview source remains read-only.
- [ ] No Office COM/Interop, WebView, or new PDF implementation is introduced.
- [ ] Project Explorer / `KAYNAK VERİ` integration remains deferred and unchanged.
- [ ] No DOCX-to-KKL reverse engineering types or mutations are introduced.

## 8. Integration-owned shell cleanup review

Apply only after Team A/B/C/D branch review:

- [ ] Permanent top-right toolbar has no `Dosyayı Aç`.
- [ ] Permanent top-right toolbar has no `Klasörü Aç`.
- [ ] Permanent top-right toolbar has no `● Canlı Önizleme` chip.
- [ ] `Word Dosyası Oluştur` remains.
- [ ] `OpenExportedFileCommand` and `OpenExportedFolderCommand` are not deleted.
- [ ] After successful Word export and only when `LastExportedFilePath` is available, compact status actions appear: `✓ Word dosyası oluşturuldu`, `[Dosyayı Aç]`, `[Klasörde Göster]`.
- [ ] Existing shell-launch behavior remains; no shell redesign is mixed into Sprint 15.

## 9. Mandatory manual Windows scenarios A–M

### A. EXACT TWO SERIAL

1. Load/create normalized data for the same PN `1234`:
   - `A222 / Qty 2`
   - `A221 / Qty 2`
2. Click `Word'e Aktar`.
3. Preview must show one logical product group and exactly two Serial No rows.
4. Product fields and Qty must be true vertically merged cells; serial cells remain separate.
5. Export Word.
6. Open DOCX and verify true Word vertical merge with separate serial cells.

Result: `PASS / FAIL / PENDING` — Notes:

### B. EXACT THREE SERIAL

`Qty 3 + 3 distinct serials` → exactly three serial rows; safe non-serial cells vertically merged in Preview and Word.

Result: `PASS / FAIL / PENDING` — Notes:

### C. QUANTITY 100, NO SERIAL

`Qty 100 + no serial` → exactly **one** row; no 100-row explosion; no serial-count mismatch warning merely because count is zero.

Result: `PASS / FAIL / PENDING` — Notes:

### D. QUANTITY 100, ONE SERIAL

`Qty 100 + one serial` → exactly **one** row containing that serial; no 100-row explosion; no serial-count mismatch warning merely because count is one.

Result: `PASS / FAIL / PENDING` — Notes:

### E. QUANTITY 3, TWO SERIAL

`Qty 3 + two serials` → exactly one row; serial cell contains both serials on two lines; visible Turkish mismatch warning; no fabricated third row; no span.

Result: `PASS / FAIL / PENDING` — Notes:

### F. DUPLICATE SERIAL

Provide duplicate serial occurrences for the same PN. Required: no grouped layout, observed duplicate values preserved in aggregated serial cell, visible duplicate/mismatch warning.

Result: `PASS / FAIL / PENDING` — Notes:

### G. CONFLICTING PRODUCT DATA

Same PN with different Product Name and/or NSN. Required: original source rows preserved in order, warning visible, no arbitrary first-value collapse.

Result: `PASS / FAIL / PENDING` — Notes:

### H. MULTI-SOURCE

Source A gives `A222`; Source B gives `A221`; same normalized PN and `Qty 2`. Required: grouping occurs **after** source-specific normalization and append, producing one exact two-serial group.

Result: `PASS / FAIL / PENDING` — Notes:

### I. PAGINATION

Create a serial group that fits a fresh page but not the current remainder. Required: entire group moves before itself to the next page; repeated header remains correct.

Result: `PASS / FAIL / PENDING` — Notes:

### J. OVERSIZED GROUP

Create a large exact serial group taller than fresh-page body capacity. Required: split by serial rows with progress; continuation page repeats/copies merged product identity; fragment-local spans are in range and use `RowSpan >= 2` only; repeated header remains.

Result: `PASS / FAIL / PENDING` — Notes:

### K. HEADER RENAME

After auto-detection, rename displayed `Product No` header (for example `Parça Numarası`). Required: grouping still works through persisted `TableColumn.Id`; no re-detection from displayed text is needed.

Result: `PASS / FAIL / PENDING` — Notes:

### L. SHELL CLEANUP AFTER INTEGRATION

Top bar: no permanent `Dosyayı Aç`, `Klasörü Aç`, or `Canlı Önizleme` chip; `Word Dosyası Oluştur` remains. After successful export, compact `Dosyayı Aç` / `Klasörde Göster` status actions appear.

Result: `PASS / FAIL / PENDING` — Notes:

### M. REGRESSION

- Front matter preview still works.
- Long A4 pagination still works.
- Preview selection across split fragments still selects the same real element.
- Preview drag/delete still mutate real report structure.
- Word `altChunk` front matter still works.
- Supported page/semantic order remains honest; no pixel-identical Word claim.

Result: `PASS / FAIL / PENDING` — Notes:

## 10. Final Windows command gate

Run from the integrated solution root on Windows with .NET 8/WPF-capable SDK:

```text
dotnet restore
dotnet build
dotnet test
```

Record exact output summaries. Do not report success from expected results or from another ZIP.

## 11. Acceptance record

| Gate | Result | Evidence / notes |
|---|---|---|
| Team A ownership | PENDING | |
| Team B ownership | PENDING | |
| Team C ownership | PENDING | |
| Team D ownership | PENDING | |
| Contract drift | PENDING | |
| Baseline manifest removed files | PENDING | Expected `0` |
| Baseline manifest removed/renamed methods | PENDING | Expected `0` |
| Total integrated tests | PENDING | |
| Skipped tests | PENDING | Expected `0` unless explicitly approved |
| `dotnet restore` | PENDING | |
| `dotnet build` warnings/errors | PENDING | |
| `dotnet test` total/pass/fail/skip | PENDING | |
| Manual A — exact two serial | PENDING | |
| Manual B — exact three serial | PENDING | |
| Manual C — Qty100/no serial | PENDING | |
| Manual D — Qty100/one serial | PENDING | |
| Manual E — Qty3/two serial | PENDING | |
| Manual F — duplicate serial | PENDING | |
| Manual G — conflicting product data | PENDING | |
| Manual H — multi-source | PENDING | |
| Manual I — pagination keep-together | PENDING | |
| Manual J — oversized group | PENDING | |
| Manual K — header rename | PENDING | |
| Manual L — shell cleanup | PENDING | |
| Manual M — regressions | PENDING | |

**Integration acceptance:** `PENDING`
