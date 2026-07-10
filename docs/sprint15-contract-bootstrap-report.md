# Sprint 15 Contract Bootstrap Report

## 1. Role

Contract Architect / Table Semantics Contract Architect bootstrap completed against the supplied `KKL.WordStudio-Sprint14-Stabilized-v3.zip` as the only code baseline. `SPRINT15-SHARED-CONTRACT.txt` was treated as authoritative.

No serial/quantity grouping algorithm, alias detector, quantity parser, row expansion heuristic, group-aware pagination, WPF rowspan renderer, Word `vMerge`, shell cleanup, Project Explorer change, or DOCX-to-KKL reverse engineering was implemented.

## 2. Baseline provenance discrepancy

The bootstrap prompt names `KKL.WordStudio-Sprint14-Stabilized-v3-WindowsGreen.zip` and requires a previously verified state of:

- `dotnet restore`: SUCCESS
- `dotnet build`: SUCCESS — 0 warnings / 0 errors
- `dotnet test`: 222 passed / 0 failed / 0 skipped

The supplied archive is named `KKL.WordStudio-Sprint14-Stabilized-v3.zip`.

Its included `docs/sprint14-windows-stabilization-v3-report.md` states that the v3 patch itself was **not executable-tested in that sandbox**. That report records the previous Windows run as build-clean but with two failing Infrastructure tests before the v3 patch, and says 222/222 green is expected only after a future Windows rerun. The included `docs/verification/dotnet-*.meta.txt` files also record exit code 127 because `dotnet` was unavailable in that earlier sandbox.

Therefore the supplied ZIP does not contain evidence that this exact v3 source is the separately named Windows-green 222/222 artifact. This bootstrap does not repair or reinterpret that Sprint 14 provenance gap and does not claim the input was Windows-green.

## 3. Domain contract

Added `KKL.WordStudio.Domain.Elements.SerialQuantityGrouping` with exactly:

- `MatchKeyColumnId`
- `SerialNumberColumnId`
- `QuantityColumnId`
- `WasAutoDetected`

The three role identities are `Guid` values intended to hold stable `TableColumn.Id` identities.

`TableElement` now exposes:

```csharp
public SerialQuantityGrouping? SerialQuantityGrouping { get; set; }
```

No raw column index, Excel column letter, or header-string grouping identity was added.

## 4. Application table semantic contract

Added namespace/folder `KKL.WordStudio.Application.Tables` containing:

- `TableCellSpan`
- `TableRowGroup`
- `TableRowCompositionResult`
- `ITableContentRowComposer`
- `PassthroughTableContentRowComposer`

`TableCellSpan` carries zero-based `RowIndex`, zero-based `ColumnIndex`, and vertical `RowSpan` semantics.

`TableRowGroup` carries complete-table `StartRowIndex`, `RowCount`, and `KeepTogetherWhenPossible` pagination intent.

`TableRowCompositionResult` carries composed rows, complete semantic cell spans, row groups, and warnings.

`PassthroughTableContentRowComposer` returns the exact input row collection and empty spans/groups/warnings. It contains no grouping heuristics.

## 5. Table content and layout payload contracts

`TableContentNode` retains `Rows` and now adds default-empty:

- `CellSpans`
- `RowGroups`
- `CompositionWarnings`

These are complete semantic table contracts. Existing direct initializers remain source-compatible.

`TablePageBlockPayload` now adds default-empty `CellSpans`. This payload contract is fragment-local. No second layout table payload was introduced.

The Sprint 14 frozen layout shape test was strengthened for the intentional contract extension: all prior table payload members remain exact and `required`, while `CellSpans` is explicitly verified as the sole default-compatible optional member.

## 6. ReportContentBuilder orchestration

`ReportContentBuilder(IDataProviderRegistry)` remains present and source-compatible. It delegates to `PassthroughTableContentRowComposer`.

A composer-aware constructor now accepts `ITableContentRowComposer`. Application DI registers exactly one bootstrap composer:

`ITableContentRowComposer -> PassthroughTableContentRowComposer`

DI can therefore select the composer-aware constructor, while direct one-argument callers preserve Sprint 14 behavior.

Successful table paths now compose only after the existing row stream is complete:

- legacy bound rows: after existing sort and field projection;
- static detail rows: after existing cell-text projection;
- multi-source rows: after every source has been normalized through stable table-column mappings and appended in source order.

`TableContentNode` is built from `TableRowCompositionResult.Rows`, `CellSpans`, `RowGroups`, and `Warnings`.

`BuildMultiSourceErrorNode` remains a direct incomplete error-node path. Partial/error rows are not passed to the composer.

No serial/quantity heuristic was added to `ReportContentBuilder`.

## 7. Fallback engine compatibility

`FallbackDocumentLayoutEngine` preserves `TableContentNode.CellSpans` when it maps a table to its one-page `TablePageBlockPayload`.

No group-aware pagination or span clipping/restart behavior was added. `DeterministicDocumentLayoutEngine` pagination behavior was not modified in this bootstrap.

## 8. Focused contract tests

Added nine focused tests equivalent to the requested contract gates:

1. `TableElement_HasPersistedSerialQuantityGroupingIdentity`
2. `SerialQuantityGrouping_UsesStableColumnIds`
3. `PassthroughComposer_PreservesRowsAndEmitsNoSpansOrGroups`
4. `ReportContentBuilder_ComposesRowsAfterNormalizedSingleSourceRows`
5. `ReportContentBuilder_ComposesRowsAfterAllMultiSourceRows`
6. `ReportContentBuilder_SourceError_DoesNotComposePartialRows`
7. `TableContentNode_DefaultsToEmptySpanGroupAndWarnings`
8. `TablePagePayload_DefaultsToEmptyFragmentSpans`
9. `FallbackLayout_PreservesTableCellSpans`

