# KKL Word Studio — Sprint 15 Reviewed Integration Report

## 1. Branch diff / ownership decision

Integration baseline: `KKL.WordStudio-Sprint15-Contract-Baseline.zip` only.

Each branch was compared independently with that baseline by file path and SHA-256 before application.

| Branch | Added | Changed | Removed | Ownership result |
|---|---:|---:|---:|---|
| Team A — Composition | 6 | 2 | 0 | PASS |
| Team B — Layout/Preview | 3 | 6 | 0 | PASS |
| Team C — Word Merge | 2 | 1 | 0 | PASS |
| Team D — QA | 6 | 2 | 0 | PASS |

A/B/C production overlap: **0 files**. Team D production `src/**` changes: **0**.

The reviewed branch files were reapplied to the pristine Contract Baseline. No branch ZIP was used as a winning workspace and no ZIP was blindly overlaid over another branch. Except for the two integration-owned QA-compatibility edits described in section 6, accepted branch files are byte-identical to their reviewed branch versions.

No `CONTRACT_CHANGE_REQUEST-A/B/C/D.md` exists and no frozen-contract change was approved or applied.

## 2. Frozen contract hash / drift result

The frozen Sprint 15 Domain/Application table contracts, `ReportContentBuilder`, `TableContentNode`, layout payload contract, fallback engine compatibility path, bootstrap tests, ADR 0014, and bootstrap report were SHA-256 compared with the Contract Baseline.

Result: **PASS — 0 frozen contract drift**.

The same frozen files are byte-identical across Team A, B, C, and D branch ZIPs. Duplicate-definition scans found exactly one production definition each for:

- `SerialQuantityGrouping`
- `TableCellSpan`
- `TableRowGroup`
- `TableRowCompositionResult`
- `ITableContentRowComposer`

No second page/table/span/row-group contract was introduced.

The Contract Bootstrap's previously recorded Sprint 14 v3 provenance caveat remains inherited historical evidence; this integration does not reinterpret it as a green Windows run for the exact Sprint 15 baseline.

## 3. Team A composition integration

Accepted and reapplied the reviewed Team A Application changes.

Production DI now has exactly one effective registration:

`ITableContentRowComposer -> SerialQuantityTableContentRowComposer`

`PassthroughTableContentRowComposer` remains available for the direct one-argument `ReportContentBuilder` compatibility constructor and is not the production DI registration.

Preserved reviewed semantics include:

- exact normalized alias-set membership over `Header` and `SourceField`, without fuzzy `contains` role matching;
- stable `TableColumn.Id` role persistence;
- New Table / Replace Columns detection;
- valid grouping preservation during Rebind;
- Add As Source preservation;
- composition only after the normalized row stream is complete;
- newline-only serial splitting and source-row/line order preservation;
- deterministic positive whole-number quantity parsing without thousands separators;
- Qty 100 + zero/one serial producing one row rather than quantity-driven row expansion;
- Qty 3 + two serials producing one newline-aggregated row with a Turkish warning;
- duplicate serials refusing exact grouping while preserving duplicate observations;
- conflicting quantity or non-serial product data preserving original rows;
- empty match keys remaining independent;
- exact Qty > 1 / equal distinct serial count producing one row per serial, non-serial `TableCellSpan` values, and one `KeepTogetherWhenPossible` row group;
- first-appearance match-key order.

No grouping heuristics were added to Engine, Preview, or Word consumers.

## 4. Team B group pagination + fragment span integration

Accepted and reapplied the reviewed Team B Engine and Preview files.

Preserved Engine behavior:

- `CompositionWarnings` flow into layout warnings and identical warning text is deduplicated;
- `RowGroups` and complete semantic `CellSpans` are consumed by deterministic Engine layout;
- a keep-together group that fits a fresh page but not current remaining body area starts on a new page;
- an oversized group splits only at physical row boundaries with forward progress;
- semantic rows/spans/groups are not mutated;
- Engine computes complete-span/fragment intersection;
- continuation fragments copy the original semantic anchor value into the first local intersecting cell;
- fragment-local span indexes are relative to payload rows;
- `RowSpan = 1` is never emitted;
- repeated table headers, first-fragment-only captions, and same `ElementId` across fragments remain.

Preserved Preview behavior:

- `TablePageBlockPayload.CellSpans` projects directly to the Preview table ViewModel;
- table data rows use `PreviewTableGridControl` and a real WPF `Grid`;
- covered cells are not duplicated;
- merged cells use true WPF row span and remain vertically centered;
- data cells stay read-only;
- editable table header and caption paths remain;
- same-element selection, body-only structure gestures, Guid drag identity, and `IReportStructureService` mutation paths remain.

Integration clarity correction: `SetRowSpan(cell, rowSpan)` was changed only to the explicit equivalent `Grid.SetRowSpan(cell, rowSpan)`. Rowspan behavior is unchanged.

## 5. Team C Word vMerge integration

Accepted and reapplied the reviewed Team C `WordTableWriter` and focused tests.

