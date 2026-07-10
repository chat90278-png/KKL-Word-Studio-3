# Sprint 16 — Team C Engine + A4 Preview Format Fidelity Report

## 1. Role and ownership scope

Team C operated as the **Engine + A4 Preview Format Fidelity** team from `KKL.WordStudio-Sprint16-Contract-Baseline.zip` only. The frozen Sprint 16 shared contract and the bootstrap-owned resolved formatting contracts were treated as authoritative.

The implementation consumes resolved `TextContentNode.Format`, `TableContentNode.Format`, `TextPageBlockPayload.Format`, and `TablePageBlockPayload.Format`. It does not parse a DOCX, infer table style from headers, configure Serial/Quantity grouping roles, resolve reference profiles, or write Word/OpenXML.

Work stayed inside the assigned Team C ownership boundary:

- `src/KKL.WordStudio.Engine/Layout/**`
- `src/KKL.WordStudio.UI/Preview/**`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageViewModel.cs`
- the rendering-template area of `src/KKL.WordStudio.UI/Views/PreviewView.xaml`
- `tests/KKL.WordStudio.Engine.Tests/*Sprint16*`
- this report

No frozen Application/Domain contract, Infrastructure/Word, DI, grouping-role configuration, or Preview command-bar file was changed. No contract change request was required.

## 2. Baseline provenance note

The supplied Sprint 16 bootstrap report records a provenance discrepancy: the contract describes an exact `KKL.WordStudio-Sprint15-Stabilized-WindowsGreen.zip` input with a 308-test Windows-green baseline, while the supplied bootstrap source archive is named `KKL.WordStudio-Sprint15-Stabilized.zip`. Its source inventory contains 308 `[Fact]` / `[Theory]` methods and 0 skips, but its embedded Sprint 15 stabilization report does not prove that the exact patched source was rerun 308/308 green on Windows.

Team C did not repair or reinterpret unrelated Sprint 15 behavior and does not claim that missing provenance. Windows/.NET 8 remains final build/runtime truth.

The Sprint 16 contract bootstrap source inventory is 318 test methods with 0 skips. Team C adds exactly 17 focused Sprint 16 tests, bringing the source inventory to 335 test methods with 0 skip assignments.

## 3. Engine text measurement fidelity

`DeterministicTextMeasurement` now has a resolved-format path used by generated KKL text.

Measurement accounts for:

- `FontSizePoints`;
- `Bold` through the existing deterministic width approximation;
- `FontFamilyName` only as a stable width-factor category, with no font-file loading or WPF dependency;
- `LineSpacingMultiple` in measured line height;
- `LeftIndentMillimeters` in paragraph available width;
- `FirstLineIndentMillimeters` in first-line available width;
- explicit newlines;
- deterministic word/character wrapping.

Stable font width categories are deliberately narrow and deterministic. Arial/Helvetica, Times/serif, mono/Consolas/Courier, and generic fallback categories receive fixed approximation factors. The Engine does not inspect or embed font files.

`GeneratedDocumentPaginator` now consumes the resolved text format for generated report text. Paragraph flow represents:

- space before;
- measured line height including line-spacing multiple;
- space after;
- left-indented X position and reduced block width;
- first-line indent in first-line measurement;
- resolved paragraph alignment in `TextPageBlockPayload`;
- the exact resolved `Format` on every generated text payload.

Vertical spacing is represented by Engine page flow and block positions. Preview does not add `SpaceBeforePoints` or `SpaceAfterPoints` a second time.

Resolved `Format.KeepWithNext` is authoritative for generated text keep-with-next behavior. The previous kind-only heading check was replaced. Existing direct-construction Engine tests remain compatible through a narrow legacy fallback only when the untouched `DefaultFormatProfiles.BodyText` singleton is still on a directly initialized `TextContentNode`; in that compatibility case existing `FontSize`, `Bold`, and Heading/AltHeading keep-next semantics are preserved. Content built through the Sprint 16 resolver uses the resolved format directly.

## 4. Engine table measurement fidelity

`DeterministicTablePaginator` now receives the resolved `ResolvedTableFormat` for generated tables and preserves that same format instance in each fragment-local `TablePageBlockPayload`.

### Width and columns

The positioned table block width follows `WidthPercent` within the Engine content area. Positive percentages are capped at 100%; absent/invalid non-positive values use 100% compatibility behavior.

When resolved table columns exist, row measurement normalizes `ResolvedTableColumnFormat.WidthWeight` values and uses those unequal column widths. The Engine no longer assumes equal widths for a valid resolved column profile.

When no format columns and no format margins/preferred-row-height exist, imported/generic legacy callers retain the earlier deterministic equal-width row-height approximation.

### Font, margins, row height, and NoWrap

Header and body row measurement consume per-column:

- header/body font family;
- header/body font size;
- header/body bold state.

Resolved horizontal cell margins reduce usable text width. Top/bottom margins increase cell height contribution. `PreferredRowHeightMillimeters` is enforced as a row minimum.

`NoWrap` prevents wrapping approximation for the resolved column, while explicit newlines remain real line boundaries. This applies consistently to the resolved column in Engine measurement; the Preview consumes the same per-column no-wrap semantic.

### Sprint 15 semantic preservation

The existing Sprint 15 pagination model remains authoritative and was not reimplemented:

- complete semantic `Rows` remain the source row matrix;
- `TableRowGroup` keep-together behavior is preserved;
- groups that fit on a fresh page move together when the current remainder is insufficient;
- oversized groups still split at physical row boundaries with mandatory progress;
- `TableCellSpan` validation remains in Engine;
- complete-table spans are clipped into fragment-local spans;
- continuation fragments restart local spans where intersection length is at least two;
- the original semantic anchor value is copied into the first local continuation row when a span started on a previous page;
- repeated table header semantics are preserved through resolved `RepeatHeader`;
- caption remains first fragment only;
- the same real `ElementId` remains on every table fragment.

## 5. Format warnings

`DeterministicDocumentLayoutEngine` now appends `ReportContentDocument.FormatWarnings` to `DocumentLayoutResult.Warnings`.

Whitespace-only warnings are ignored. Non-empty warnings are trimmed and deduplicated with ordinal comparison. Imported-document warnings use the same focused add-once behavior.

`FallbackDocumentLayoutEngine` also preserves format warnings and the Sprint 16 payload `Format` values so compatibility/fallback output remains contract-compliant.

## 6. A4 Preview text fidelity

`PreviewPageProjection` and `PreviewTextBlockControl` now consume resolved text payload formatting.

Generated Preview text applies:

- resolved `FontFamilyName`;
- resolved `FontSizePoints` converted to WPF DIP;
- `Bold`;
- `Italic`;
- `Underline`;
- valid `ForegroundColor` values through guarded brush conversion;
- resolved paragraph alignment;
- resolved line-spacing multiple through projected `LineHeight`;
- first-line indent on the first generated fragment.

Invalid foreground color values do not fail Preview projection; they fall back to inherited/default WPF foreground behavior.

The Engine already accounts for left indent in positioned block X/width and for before/after paragraph spacing in page flow. Preview therefore does not add those vertical or block-left offsets again.

Imported front-matter text retains its payload `Alignment` rather than being accidentally overwritten by the default Sprint 16 text format. Existing imported run-level font sizing/formatting remains intact.

## 7. A4 Preview table fidelity

`PreviewTableGridControl` now consumes `ResolvedTableFormat` directly.

The read-only data grid applies:

- `WidthWeight` as WPF `GridLength(..., Star)` ratios;
- table border size converted from points to DIP;
- resolved cell margins converted from millimeters to DIP;
- preferred row height as `RowDefinition.MinHeight`;
- per-column body font family, size, and bold;
- per-column body paragraph alignment;
- per-column vertical alignment;
- per-column `NoWrap` / wrapping behavior.

The resolved table `WidthPercent` is already represented by the Engine's positioned block width; the Preview control fills that positioned table block and does not re-derive table width.

Sprint 15 rowspan rendering is preserved:

- each payload row maps to one WPF grid row;
- valid fragment-local spans use `Grid.RowSpan`;
- covered continuation cells are omitted rather than rendered with duplicate content;
- conflicting/invalid local spans remain ignored by the rendering-only guard;
- unmerged serial cells retain individual bottom borders so serial-row separation remains visible;
- merged cell content uses the resolved vertical alignment.

A generic Preview still works through `DefaultFormatProfiles.Table` and a deterministic fallback column format when no reference profile columns are available.

## 8. Editable header uses the same column ratios

The table header renderer no longer uses equal-width `UniformGrid` layout when resolved table formatting is available.

Added `PreviewTableColumnsPanel`, a Preview-only WPF `Panel` that measures and arranges the existing editable header item containers with the same `Format.Columns[index].WidthWeight` ratios used by `PreviewTableGridControl`.

The existing header edit gestures, routed handlers, and edit state remain in place. Header text/edit controls now also consume:

- resolved header font family/size/bold;
- per-column header alignment;
- resolved vertical alignment;
- resolved cell margins;
- preferred row height;
- resolved border thickness;
- resolved no-wrap behavior.

No Preview command-bar area was changed.

## 9. A4 page geometry

`PreviewPageProjection` continues to project `DocumentPageLayout.PageLayout.WidthMillimeters` and `HeightMillimeters` directly to WPF DIP. Page-block X/Y/Width/Height remain the Engine-owned positioned layout geometry.

Therefore a resolved Sero page profile of approximately 210 × 297 mm produces a physical A4-proportioned Preview page, and approximately 25 mm margins are visible through the Engine block positions. No fake margin guides were added.

## 10. Focused Sprint 16 tests added

Added `tests/KKL.WordStudio.Engine.Tests/Sprint16FormatAwareLayoutTests.cs` with the 9 requested Engine tests:

1. `ReferenceTextSpacing_AffectsPageFlow`
2. `ReferenceIndent_ReducesTextLineWidth`
3. `ResolvedKeepWithNext_IsHonored`
4. `ReferenceColumnWeights_AffectRowMeasurement`
5. `PreferredRowHeight_IsMinimum`
6. `CellMargins_AffectWrappingHeight`
7. `NoWrapColumn_DoesNotWrapInMeasurement`
8. `FormatWarnings_AppearInLayoutWarnings`
9. `Sprint15GroupedPagination_RemainsWithResolvedFormat`

The grouped-pagination regression uses the frozen Sero reference profile's Table 2 raw grid width weights (`469 / 2550 / 1579 / 1579 / 1802 / 1021`), 99.32% table width, 10.195 mm preferred row height, 1.235 mm left/right cell margins, and no-wrap columns. It verifies multi-page progress, same `ElementId`, payload format preservation, serial row order, fragment-local span bounds, and repeated continuation headers.

Added `tests/KKL.WordStudio.Engine.Tests/Sprint16PreviewFormatStaticTests.cs` with the 8 requested Preview/static guards:

1. `PreviewText_ConsumesResolvedTextFormat`
2. `PreviewTable_ConsumesResolvedTableFormat`
3. `PreviewTable_UsesColumnWidthWeights`
4. `PreviewTable_UsesPreferredRowHeight`
5. `PreviewTable_UsesPerColumnAlignment`
6. `PreviewTable_PreservesGridRowSpan`
7. `EditableHeader_UsesReferenceColumnWidths`
8. `NoReferenceProfile_GenericPreviewStillWorks`

Baseline tests were not edited or weakened.

## 11. Exact Team C change record

Modified:

- `src/KKL.WordStudio.Engine/Layout/DeterministicDocumentLayoutEngine.cs`
- `src/KKL.WordStudio.Engine/Layout/DeterministicTablePaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/DeterministicTextMeasurement.cs`
- `src/KKL.WordStudio.Engine/Layout/FallbackDocumentLayoutEngine.cs`
- `src/KKL.WordStudio.Engine/Layout/FrontMatterPaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/GeneratedDocumentPaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/LayoutPageFlow.cs`
- `src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs`
- `src/KKL.WordStudio.UI/Preview/PreviewTextBlockControl.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageViewModel.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml` — rendering templates only

Added:

- `src/KKL.WordStudio.UI/Preview/PreviewTableColumnsPanel.cs`
- `tests/KKL.WordStudio.Engine.Tests/Sprint16FormatAwareLayoutTests.cs`
- `tests/KKL.WordStudio.Engine.Tests/Sprint16PreviewFormatStaticTests.cs`
- `docs/sprint16-team-c-engine-preview-report.md`

Fresh-baseline SHA-256 path comparison found no change outside the Team C ownership allowlist.

## 12. Actual verification

The required commands were attempted with actual shell execution from the Team C workspace:

```text
dotnet restore
bash: line 1: dotnet: command not found
exit 127


dotnet build
bash: line 1: dotnet: command not found
exit 127


dotnet test
bash: line 1: dotnet: command not found
exit 127
```

Therefore this report does **not** claim restore, build, or test success. Windows/.NET 8 verification remains pending and is the final WPF/runtime/build truth.

Static/source checks actually completed in the available environment:

- all 12 `.csproj` files plus `PreviewView.xaml` parse as XML;
- all 17 requested Team C Sprint 16 test names are present;
- current source test inventory is 335 `[Fact]` / `[Theory]` methods;
- source scan finds 0 `Skip =` assignments;
- all 14 modified/new C# implementation/test files passed a lexical delimiter-balance scan that ignores comments, normal strings, verbatim strings, and character literals;
- Engine source contains no `System.Windows` reference;
- changed production files contain no `DocumentFormat.OpenXml`, `WordprocessingDocument`, `SerialQuantityGrouping`, or `IReportContentFormatResolver` dependency;
- fresh-baseline SHA-256 path comparison reports zero ownership violations;
- no `bin` or `obj` directories are present;
- `git diff --no-index --check` emitted no whitespace-error diagnostics; its exit status is 1 solely because the compared trees intentionally differ;
- the final delivery ZIP is integrity-checked after packaging.

## 13. Contract status

The frozen Sprint 16 shared contract was sufficient for Team C. `docs/CONTRACT_CHANGE_REQUEST-C.md` was not created.
