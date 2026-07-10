# Sprint 15 — Team B Group-Aware Layout + Preview Rowspan Report

## Role / baseline

Team B — Group-Aware Pagination + WPF Table Merge Lead.

Baseline: `KKL.WordStudio-Sprint15-Contract-Baseline.zip` only.
The Sprint 15 shared table contracts were consumed as frozen. No Application contract, Domain, Infrastructure, MainWindow/MainViewModel, ReportContentBuilder, or composition implementation was modified.

## Result

Sprint 15 grouped serial table semantics now flow through the Team B-owned Engine and Preview layers without PN/Serial/Quantity role inference:

`TableContentNode.Rows + CellSpans + RowGroups + CompositionWarnings`
→ deterministic group-aware page flow
→ fragment-local `TablePageBlockPayload.Rows + CellSpans`
→ `PreviewTablePageBlockViewModel.CellSpans`
→ real WPF `Grid.RowSpan` rendering.

The Engine never inspects displayed header text or cell values to discover grouping roles. The Preview trusts fragment-local span payloads and does not reconstruct complete-table merge ranges.

## Engine warning flow

`GeneratedDocumentPaginator.LayoutTable` now appends non-empty `TableContentNode.CompositionWarnings` to the document layout warning list before table pagination.

- identical warning text is deduplicated using ordinal comparison;
- `SourceError` keeps its separate existing warning path;
- composition warnings are not converted to `SourceError` and remain non-blocking layout warnings.

## Group-aware table pagination

`DeterministicTablePaginator.Layout` now consumes the frozen:

- `IReadOnlyList<TableCellSpan> cellSpans`
- `IReadOnlyList<TableRowGroup> rowGroups`

For a valid `KeepTogetherWhenPossible` group when the next semantic row index equals `StartRowIndex`:

1. complete group row height is deterministically estimated;
2. current fragment remaining row capacity is calculated after applicable caption/header/source-error overhead;
3. fresh-page row capacity is calculated with the overhead applicable to the fragment that would start there;
4. if the complete group fits the fresh page but not the current remainder, the current fragment ends before the group and the group starts on the next page;
5. if it fits current capacity, the complete group is added together;
6. if the group itself exceeds fresh capacity, pagination falls back to row-atomic forward progress and may split only at physical row boundaries.

Rows outside groups preserve Sprint 14 row-atomic greedy behavior. Row order is never changed.

Table header repeat, first-fragment-only caption, fragment index/continuation state, and same real `ElementId` across fragments remain intact.

Defensive invalid row-group inputs are ignored with a concise layout warning so malformed group metadata cannot create a progress loop.

## Fragment-local span projection

A focused internal `ProjectFragmentRowsAndSpans` helper projects complete semantic table rows/spans into one page fragment.

The helper:

- creates a new fragment row matrix and never mutates `TableContentNode.Rows`;
- computes interval intersection between each valid complete span and the fragment row range;
- omits spans with no intersection;
- when a fragment starts in the middle of a semantic merge, copies the original semantic anchor cell value from `completeRows[semanticStart][ColumnIndex]` into the first local intersecting row;
- emits a fragment-local `TableCellSpan` only when local intersection length is at least 2;
- keeps the copied anchor value without emitting invalid `RowSpan=1` when only one row intersects;
- preserves `StartRowIndex` as the complete semantic source-row index of the fragment's first row.

Complete semantic spans are validated before fragment projection. Negative indexes, `RowSpan < 2`, row-range overflow, and out-of-range anchor columns are ignored and produce a concise layout warning instead of crashing pagination.

`TablePageBlockPayload.Rows` now receives projected fragment rows and `CellSpans` receives projected local spans.

## Keep-with-next minimum table estimate

The minimum first-fragment table estimate used by generated heading keep-with-next now receives `TableRowGroup` metadata and fresh body height.

When the first table row starts a keep-together group and caption/header plus the complete group fit the fresh body, the estimate uses the complete first-group height. If the group is too large, the bounded first-row estimate is retained. A huge serial group therefore does not turn the whole table atomic or create a heading pagination loop.

## Preview projection

`PreviewPageProjection` now forwards `TablePageBlockPayload.CellSpans` directly to `PreviewTablePageBlockViewModel`.

`PreviewTablePageBlockViewModel` exposes:

```csharp
IReadOnlyList<TableCellSpan> CellSpans
```

No second span contract/class was introduced.

## WPF true rowspan renderer

Added Preview-only `PreviewTableGridControl`.

The former nested data-row `ItemsControl + UniformGrid` renderer was removed. The table header's existing one-row `UniformGrid` remains unchanged because header editing does not require vertical merge semantics.

The new data renderer:

- builds one WPF `Grid` row per payload row;
- resolves the table column count from the supplied column count, fragment rows, and fragment-local spans;
- creates equal star-sized `ColumnDefinition` entries;
- maps each span anchor to real `Grid.RowSpan` via `SetRowSpan`;
- does not create duplicate visible cells covered by a prior span;
- retains right/bottom borders per visible cell, so a merged cell has no horizontal divider through its span while serial/non-merged cells keep individual row separators;
- vertically centers merged-cell content;
- keeps wrapping/trimming and read-only `TextBlock` data cells;
- ignores malformed fragment-local spans defensively without performing any complete-table clipping or pagination.