The builder composition-order tests use a spy composer. No real serial/quantity heuristic is tested or implemented.

Baseline source test inventory is 222 `[Fact]` / `[Theory]` methods with 0 skipped according to the supplied Sprint 14 v3 report. The current source inventory is 231 test methods: 222 baseline + 9 Sprint 15 bootstrap tests.

## 9. ADR

Added:

`docs/adr/0014-serial-quantity-grouped-table-semantics.md`

It records:

- why `Rows` alone cannot express the grouped serial table;
- stable `TableColumn.Id` role configuration;
- composition after full multi-source normalization;
- complete semantic spans versus fragment-local layout spans;
- Engine ownership of span clipping/restart at page boundaries;
- Word consumption of complete semantic spans with vertical merge semantics;
- Preview consumption of fragment-local spans;
- DOCX-to-KKL reverse engineering as a later concern.

## 10. Exact change record and frozen bootstrap ownership

Exact Sprint 15 bootstrap changes are listed below.

Modified:

- `src/KKL.WordStudio.Domain/Elements/TableElement.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentBuilder.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentNode.cs`
- `src/KKL.WordStudio.Application/Layout/DocumentLayoutContracts.cs`
- `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `src/KKL.WordStudio.Engine/Layout/FallbackDocumentLayoutEngine.cs`
- `tests/KKL.WordStudio.Architecture.Tests/FrozenContractShapeTests.cs`

Added:

- `src/KKL.WordStudio.Domain/Elements/SerialQuantityGrouping.cs`
- `src/KKL.WordStudio.Application/Tables/TableCellSpan.cs`
- `src/KKL.WordStudio.Application/Tables/TableRowGroup.cs`
- `src/KKL.WordStudio.Application/Tables/TableRowCompositionResult.cs`
- `src/KKL.WordStudio.Application/Tables/ITableContentRowComposer.cs`
- `src/KKL.WordStudio.Application/Tables/PassthroughTableContentRowComposer.cs`
- `tests/KKL.WordStudio.Domain.Tests/Sprint15ContractBootstrapTests.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint15ContractBootstrapTests.cs`
- `tests/KKL.WordStudio.Engine.Tests/Sprint15ContractBootstrapTests.cs`
- `docs/adr/0014-serial-quantity-grouped-table-semantics.md`
- `docs/sprint15-contract-bootstrap-report.md`

The core contract/orchestration bootstrap files are frozen for Teams A/B/C/D:

- `src/KKL.WordStudio.Domain/Elements/TableElement.cs`
- `src/KKL.WordStudio.Domain/Elements/SerialQuantityGrouping.cs`
- `src/KKL.WordStudio.Application/Tables/**`
- `src/KKL.WordStudio.Application/Content/ReportContentBuilder.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentNode.cs`
- `src/KKL.WordStudio.Application/Layout/DocumentLayoutContracts.cs`
- `src/KKL.WordStudio.Engine/Layout/FallbackDocumentLayoutEngine.cs`
- `tests/KKL.WordStudio.Domain.Tests/Sprint15ContractBootstrapTests.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint15ContractBootstrapTests.cs`
- `tests/KKL.WordStudio.Engine.Tests/Sprint15ContractBootstrapTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/FrozenContractShapeTests.cs`
- `docs/adr/0014-serial-quantity-grouped-table-semantics.md`
- `docs/sprint15-contract-bootstrap-report.md`

One explicit handoff exception is required by the authoritative shared contract: Team A may edit `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs` only to replace the bootstrap `ITableContentRowComposer -> PassthroughTableContentRowComposer` registration with the real Sprint 15 composer registration. This exception is necessary because the shared contract explicitly assigns that DI replacement to Team A; it does not authorize changing the frozen interface, result, content-node, builder, or layout contracts.

## 11. Verification

The required runtime commands could not be executed because this environment has no `dotnet` CLI.

Actual availability check:

```text
command -v dotnet
<no path returned>
```

Therefore:

- `dotnet restore`: NOT RUN — .NET CLI unavailable
- `dotnet build`: NOT RUN — .NET CLI unavailable
- `dotnet test`: NOT RUN — .NET CLI unavailable

No green build/test claim is made.

Static/source checks performed in this bootstrap include:

- all project files parse as XML;
- all `ProjectReference` targets exist;
- no `bin` / `obj` directories are present in delivery;
- the domain grouping contract has exactly four public properties and uses `Guid` stable role identities;
- Application table contracts exist in `KKL.WordStudio.Application.Tables`;
- passthrough composition returns the input rows and empty spans/groups/warnings by source inspection;
- `TableContentNode` and `TablePageBlockPayload` use empty collection defaults for new fields;
- the one-argument `ReportContentBuilder` constructor remains present;
- the composer-aware constructor is present;
- exactly one bootstrap `ITableContentRowComposer` DI registration exists;
- builder success paths route through one composition helper after normalization/projection;
- `BuildMultiSourceErrorNode` does not call the composer;
- fallback table mapping assigns `CellSpans = table.CellSpans`;
- all nine requested focused test method names are present;
- source test inventory is 231 methods;
- no bootstrap grouping algorithm, alias list, quantity parser, row-expansion implementation, `Grid.RowSpan`, or Word `VerticalMerge` implementation was introduced.

Windows/.NET 8 restore/build/test remains mandatory final truth before parallel Team A/B/C/D implementation starts from this baseline.
