# Sprint 15 Team D QA / Architecture Report

## 1. Role and scope

Team D operated as QA / data-loss adversary / architecture compliance for Sprint 15 Serial–Quantity grouped-table semantics.

Production feature implementation was not performed. No `src/**` file was modified.

Allowed Team D changes are limited to:

- `tests/KKL.WordStudio.Architecture.Tests/**`
- `docs/sprint15-team-d-qa-report.md`
- `docs/sprint15-integration-checklist.md`

No `docs/CONTRACT_CHANGE_REQUEST-D.md` was created because no frozen-contract architecture flaw was found.

## 2. Exact Sprint 15 baseline manifest

The manifest was generated from the exact supplied `KKL.WordStudio-Sprint15-Contract-Baseline.zip` before Team D test additions.

Manifest:

`tests/KKL.WordStudio.Architecture.Tests/TestData/sprint15-contract-baseline-tests.json`

Recorded Contract Baseline inventory:

- baseline test methods: **231**
- baseline test files containing `[Fact]` / `[Theory]`: **43**
- skipped baseline tests: **0**

The manifest records every baseline test file path and every detected `[Fact]` / `[Theory]` method name.

Team D updated the reusable inventory comparison to report:

- total current/integrated test methods
- current skipped tests
- baseline method count
- baseline test-file count
- baseline skipped-test count
- removed baseline files
- removed or renamed baseline methods

Static inventory comparison on the Team D workspace:

- current test methods: **246**
- current Architecture test methods: **37**
- current skipped tests: **0**
- removed baseline files: **0**
- removed/renamed baseline methods: **0**

The original baseline inventory test method name was retained so the baseline manifest does not falsely report its own rename as a regression.

## 3. Frozen Sprint 15 contract guards

Added `Sprint15FrozenContractGuardTests.cs`.

Executable reflection/compile-time guards cover:

### Domain

- `TableElement.SerialQuantityGrouping`
- exact `SerialQuantityGrouping` public surface:
  - `MatchKeyColumnId : Guid`
  - `SerialNumberColumnId : Guid`
  - `QuantityColumnId : Guid`
  - `WasAutoDetected : bool`

This makes displayed-header/index identity drift visible.

### Application Tables

Exact public contract shape is guarded for:

- `TableCellSpan.RowIndex / ColumnIndex / RowSpan`
- `TableRowGroup.StartRowIndex / RowCount / KeepTogetherWhenPossible`
- `TableRowCompositionResult.Rows / CellSpans / RowGroups / Warnings`
- `ITableContentRowComposer.Compose(TableElement, IReadOnlyList<IReadOnlyList<string>>)` returning `TableRowCompositionResult`

### Shared semantic table path

Guards verify:

- `TableContentNode.Rows` remains present
- `TableContentNode.CellSpans`
- `TableContentNode.RowGroups`
- `TableContentNode.CompositionWarnings`
- `TablePageBlockPayload.Rows`
- `TablePageBlockPayload.CellSpans`

No Team A/B/C implementation class name is required by these frozen-contract tests.

## 4. ReportContentBuilder orchestration guards

Added `Sprint15OrchestrationAndHeuristicGuardTests.cs`.

The guards require:

- `ReportContentBuilder` depends on `ITableContentRowComposer`
- one successful semantic composition call boundary
- successful single-source, static, and multi-source table paths route through `BuildComposedTableNode`
- multi-source source iteration occurs before normalized `renderedRows.Add(...)`
- the final composer path occurs after normalized rows are appended
- `BuildMultiSourceErrorNode` does not compose partial rows
- composition result rows/spans/groups/warnings populate `TableContentNode`
- `ReportContentBuilder` contains no Product/Serial/Quantity alias family

Static source inspection of the Contract Baseline found the expected bootstrap orchestration markers:

- `_tableContentRowComposer.Compose(...)` call count: **1**
- successful `return BuildComposedTableNode(...)` paths: **3**

## 5. Heuristic confinement and data-loss guards

The Team D source guards treat grouping heuristics as semantic composition logic rather than consumer logic.

### Full alias-set confinement

A production file outside `src/KKL.WordStudio.Application/TableComposition/**` is rejected if it contains a complete match-key + serial + quantity alias-family set.