Mouse events continue to bubble to the existing outer table block container. Therefore clicking a normal or merged data cell still selects the real table element and preserves the existing body-only structure gesture path.

## Existing Preview interaction preservation

No interaction service or report-structure path was changed.

Preserved:

- physical page stack / page geometry;
- zoom behavior;
- same-`ElementId` selection across split fragments;
- `CanInteract` / `CanStructureInteract` separation;
- body-only preview delete and drag/drop;
- caption edit on fragment 0 only;
- displayed table-header edit;
- read-only Excel-derived data cells;
- `ReportContentChanged` layout rebuild;
- `WorkspaceChanged` selection synchronization;
- split-text semantic reconstruction and existing text editor paths.

Grouped/merged data cells do not introduce a new editing model.

## Focused tests added

Added `tests/KKL.WordStudio.Engine.Tests/Sprint15GroupedTablePaginationTests.cs` with 12 requested focused tests:

1. `GroupedRows_StayTogetherWhenTheyFitFreshPage`
2. `GroupedRows_MoveToNextPageWhenCurrentRemainderIsTooSmall`
3. `OversizedGroup_SplitsAtRowBoundariesWithProgress`
4. `FragmentSpans_AreLocalToPayloadRows`
5. `SpanCrossingPageBoundary_RestartsAtContinuationFragment`
6. `SpanContinuation_CopiesSemanticAnchorValue`
7. `SingleRowSpanIntersection_EmitsValueWithoutInvalidRowSpan`
8. `InvalidSemanticSpan_IsWarnedAndDoesNotCrash`
9. `CompositionWarnings_AppearInDocumentLayoutWarnings`
10. `GroupedTableFragments_PreserveElementIdAndRowOrder`
11. `RepeatedTableHeader_RemainsOnGroupedContinuationPage`
12. `GroupedCaption_RemainsFirstFragmentOnly`

The focused page sizes are calibrated against the deterministic Sprint 14 measurement model so the tests intentionally create:

- a 3-row group that fits a fresh table fragment;
- the same group not fitting after one prior row;
- a 5-row group that exceeds fresh capacity and must make row-boundary progress;
- two-row page fragments for span clipping/restart checks;
- a final one-row span intersection that must carry anchor identity without emitting `RowSpan=1`.

Source test inventory is now 243 `[Fact]`/`[Theory]` attributes: 231 in the supplied contract baseline plus 12 Team B focused tests. This is source inventory only, not a runtime pass count.

## Files changed / added

Changed:

- `src/KKL.WordStudio.Engine/Layout/DeterministicTablePaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/FrontMatterPaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/GeneratedDocumentPaginator.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageViewModel.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml`

Added:

- `src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs`
- `tests/KKL.WordStudio.Engine.Tests/Sprint15GroupedTablePaginationTests.cs`
- `docs/sprint15-team-b-layout-preview-report.md`

A clean-baseline byte comparison found no changes outside Team B-owned paths.

## Static verification

A focused source/XAML acceptance audit passed 21/21 gates:

1. Team B ownership paths only
2. Preview XAML XML well-formed
3. all 20 XAML event handlers resolve in code-behind
4. modified/new C# lexical delimiters balance
5. CompositionWarnings flow to layout warnings
6. paginator consumes semantic CellSpans
7. paginator consumes semantic RowGroups
8. keep-together compares current and fresh capacity
9. span projection uses interval intersection
10. continuation copies semantic anchor value
11. fragment-local RowIndex/RowSpan emission
12. invalid semantic spans warn and are ignored
13. table payload receives projected CellSpans
14. Preview table VM exposes contract `TableCellSpan`
15. Preview projection forwards payload CellSpans
16. Preview data renderer binds Rows and CellSpans
17. real WPF `Grid.RowSpan` is applied
18. cells covered by a span are not rendered
19. data-row renderer no longer uses `UniformGrid`
20. Engine/Preview contains no ProductNo/Product No/Serial No/Quantity/Seri No/Adet role heuristic
21. all 12 requested focused Engine test names exist

The remaining `UniformGrid` in `PreviewView.xaml` is the existing editable header row, not table data rows.

## Restore / build / test status

The current execution environment has no .NET CLI.

Actual availability check:

```text
command -v dotnet
<no path returned>
```

Therefore the required runtime commands could not be executed:

- `dotnet restore`: NOT RUN — .NET CLI unavailable
- `dotnet build`: NOT RUN — .NET CLI unavailable
- `dotnet test`: NOT RUN — .NET CLI unavailable

No green build/test result is claimed. Windows/.NET 8 WPF verification remains pending and is final runtime truth.

## Contract change request

None. The frozen Sprint 15 shared contract was sufficient for Team B. No `docs/CONTRACT_CHANGE_REQUEST-B.md` was created.