The Word merge semantic input remains only `TableContentNode.CellSpans` from the complete semantic table. No header, serial, quantity, or grouping-role re-detection was introduced in Word code.

Preserved Word behavior:

- span anchor emits `VerticalMerge` restart;
- covered continuation cells emit continue;
- merged cells are vertically centered;
- semantic row index excludes the repeated Word header row;
- every data row retains the full calculated `TableCell` count;
- continuation cells remain physical cells in XML;
- invalid spans are ignored;
- overlapping ambiguous span candidates do not emit conflicting merge state;
- repeated header, `pct=5000`, fixed layout, deterministic equal widths, and editability remain.

`WordTableWriter` and Word export paths contain no `DocumentLayoutResult` dependency. Word remains on the shared `ReportContentDocument` semantic path.

## 6. Team D QA guard correction

Accepted Team D's Architecture test tree and Sprint 15 QA docs.

Applied the reviewed central correction only in:

`tests/KKL.WordStudio.Architecture.Tests/Sprint15SpanConsumerGuardTests.cs`

The Preview rowspan guard still requires:

- `CellSpans` consumption;
- `Grid.SetRowSpan` or `Grid.RowSpan` true rowspan semantics;
- `PreviewTableGridControl` in Preview XAML;
- `Rows="{Binding Rows}"` binding;
- `CellSpans="{Binding CellSpans}"` binding.

The old table-data-row `ItemsControl ItemsSource="{Binding Rows}" -> UniformGrid` shape is rejected by a scoped regex. The valid editable header `ItemsControl ItemsSource="{Binding Columns}" -> UniformGrid Rows="1"` is allowed.

This narrows the source scan to the frozen Sprint 15 data-row requirement; it does not remove or weaken the true-rowspan architecture gate.

## 7. Real-composer cross-branch pipeline tests

Added three integration-owned `[Fact]` tests that call the real `SerialQuantityTableContentRowComposer`; no grouping algorithm is duplicated in the tests.

### Composer -> Engine exact two serials

`Sprint15SerialQuantityPipelineTests.SerialQuantityPipeline_ExactTwoSerials_ComposerToEnginePreservesGroupedSpans`

Uses the real configured `TableColumn.Id` identities and normalized rows:

- `1 | Elma | 1234 | 321 | A222 | 2`
- `2 | Elma | 1234 | 321 | A221 | 2`

The test asserts real composition rows, serial order, one two-row group, non-serial spans, no Serial No span, then sends the real composition result into `DeterministicDocumentLayoutEngine`. It verifies same table `ElementId`, serial row order, valid fragment-local spans, `RowSpan >= 2`, and an unspanned serial column.

### Qty 100 / no serial -> Engine

`Sprint15SerialQuantityPipelineTests.SerialQuantityPipeline_Quantity100WithoutSerial_RemainsOneSemanticAndLayoutRow`

The real composer must return one row, no spans, and no groups. All Engine table fragments together must contain exactly one data row.

### Composer -> Word true vMerge

`Sprint15SerialQuantityWordPipelineTests.SerialQuantityPipeline_ExactTwoSerials_ComposerToWordWritesTrueVerticalMerge`

The real composition rows/spans are placed directly on `TableContentNode` and passed to the real `WordTableWriter.BuildTable` path. The test asserts:

- two data rows;
- six cells on each data row;
- columns 0, 1, 2, 3, and 5 restart/continue vMerge;
- column 4 has no vertical merge;
- `A222` and `A221` remain separate serial cells.

These tests create the direct semantic consumer bridges:

`Team A composer -> Team B Engine`

and

`Team A composer -> Team C Word writer`.

## 8. Shell cleanup

Applied the integration-owned narrow XAML patch in:

`src/KKL.WordStudio.UI/MainWindow.xaml`

Permanent top-right command bar now:

- does not contain `Dosyayı Aç`;
- does not contain `Klasörü Aç`;
- does not contain `● Canlı Önizleme`;
- retains `Word Dosyası Oluştur`.

The bottom status area was increased minimally from 26 px to 28 px. When `LastExportedFilePath` is non-empty, `StringToVisibility` shows compact actions:

`✓ Word dosyası oluşturuldu   Dosyayı Aç   Klasörde Göster`

Bindings remain:

- `OpenExportedFileCommand`
- `OpenExportedFolderCommand`

`MainViewModel.cs` is byte-identical to the Contract Baseline. `LastExportedFilePath`, both commands, existing shell launcher behavior, export failure clearing, and New/Open project clearing are preserved.

Project Explorer / `KAYNAK VERİ` integration remains unchanged and deferred.

## 9. DI / semantic path

Static DI gate result: **PASS**.

Exactly one effective Application registration:

`ITableContentRowComposer -> SerialQuantityTableContentRowComposer`

Exactly one reviewed content-builder registration:

`IReportContentBuilder -> ReportContentBuilder`