This is intentionally narrower than rejecting an isolated word such as `Quantity`, because existing unrelated data-range detection may legitimately recognize generic data headers. The guard is aimed at duplicated Serial–Quantity grouping heuristics.

Static Contract Baseline scan:

- complete grouping alias-set offenders outside Application TableComposition: **0**

### Consumer alias prohibition

Engine, Preview, and Word consumer paths are scanned for Product/Serial/Quantity role alias literals.

Static Contract Baseline scan:

- Engine/Preview/Word alias-family offenders: **0**

### Quantity-driven row explosion guard

Non-composition consumers are scanned for suspicious `Enumerable.Range` / `Enumerable.Repeat` or `for`-loop row generation driven by quantity/adet/qty identifiers.

Static Contract Baseline scan:

- quantity-driven consumer row-expansion offenders: **0**

The exact data-loss behavioral cases remain mandatory Team A behavioral tests and integration manual scenarios. They are enumerated in the integration checklist, including:

- Quantity 100 + zero serial
- Quantity 100 + one serial
- Quantity 3 + two serials
- duplicate serials
- conflicting product data
- conflicting quantities
- empty match keys
- multi-source post-normalization grouping

## 6. Engine / span / Preview / Word integration consumer guards

Added `Sprint15SpanConsumerGuardTests.cs`.

These tests are deliberately hard integration gates. The Contract Baseline contains bootstrap contracts but not Team B/C implementations, so three consumer implementation gates are expected to remain red until reviewed Team B/C outputs are integrated.

### Engine gate

Requires deterministic Engine Layout production source to:

- consume `RowGroups`
- consume `CellSpans`
- work with `TableCellSpan`
- assign fragment-local `TablePageBlockPayload.CellSpans`
- surface `CompositionWarnings`

Static Contract Baseline state:

- `RowGroups`: absent from deterministic Layout implementation
- `CellSpans`: absent from deterministic Layout implementation
- `CompositionWarnings`: absent from deterministic Layout implementation

**Pre-integration status: expected RED / Team B implementation pending.**

The existing fallback bootstrap propagation is deliberately excluded from this gate; fallback pass-through is not equivalent to group-aware pagination and span clipping/restart.

### Preview gate

Requires Preview table rendering to:

- consume fragment-local `CellSpans`
- use true WPF `Grid.RowSpan` / `Grid.SetRowSpan`
- stop relying on `UniformGrid` for the table row renderer

A separate prohibition guard rejects complete-semantic span intersection reconstruction in Preview.

Static Contract Baseline state:

- Preview `CellSpans` consumption: absent
- true Grid rowspan marker: absent
- `UniformGrid`: still present in Preview table XAML
- complete-semantic span-intersection logic in UI: **0 offenders**

**Pre-integration implementation gate: expected RED / Team B implementation pending.**

### Word gate

Requires `WordTableWriter` to:

- consume `tableNode.CellSpans`
- use OpenXML `VerticalMerge`
- contain restart and continue merge semantics
- retain repeated table header and fixed table-layout markers

Static Contract Baseline state:

- `tableNode.CellSpans`: absent
- `VerticalMerge`: absent
- restart/continue markers: absent

**Pre-integration implementation gate: expected RED / Team C implementation pending.**

The semantic-path guard already confirms the baseline Word exporter remains on `IReportContentBuilder -> ReportContentDocument` content and does not consume `DocumentLayoutResult`.

## 7. Preview semantic-index prohibition

Team D added a dedicated source guard rejecting Preview-side complete-table span intersection calculations using markers such as `intersectionStart` / `intersectionEnd` or `Math.Max` / `Math.Min` combinations over semantic `StartRowIndex` and span indexes.

Static Contract Baseline scan:

- Preview complete-semantic span-intersection offenders: **0**

The UI must consume fragment-local `TablePageBlockPayload.CellSpans` after Team B integration.

## 8. Word regression review requirements

Executable stable markers plus checklist review protect:

- `ReportContentDocument` semantic path
- no `DocumentLayoutResult` in Word writer/export path
- repeated `TableHeader`
- fixed table layout
- existing altChunk composition
- explicit Styles/Header/Footer save behavior through existing stabilization guards

The integration checklist additionally requires behavioral verification that:

