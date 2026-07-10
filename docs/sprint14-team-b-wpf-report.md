# Sprint 14 — Team B WPF Document Surface Report

## Role

Team B — WPF Document Surface Lead / Interaction Engineer.

Baseline: `KKL.WordStudio-Sprint14-Contract-Baseline-Stabilized.zip`.
The frozen Sprint 14 Application layout/imported-document contracts were consumed without modification.

## Result

The center **RAPOR TASARIMI** surface now consumes `PreviewSnapshot.Layout.Pages` as its authoritative visual input. The compatibility `HeaderBlocks`, `BodyBlocks`, `FooterBlocks`, `TableOfContents`, and single-page `PageLayout` snapshot properties remain untouched in Application/Preview orchestration, but Team B no longer renders them.

### Physical page stack

- `DocumentLayoutResult.Pages` is projected in supplied order.
- Each page uses its own `PageLayout` width and height.
- Millimeters are converted to WPF DIPs with `96 / 25.4`.
- Each `PositionedPageBlock` is placed by its contract `X/Y/Width/Height` inside a clipped page `Canvas`.
- Pages are white physical surfaces with border/shadow and a gray workspace gap.
- The full page stack receives one presentation scale through a `LayoutTransform`; block coordinates are not individually zoom-recalculated.
- Fit Width uses the widest projected page and viewport width.
- Fit Page uses the widest/tallest projected page bounds and viewport dimensions.
- Percent zoom modes remain 75/100/125/150 and are runtime-only.

### Payload rendering

Added UI-only page/block projections for every frozen payload type:

- `TextPageBlockPayload`: run-preserving WPF `Run` inlines with bold/italic/underline, point-size conversion, optional font family, and paragraph alignment.
- `TablePageBlockPayload`: caption on the first fragment, fragment rows only, header/header-repeat presentation, subtle continuation hint, and visible `SourceError`.
- `TocPageBlockPayload`: level indentation and right-aligned payload page number.
- `ImagePageBlockPayload`: image-byte decode with `Stretch=Uniform`; honest named placeholder when bytes cannot be rendered.
- `PageNumberPageBlockPayload`: renders the actual payload page number.
- `UnsupportedPageBlockPayload`: visible subdued unsupported-content placeholder with description.

No heading/table/source semantics are re-derived in WPF.

## Shared selection and preview delete

Generated blocks interact only when both `IsEditableReportElement=true` and `ElementId != null`.

- Single click calls the existing Workspace selected-element state.
- Selection synchronization compares every visible page fragment to the one `Workspace.SelectedReportElementId`.
- Therefore split fragments with the same `ElementId` share selection highlighting.
- Front-matter and derived blocks are not selectable as report elements.
- Delete key and right-click **Sil** route through the same Preview ViewModel deletion path.
- Destructive confirmation uses the existing `IDialogService`.
- Product deletion is delegated to `IReportStructureService.Delete`.
- The preview never removes a layout block directly.
- Nearby remaining editable element selection is chosen from the current visible element-id order after deletion.
- Table deletion does not touch data-source/working-data state because only the structure service is called.

Selection-only Workspace changes still run only `SyncSelection`; layout rebuild remains subscribed to `ReportContentChanged`.

## Preview drag/drop reorder

Preview drag identity is the real `ElementId` serialized into a WPF drag-data format.

- Any split fragment can start a drag for the same real element id.
- Only generated editable blocks are targets.
- Top 25% resolves to `Before`.
- Bottom 25% resolves to `After`.
- Middle zone on a payload-classified Heading/AltHeading resolves to `Into`.
- Middle zone on non-headings resolves deterministically by pointer half to `Before`/`After`.
- Drop feedback is a top insertion line, target outline, or bottom insertion line.
- Product movement delegates to `IReportStructureService.Move(sourceId, targetId, mode)`.
- Invalid hierarchy/cross-section moves surface the Application service's Turkish `Result.Error` non-modally in the document-surface status line.
- `Pages` and `Blocks` collections are never reordered by drag/drop code.

## Inline editing preservation

- Double-click generated text fragments opens the existing semantic text commit path.
- Table caption editing is available only on fragment 0; this preserves first-fragment caption semantics and still allows adding an empty caption there.
- Table display-header editing is available on any editable fragment header.
- Excel-derived table cells remain read-only.
- Imported/front-matter blocks are preview-only.

All commits continue through `IReportEditingService` and then `Workspace.NotifyReportContentChanged()`.

## Front matter page display

The old large composition placeholder was removed from the document body.

- Front-matter pages supplied by `Layout.Pages` render before generated report pages in final layout order.
- They use the same physical page visual and payload templates.
- Contract non-editability is preserved through `IsEditableReportElement`/nullable `ElementId`.
- Add/replace/remove front-matter commands remain in the compact surface toolbar.
- Missing source state is shown honestly through the existing front-matter availability/status flow; no fake page is created.

## Files changed / added

Changed:

- `src/KKL.WordStudio.UI/ViewModels/PreviewViewModel.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml.cs`

Added:

- `src/KKL.WordStudio.UI/Preview/PreviewTextBlockControl.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewInteractionHelpers.cs`
- `docs/sprint14-team-b-wpf-report.md`

No files outside Team B ownership were modified.

## Focused acceptance / static verification

A local source-level acceptance audit passed **19/19** checks covering:

- XAML XML well-formedness;
- `Layout.Pages` as the authoritative UI input;
- supplied page order preservation;
- `96 / 25.4` geometry conversion;
- Canvas X/Y placement;
- one scale transform for the page stack;
- Fit Width page-width/viewport math;
- same-ElementId fragment selection;
- structure-service delete path;
- real ElementId drag identity;
- structure-service move path;
- Before/Into/After resolver;
- removal of the old front-matter placeholder;
- non-editable imported block guard;
- payload page-number rendering;
- visible unsupported block rendering;
- WPF Run inline rendering;
- absence of pagination implementation in Team B files;
- absence of OpenXML/COM/WebView references in Team B implementation.

XAML event-handler names were cross-checked against code-behind and all referenced handlers were found. Modified/new C# files passed a lexical delimiter-balance check. A ZIP-vs-workspace ownership hash comparison found **zero ownership violations**.

### Build / test status

The execution environment does not contain the .NET CLI:

```text
bash: line 1: dotnet: command not found
```

Therefore `dotnet restore`, `dotnet build`, and `dotnet test` could not be run here. No green build/test result is claimed. Windows/.NET 8 WPF verification remains pending and is the final runtime truth.

## Contract change request

None. The frozen Sprint 14 contract was sufficient for Team B implementation. No `docs/CONTRACT_CHANGE_REQUEST-B.md` was created.