`ReportContentBuilder` retains the composer-aware constructor and the one-argument passthrough compatibility constructor. Production DI can select the composer-aware constructor.

Integrated semantic path:

```text
provider / static / complete multi-source normalized rows
                    ↓
            ReportContentBuilder
                    ↓
          ITableContentRowComposer
                    ↓
 TableContentNode.Rows / CellSpans / RowGroups / CompositionWarnings
                    ↓
          ┌─────────┴─────────┐
        Engine               Word
          ↓                    ↓
fragment-local spans      complete spans
          ↓                    ↓
Preview Grid.RowSpan      Word w:vMerge
```

Static consumer scans found no complete serial/quantity role-alias family in Engine, Preview, or Word, no duplicate composer contract, and no `DocumentLayoutResult` dependency in Word writers.

Sprint 14 preservation markers checked statically include read-only imported DOCX open semantics and absence of source DOCX/Spreadsheet write-open markers. No new Office Interop, WebView, PDF implementation, `IDocumentStructureAnalyzer`, or `DocumentImportProposal` marker was introduced in the integrated production diff.

## 10. Test inventory

Team D Sprint 15 baseline manifest header and entries:

- baseline test methods: **231**
- baseline test files: **43**
- baseline skipped: **0**

Integrated source inventory using the same attribute/method capture semantics as `TestInventory`:

- total `[Fact]` / `[Theory]` methods: **308**
- skipped tests: **0**
- removed baseline test files: **0**
- removed or renamed baseline test methods: **0**

Reviewed branch contribution:

- Contract Baseline: 231
- Team A: +35
- Team B: +12
- Team C: +12
- Team D: +15
- reviewed pre-integration: 305
- integration cross-branch tests: +3
- integrated total: **308**

Supplemental static integration gate: **117 / 117 PASS**. This gate covers reviewed diff ownership, frozen hashes, branch reapplication, baseline manifest comparison, DI uniqueness, duplicate contracts, consumer alias confinement, Engine dependency prohibitions, Preview data-row renderer shape, shell cleanup markers, XAML/csproj XML parsing, XAML handler resolution, read-only source-open markers, out-of-scope technology markers, and lexical delimiter balance on changed/added C# files.

Static/source validation is not reported as an xUnit runtime pass.

## 11. Restore / build / test actual output

The required commands were run from the integrated solution root in the available sandbox.

### `dotnet restore`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

Result: **NOT RUN / NOT GREEN — .NET CLI unavailable**.

### `dotnet build`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

Result: **NOT RUN / NOT GREEN — .NET CLI unavailable**.

### `dotnet test`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

Result: **NOT RUN / NOT GREEN — .NET CLI unavailable**.

A non-admin local SDK installation path was also investigated, but the environment could not retrieve the install script. Therefore this report does not claim restore, build, Architecture.Tests, full test, WPF/XAML compile, or runtime success.

Windows/.NET 8 WPF remains final build/runtime truth.

## 12. Manual smoke status

Mandatory manual Windows scenarios were **not run** because the prerequisite build/test green gate was not achieved in this environment.

| Scenario | Status |
|---|---|
| A — Qty 2 / two serials | PENDING |
| B — Qty 3 / three serials | PENDING |
| C — Qty 100 / no serial | PENDING |
| D — Qty 100 / one serial | PENDING |
| E — Qty 3 / two serials | PENDING |
| F — duplicate serial | PENDING |
| G — conflicting Product Name / NSN | PENDING |
| H — multi-source post-normalization grouping | PENDING |
| I — keep-together pagination | PENDING |
| J — oversized group continuation identity | PENDING |
| K — displayed header rename | PENDING |
| L — shell cleanup / post-export actions | PENDING |
| M — Sprint 14 regressions | PENDING |

No Preview or Word visual/runtime result is claimed from source inspection alone.

## 13. Warnings / remaining gaps

1. **Primary blocker:** the available sandbox has no .NET CLI. Exact Windows `.NET 8` `dotnet restore`, `dotnet build`, and `dotnet test` remain mandatory.
2. **WPF runtime validation pending:** true Preview rowspan appearance, merged-cell borders, parent selection/drag bubbling, and compact 28 px status actions require the Windows manual smoke.
3. **Word runtime validation pending:** the integrated tests are present, but real xUnit execution and manual Microsoft Word inspection of vMerge remain pending.
4. **Manual data-loss cases A–K pending:** source tests and reviewed composition semantics cover the requested cases, but the mandatory product-level Windows scenarios are not marked PASS without execution.
5. **Sprint 14 provenance note inherited:** the Contract Bootstrap report records that its supplied Sprint 14 v3 provenance was not itself an exact 222/222 Windows-green artifact. Sprint 15 integration preserves the supplied Contract Baseline and does not alter that historical evidence gap.
6. **Fidelity statement unchanged:** Preview/Word are not claimed pixel-identical to Microsoft Word.

Integration source/review result: **STATIC REVIEW PASS / WINDOWS ACCEPTANCE PENDING**.