- the Word header row is excluded from semantic span row indexes
- every physical semantic row retains full `TableCell` count
- covered cells remain present and use vMerge continue
- serial cells remain unmerged

These XML-shape behaviors primarily belong in Team C `*Sprint15*` tests.

## 9. Integration checklist

Created:

`docs/sprint15-integration-checklist.md`

It includes:

- Sprint 15 baseline regression gate
- frozen contract gate
- Team A ownership/orchestration/data-loss review
- Team B Engine/span/Preview review
- Team C Word vMerge review
- Team D ownership/test review
- cross-branch architecture gate
- integration-owned shell cleanup review
- exact manual Windows scenarios A–M
- final restore/build/test command gate
- explicit acceptance record for ownership, contract drift, baseline removals, build/test totals, and scenarios A–M

The checklist explicitly rejects:

- Quantity-driven blank-row explosions
- fabricated serial rows
- duplicate-serial equality claims
- silent first-value collapse for conflicting product or quantity data
- grouping empty keys together
- grouping before multi-source normalization
- Preview-only fake merges
- Word-only header alias grouping
- invalid fragment-local spans
- page-boundary merged-cell identity loss

## 10. Team D ownership verification

Compared with a pristine extraction of the supplied Sprint 15 Contract Baseline.

Modified:

- `tests/KKL.WordStudio.Architecture.Tests/BaselineRegressionInventoryTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/TestInventory.cs`

Added:

- `tests/KKL.WordStudio.Architecture.Tests/Sprint15FrozenContractGuardTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/Sprint15OrchestrationAndHeuristicGuardTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/Sprint15SpanConsumerGuardTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/TestData/sprint15-contract-baseline-tests.json`
- `docs/sprint15-team-d-qa-report.md`
- `docs/sprint15-integration-checklist.md`

Removed: **none**.

Production `src/**` changes: **0**.

Team D ownership result: **PASS by pristine-file hash/path comparison**.

## 11. Static validation performed

Because `.NET` is unavailable, static validation does not substitute for build/test.

Performed:

- all `.csproj` files parsed successfully as XML
- pristine baseline vs Team D workspace file comparison
- exact baseline manifest count/file/skip calculation
- baseline removed-file comparison
- baseline removed/renamed-method comparison
- current source test inventory count
- source delimiter-balance sanity check on Team D changed/added C# files
- ReportContentBuilder composer/call-path marker scan
- grouping alias-set confinement scan
- Engine/Preview/Word alias prohibition scan
- quantity-driven consumer row-expansion scan
- Preview complete-semantic span-intersection prohibition scan
- pre-integration Engine/Preview/Word consumer marker inventory

Static inventory summary:

- baseline methods: **231**
- baseline files: **43**
- baseline skip: **0**
- current methods: **246**
- Architecture test methods: **37**
- current skip: **0**
- removed baseline files: **0**
- removed/renamed baseline methods: **0**
- production changes: **0**

## 12. Required command verification

Commands were attempted from the Team D solution root.

### `dotnet restore`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

Result: **NOT RUN — .NET CLI unavailable**.

### `dotnet build`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

Result: **NOT RUN — .NET CLI unavailable**.

### `dotnet test`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

Result: **NOT RUN — .NET CLI unavailable**.

No restore/build/test success claim is made.

Architecture.Tests pass/fail totals are unavailable because xUnit could not execute. The pre-integration source inspection described above is not reported as an xUnit test run.

Windows/.NET 8 verification is **PENDING**.

## 13. Contract change request decision

No real frozen-contract architecture flaw was identified.

The complete semantic span versus fragment-local span split is sufficient for Team A/B/C integration:

- Application composer owns row grouping semantics
- Engine owns row-group pagination and span clipping/restart
- Preview consumes fragment-local spans
- Word consumes complete semantic spans

`docs/CONTRACT_CHANGE_REQUEST-D.md` was therefore **not created**.

## 14. QA conclusion

Sprint 15 Team D guardrails are ready for parallel branch integration.

The most important integration behavior is intentionally adversarial: the Engine, Preview, and Word implementation gates do not accept bootstrap pass-through, blank-cell visual imitation, or header-name re-derivation as grouped-table support.

Final acceptance remains blocked until reviewed Team A/B/C implementations are integrated and Windows `.NET 8` restore/build/test plus manual scenarios A–M are recorded.
