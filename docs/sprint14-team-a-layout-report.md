# Sprint 14 — Team A Layout Core Report

## Role

Team A / Layout Core Lead / Engine Architect.

Baseline: `KKL.WordStudio-Sprint14-Contract-Baseline-Stabilized.zip`.

The frozen Sprint 14 Application layout and imported-document contracts were consumed as-is. No contract/bootstrap file was changed and no `CONTRACT_CHANGE_REQUEST-A.md` was required.

## Result

The Engine DI registration now resolves `IDocumentLayoutEngine` to `DeterministicDocumentLayoutEngine` instead of the bootstrap fallback. The original fallback implementation remains in the Engine project only for baseline compatibility tests; it is no longer the production registration.

Implemented deterministic millimeter-based multi-page layout with focused internal components:

- `DeterministicDocumentLayoutEngine`: front matter + generated report orchestration and bounded TOC convergence passes.
- `GeneratedDocumentPaginator`: generated KKL body flow, repeating header/footer decoration, page numbers, heading keep-with-next, TOC blocks, images and unsupported placeholders.
- `FrontMatterPaginator`: imported section/page flow, explicit page breaks, rich paragraph style preservation, tables, images and unsupported placeholders.
- `DeterministicTablePaginator`: row-atomic fragmentation, repeated continuation headers, first-fragment-only captions, `StartRowIndex`, fragment identity/order and oversized-row safety.
- `DeterministicTextMeasurement`: environment-stable text approximation based on available width, point size, whitespace wrapping, explicit newlines and conservative line height.
- `LayoutPageFlow`: sequential page creation/body cursor handling.
- `TextRunLayoutFactory`: preserves rich run style when measured text is split into page fragments.

No WPF, OpenXML, Excel/provider, report-structure mutation, COM, PDF or WebView dependency was introduced.

## Pagination behavior

Generated report layout uses `ReportContentDocument.PageLayout` and flows body blocks inside the configured margins. Long text can emit multiple fragments/pages while preserving `ElementId`, semantic kind and fragment order.

Generated headers and footers are laid out in their own regions and repeated for every generated page. When page numbers are enabled, a non-editable `PageNumber` footer block is emitted with the actual absolute 1-based document page number, including imported front-matter offset.

Heading and AltHeading nodes apply keep-with-next behavior by comparing the measured heading height plus the following node's minimum first-fragment height with the current and fresh body capacity.

## Table fragmentation

Tables split only between data rows. A data row is never intentionally split.

For each fragment:

- the same real report `ElementId` is preserved for generated KKL tables;
- `FragmentIndex` increments from zero;
- continuation fragments set `IsContinuation=true`;
- `Rows` contains only rows placed in that fragment;
- `StartRowIndex` is the first source data-row index in the fragment;
- captions are emitted only on the first fragment;
- headers are present on the first generated fragment and repeated on continuation fragments;
- `IsHeaderRepeated=true` is set on repeated continuation headers;
- `SourceError` remains in the table payload and also produces a warning.

If a single measured row is taller than the complete body height, the row is placed alone, a warning is emitted and layout continues. Page/block/row loops contain cancellation checks and no unbounded retry loop is used.

## TOC projection

When `ReportContentDocument.TableOfContents` is present, the engine paginates with provisional entries, resolves `ElementId -> first page number`, projects absolute page numbers into `LaidOutTocEntry`, and repeats layout until page-number projection stabilizes.

The pass count is bounded at five. If projection does not converge, the latest deterministic result is retained and an explicit warning is emitted.

## Imported front matter

Imported front matter is laid out before generated KKL pages.

Each `ImportedDocumentSection` starts with its own `PageLayout` geometry. Supported imported blocks are flowed as follows:

- paragraphs: deterministic rich-run measurement with bold/italic/underline/font size/family preserved;
- KeepWithNext paragraphs: moved with the following block when both can fit on a fresh body;
- tables: row-based fragmentation; when `RepeatFirstRow=true`, the first source row is used as the repeating continuation header;
- images: image bytes/content type and intrinsic dimensions are preserved; known width/height are proportionally fitted without upscaling;
- explicit page breaks: force a new front-matter page;
- unsupported blocks: produce visible `Unsupported` blocks and warnings.

Imported blocks always use `ElementId=null` and `IsEditableReportElement=false`.

## Focused tests

Added `DeterministicDocumentLayoutEngineTests.cs` with 18 Sprint 14 scenarios:

1. `LongBodyText_FlowsAcrossMultiplePages`
2. `GeneratedPages_UseConfiguredPageGeometryAndMargins`
3. `HeaderAndFooter_RepeatOnEveryGeneratedPage`
4. `PageNumbers_AreAbsoluteAndSequential`
5. `Heading_IsKeptWithFollowingBlockWhenPossible`
6. `Table_SplitsRowsAcrossPages`
7. `TableContinuation_RepeatsHeader`
8. `TableCaption_AppearsOnlyOnFirstFragment`
9. `TableFragments_PreserveElementIdAndFragmentOrder`
10. `OversizedTableRow_ProducesWarningWithoutInfiniteLoop`
11. `TocEntries_UseFirstElementPageNumbers`
12. `FrontMatterPages_PrecedeGeneratedPages`
13. `FrontMatterExplicitBreak_ForcesNewPage`
14. `FrontMatterSection_UsesOwnPageLayout`
15. `ImportedImage_IsFitWithinPageBody`
16. `UnsupportedImportedBlock_IsVisibleAndWarned`
17. `LayoutBlocks_DoNotOverlapWithinBodyFlow`
18. `Cancellation_IsObservedDuringLargeTableLayout`

Baseline fallback/PreviewSnapshot compatibility tests were retained unchanged.

## Verification

The requested commands were attempted in the available execution environment:

```text
$ dotnet restore
bash: line 2: dotnet: command not found
EXIT:127

$ dotnet build
bash: line 5: dotnet: command not found
EXIT:127

$ dotnet test
bash: line 8: dotnet: command not found
EXIT:127
```

Therefore no restore/build/test success is claimed. Windows/.NET 8 verification remains pending.

Additional static checks completed in this environment:

- C# syntax parsing of every Engine and Engine test `.cs` file: no syntax error nodes.
- forbidden dependency scan in `src/KKL.WordStudio.Engine`: no `System.Windows`, OpenXML, Office/Interop, WebView, PDF, `IDataProvider`, `TableElement` or `IReportStructureService` references.
- baseline ownership diff: changes are limited to `src/KKL.WordStudio.Engine/**`, `tests/KKL.WordStudio.Engine.Tests/**` and this Team A report.
- frozen Application contracts and solution/project bootstrap structure are unchanged.

## Contract change request

None created. The frozen Sprint 14 contract was sufficient for Team A implementation.
