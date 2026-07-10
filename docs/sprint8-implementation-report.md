# Sprint 8 Implementation Report

## Baseline Used

The only implementation baseline was the verified full-project archive `KKL.WordStudio-Sprint7.zip` supplied for this task. It was extracted directly into a fresh Sprint 8 workspace. Sprint 6 / Variant 2.5 cached workspaces and older branches were not used as code baselines.

## Executive Summary

Sprint 8 implements only the requested shell cleanup, Excel file drag-and-drop intake, explicit table caption/title semantics, heading-to-caption copy workflow, Word front-matter composition foundation, and the focused worksheet-mapping ownership correction.

The implementation keeps the structured report model intact. It does not add editable Excel working data, arbitrary DOCX-to-`ReportElement` conversion, a general Word editor, Office COM/Interop, PDF conversion, a pagination engine, report-element drag reorder, undo/redo, or Sprint 9 work.

The most material architecture changes are:

- `TableElement.Caption` is now an explicit persisted display semantic separate from author-note `Description`.
- `Project.FrontMatter` holds imported cover/preface metadata while `.kws` owns the DOCX binary as a separate ZIP asset.
- final Word composition uses a narrow Infrastructure `WordFrontMatterComposer` and WordprocessingML alternative-format import (`w:altChunk`) rather than cloning source `Body` XML.
- `Worksheet.ColumnMappings` is the primary mapping owner for newly configured worksheet datasets; legacy `DataSource.ColumnMappings` is retained as a compatibility fallback.

## Shell Cleanup

The Settings gear and Help placeholder were removed with the obsolete left navigation rail. The rail became decorative after its launchers were removed, so its width was reclaimed for the Excel Workspace.

`Proje Gezgini` now launches the existing Project Explorer overlay from the top command bar next to `Farklı Kaydet`.

Current command order is:

`Yeni → Aç → Kaydet → Farklı Kaydet → Proje Gezgini`

The Project Explorer overlay implementation and navigation behavior remain the existing Sprint 7 behavior; only launcher ownership/location changed.

## Excel Drag & Drop

`Kaynak Veri` now owns the Excel drop target. The empty workspace shows:

- `Excel dosyasını buraya sürükleyip bırakın`
- `.xlsx ve .xlsm · kaynak dosya değiştirilmez`
- `veya Excel Dosyası Aç`

`DragEnter` / `DragOver` set a visible active-drop state. WPF code-behind only routes the OS gesture and calculates the native copy/none drop effect. File-type and multi-file decisions live in `SourceFileDropValidator`.

Accepted initial extensions are `.xlsx` and `.xlsm`. Legacy `.xls` is rejected in Turkish.

A valid drop calls `ExcelWorkspaceViewModel.HandleDroppedFilesAsync`, which then calls the existing `OpenWorkbookFromPathAsync` workflow used by the file picker and Project Explorer. There is no second workbook-import path.

The existing OpenXML reader/provider continue opening spreadsheet packages read-only. A new source-hash test, `OpenWorkbookAsync_DoesNotModifySourceWorkbook`, was added to characterize that requirement.

The model can hold multiple project data sources, but there is no existing transactional/batch intake command for a multi-file OS drop. Sprint 8 therefore accepts the first supported file deterministically and reports the limitation instead of inventing a separate batch-import path.

## Table Caption Architecture

Existing model:

`TableElement.Name` represented designer identity. `TableElement.Description` represented free-text author notes in Properties. There was no correct visible table-caption semantic.

Final model:

`TableElement.Caption : string?` is the explicit persisted visible title/caption. `Description` keeps its existing author-note purpose.

`ReportContentBuilder` copies the value into `TableContentNode.Caption`. The same semantic content node feeds Preview and Word output.

Architecture change required: YES

The change is intentionally small: one explicit Domain property plus propagation through the existing shared report-content semantic pipeline. No separate preview-only caption state and no misuse of `Description` were introduced.

## Use Heading as Table Title Workflow

For a selected table, Properties now shows a compact list of actual Heading / AltHeading elements and the command `Başlıktan Al`.

`IReportEditingService.UseHeadingTextAsTableCaption` validates both real report elements, reads the selected heading's current text, and copies that text into `TableElement.Caption`.

The operation is value-copy semantics:

- no live `Table → Heading` reference is created;
- the source heading is not deleted;
- the source heading is not moved;
- later heading edits do not silently mutate the table caption.

`UseHeadingTextAsTableCaption_CopiesTextWithoutDeletingHeading` locks this behavior in source tests.

## Table Caption Preview

The table preview block renders the semantic caption immediately above the table grid. A captionless table shows the inline hint `Tablo başlığı eklemek için çift tıklayın`.

Double-clicking that caption area opens a scoped caption editor. Commit goes through `IReportEditingService.CommitTableCaption`, mutates the real `TableElement`, and triggers the existing shared report-content refresh. Excel-derived data cells remain read-only.

The table remains one selectable structured element; the caption is not modeled as a separate report element.

## Table Caption Word Output

`TableContentNode.Caption` reaches `WordContentWriter`. When non-empty, `WordParagraphWriter.BuildTableCaptionParagraph` writes the caption paragraph immediately before the table.

This is the same semantic value used by Preview. The existing `WordExporterTests` method was preserved and extended to assert that `Satış Tablosu` appears in generated Word body text.

Caption persistence is covered separately by `TableCaption_RoundTripsThroughProjectPersistence`, and semantic inclusion is covered by `TableCaption_IsIncludedInReportContentDocument`.

## Front-Matter DOCX Architecture Assessment

The imported DOCX is not converted into `ReportElement` objects. `Project.FrontMatter` is project-level imported document state because arbitrary Word content is not part of the structured KKL report design model.

Actual OpenXML composition risks evaluated were:

- style and style-id collisions;
- numbering/abstract-numbering identifier collisions;
- theme references;
- image/media parts and relationship references;
- hyperlinks and external relationships;
- section properties and page/section boundaries;
- source headers and footers;
- footnotes and endnotes;
- custom XML and ancillary package parts;
- relationship-id collisions;
- the false assumption that source `Body` XML alone contains the complete document graph.

A raw source-`Body` child append was rejected because it would require a real package-graph merge/remapping algorithm for these parts and relationships.

The selected Sprint 8 architecture uses WordprocessingML alternative-format import (`AlternativeFormatImportPart` + `w:altChunk`) in a narrow Infrastructure composer. The imported DOCX remains a complete nested WordprocessingML package part instead of having its body elements cloned into the generated package.

## Front-Matter Ownership / Persistence

Referenced external file or project-owned embedded asset:

Project-owned embedded asset.

Reason:

`.kws` is already a ZIP container. On project save, the readable imported DOCX is copied to the separate entry `resources/frontmatter/front-matter.docx`. `project.json` stores only front-matter metadata/state; arbitrary DOCX binary is not base64-encoded into JSON.

`ResolvedFilePath` is runtime-only and `[JsonIgnore]`. When a `.kws` project opens, the embedded asset is materialized to a private temporary path by Infrastructure. File-system checks remain outside Domain.

Portability consequence:

After the project is saved with a readable front-matter source, the `.kws` file carries its own DOCX asset and no longer depends on the original absolute path for normal reopen/export. The original source path is retained as informational/compatibility fallback metadata.

If a project state has neither an embedded asset nor a still-readable original source, the front-matter state is retained and the project still opens.

## DOCX Drop Workflow

`Rapor Tasarımı` owns the `.docx` drop target. It does not share a global drop zone with Excel.

The surface exposes `Ön Belge Ekle` and accepts `.docx` from the file picker or report-design drop target. Unsupported files are rejected in Turkish.

`OpenXmlFrontMatterDocumentService` validates the file extension, existence, OpenXML package, `MainDocumentPart`, document, and body by opening the source read-only. It returns `FrontMatterDocument` state and never writes the source.

`FrontMatterImport_ValidatesPackageWithoutModifyingSource` hashes a real generated DOCX before and after import validation to characterize source immutability.

The UI displays:

`ÖN BELGE`

`<dosya adı>`

`Ön belge final Word dosyasına eklenecek`

and `Değiştir` / `Kaldır` actions. No unreliable page count is invented.

## Final DOCX Composition

The exact export order is:

1. create the generated destination WordprocessingML package and main body;
2. add the source DOCX bytes as an `AlternativeFormatImportPart` with type `WordprocessingML`;
3. append a `w:altChunk` anchor as the first body content;
4. append an explicit page-break paragraph;
5. append the existing KKL TOC field when enabled;
6. append generated KKL semantic body nodes;
7. append final section properties, header/footer references, and page layout;
8. explicitly save the main document.

Front matter
+
generated KKL report

are therefore combined by package-level alternative-format import before generated content.

This is not a naive `Body` append: the implementation never enumerates and clones source `Body.ChildElements` into the host. The complete source DOCX is fed to its own import part; the destination body only receives the relationship-backed `w:altChunk` anchor plus the explicit page boundary.

The composition fixture generates a real front-matter DOCX with text, a distinct `CoverDistinct` paragraph style, and a PNG media part. The composition test verifies the first body node is `AltChunk`, generated report text comes later, the page-break paragraph follows the import anchor, the imported nested package still contains front-matter text/style/media, and the final host package can be reopened with OpenXML.

Existing header/footer/PAGE/TOC/page-layout writers and explicit save behavior were left in the exporter pipeline after the composition anchor.

## Front-Matter Preview

Limited native preview or composition placeholder:

Composition placeholder.

Reason:

The current WPF preview is a structured `ReportContentDocument` renderer, not a Word layout/rendering engine. Rendering an arbitrary DOCX faithfully would require a major pagination, style, relationship, field, drawing, header/footer, and section rendering subsystem that is outside Sprint 8.

Known limitations:

- no pixel-faithful Word cover rendering;
- no Word pagination or reliable page count;
- no native rendering of imported Word styles, images, fields, headers/footers, or section layout in WPF;
- the final composed DOCX is authoritative;
- alternative-format import must be processed by a Word consumer that supports WordprocessingML `altChunk`; Sprint 8 does not flatten the imported package into host XML itself.

The placeholder appears before KKL report preview content and states that the front matter will be added to the final Word file.

## Missing Source Behavior

Project open is non-fatal when front-matter state exists but the source is unavailable.

`KwsProjectRepository.OpenAsync` first looks for the embedded project asset. If present, it materializes that asset and normal operation continues even if the original DOCX was moved or deleted.

If no embedded asset is present, Infrastructure falls back to the remembered original path when it still exists. If neither source exists, `Project.FrontMatter` remains intact, `ResolvedFilePath` stays unavailable, and the Preview status becomes exactly `Ön belge bulunamadı`.

The existing project is not rejected and the front-matter state is not silently deleted. The visible `Değiştir` action provides a clean replace/relink-by-replacement path.

## Worksheet Mapping Independence

Scenario:

One `ExcelDataSource` / workbook contains:

- `Sheet1`: column `B → PartName`
- `Sheet2`: column `B → EngineType`

Test:

`WorksheetMappings_AreIndependentPerWorksheet`

The test creates a real two-sheet OpenXML workbook, configures separate `Worksheet.ColumnMappings`, reads both worksheets through the same `ExcelDataProvider`, and asserts Sheet1 exposes `PartName` without `EngineType` while Sheet2 exposes `EngineType` without `PartName`.

Result:

The Sprint 7 source-level mapping model was a real defect for this scenario. Sprint 8 applies the smallest local correction: `Worksheet.ColumnMappings` is primary in transfer, provider, report-content fallback field resolution, and legacy table-column materialization. `DataSource.ColumnMappings` remains a read fallback only when a worksheet-specific collection is empty.

The new characterization/regression test is present but was not executed in this sandbox because the `dotnet` CLI is unavailable. Static path inspection confirms the worksheet override is used in all four corrected paths; this is not reported as a passing runtime test.

Architecture change required: YES

The change is local to mapping ownership/resolution. No broad DataSource hierarchy redesign was started.

## Existing Sprint 7 Features Preserved

- Turkish Variant 2.5 shell structure and visual resource system, with only the requested launcher/rail cleanup.
- Excel workbook/worksheet loading and switching.
- configured `DataRange` behavior.
- optional column mapping.
- direct `Word'e Aktar` Excel-to-report flow.
- stable per-table `Binding.WorksheetName`.
- worksheet override behavior in the provider.
- structured report design surface.
- Preview / Contents / Properties shared selection state.
- inline heading editing.
- inline table display-header editing.
- `TableColumn.SourceField` display/source identity separation.
- read-only report data cells.
- Word export.
- `.kws` project persistence.

No Sprint 7 baseline test method was removed. Static source inventory found 56 baseline test methods and all 56 still present.

## Stabilization Fixes Preserved

Verified in source/static regression scan:

- OpenXML Domain `Workbook` / `Worksheet` aliases.
- `Workbook.SourcePath`.
- `Serilog.Extensions.Hosting` package reference.
- existing `System.IO` / `File.Create` qualification fixes remain untouched.
- `PreferredObjectCreationHandling.Populate`.
- DataSource JSON polymorphism.
- ReportElement JSON polymorphism.
- `DataRange.RangeReference` `[JsonIgnore]` computed persistence semantics.
- explicit Styles/Header/Footer `Save(...)` calls.
- `A2:C10` persistence semantics in existing tests.
- `Binding.WorksheetName`.
- worksheet override behavior.
- `TableColumn.SourceField`.
- direct transfer tests.
- Preview/Contents/Properties shared selection state.
- existing Word export tests.
- existing project persistence tests.

The static scan found no `Microsoft.Office`/Interop package or code path, no WebView package, and no new OpenXML source package opened with edit mode `true`.

## Files Added

- `docs/adr/0012-sprint8-front-matter-captions-and-worksheet-mappings.md`
- `docs/sprint8-implementation-report.md`
- `src/KKL.WordStudio.Application/Abstractions/IFrontMatterDocumentService.cs`
- `src/KKL.WordStudio.Application/Importing/SourceFileDropValidator.cs`
- `src/KKL.WordStudio.Domain/Projects/FrontMatterDocument.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Composition/WordFrontMatterComposer.cs`
- `src/KKL.WordStudio.Infrastructure/Word/FrontMatterSourcePathResolver.cs`
- `src/KKL.WordStudio.Infrastructure/Word/OpenXmlFrontMatterDocumentService.cs`
- `src/KKL.WordStudio.UI/ViewModels/HeadingCaptionCandidateViewModel.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint8CaptionAndDropTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/WorksheetMappingIndependenceTests.cs`

## Files Modified

- `README.md`
- `docs/architecture-diagram.md`
- `src/KKL.WordStudio.Application/Content/ReportContentBuilder.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentNode.cs`
- `src/KKL.WordStudio.Application/Editing/ReportEditingService.cs`
- `src/KKL.WordStudio.Application/Preview/PreviewSnapshot.cs`
- `src/KKL.WordStudio.Application/Transfer/ExcelReportTransferService.cs`
- `src/KKL.WordStudio.Domain/DataSources/Worksheet.cs`
- `src/KKL.WordStudio.Domain/Elements/TableElement.cs`
- `src/KKL.WordStudio.Domain/Projects/Project.cs`
- `src/KKL.WordStudio.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `src/KKL.WordStudio.Infrastructure/Excel/ExcelDataProvider.cs`
- `src/KKL.WordStudio.Infrastructure/Excel/OpenXmlExcelWorkbookReader.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordContentWriter.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordParagraphWriter.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/WordExporter.cs`
- `src/KKL.WordStudio.Infrastructure/Persistence/KwsProjectRepository.cs`
- `src/KKL.WordStudio.UI/MainWindow.xaml`
- `src/KKL.WordStudio.UI/Preview/PreviewRenderer.cs`
- `src/KKL.WordStudio.UI/Services/FileDialogService.cs`
- `src/KKL.WordStudio.UI/Services/IFileDialogService.cs`
- `src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/PropertiesViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/TablePreviewBlockViewModel.cs`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml.cs`
- `src/KKL.WordStudio.UI/Views/PropertiesView.xaml`
- `tests/KKL.WordStudio.Infrastructure.Tests/OpenXmlExcelWorkbookReaderTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/WordExporterTests.cs`

## Files Removed

- None.

## Tests Added

- `OpenWorkbookAsync_DoesNotModifySourceWorkbook` — `KKL.WordStudio.Infrastructure.Tests/OpenXmlExcelWorkbookReaderTests.cs`
- `TableCaption_RoundTripsThroughProjectPersistence` — `KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `FrontMatterState_RoundTripsThroughProjectPersistence` — `KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `FrontMatterImport_ValidatesPackageWithoutModifyingSource` — `KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `MissingFrontMatterSource_DoesNotPreventProjectOpen` — `KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `ComposedWordDocument_ContainsFrontMatterBeforeGeneratedReport` — `KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `ComposedWordDocument_ReopensWithOpenXml` — `KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `WorksheetMappings_AreIndependentPerWorksheet` — `KKL.WordStudio.Infrastructure.Tests/WorksheetMappingIndependenceTests.cs`
- `TableCaption_IsIncludedInReportContentDocument` — `KKL.WordStudio.Application.Tests/Sprint8CaptionAndDropTests.cs`
- `UseHeadingTextAsTableCaption_CopiesTextWithoutDeletingHeading` — `KKL.WordStudio.Application.Tests/Sprint8CaptionAndDropTests.cs`
- `ExcelDropValidation_AcceptsXlsxAndRejectsUnsupportedFiles` — `KKL.WordStudio.Application.Tests/Sprint8CaptionAndDropTests.cs`
- `FrontMatterDropValidation_AcceptsDocxAndRejectsUnsupportedFiles` — `KKL.WordStudio.Application.Tests/Sprint8CaptionAndDropTests.cs`
- `MultipleExcelDrop_AcceptsFirstSupportedFileAndReportsLimitation` — `KKL.WordStudio.Application.Tests/Sprint8CaptionAndDropTests.cs`

Static source inventory: 13 new Sprint 8/regression test methods. Total source test inventory is 69 methods.

## Tests Preserved

All 56 Sprint 7 baseline test methods are still present. No baseline test file and no baseline `[Fact]`/`[Theory]` method was removed.

`WordExporterTests` was modified only to add a real table caption to the existing export scenario and assert that caption text reaches the generated Word document; the existing test method remains.

## Verification

dotnet restore:

NOT RUN successfully — `dotnet restore` was invoked in the Sprint 8 project root and failed immediately with `dotnet: command not found` (exit code 127). The sandbox has no `dotnet` CLI.

dotnet build:

NOT RUN successfully — `dotnet build` was invoked and failed immediately with `dotnet: command not found` (exit code 127). WPF UI build success is **not claimed**.

dotnet test:

NOT RUN successfully — `dotnet test` was invoked and failed immediately with `dotnet: command not found` (exit code 127). No new or existing test is claimed as passing in this environment.

Supplemental static verification actually performed:

- required delivery roots exist;
- 21 XAML/XML/project/props files parsed with zero XML parse failures;
- XAML code-behind event-handler cross-check found zero missing handlers;
- 167 C# source files passed a lexical delimiter-balance scan with zero reported brace/parenthesis/bracket issues;
- Sprint 7 baseline test inventory: 56 methods; current inventory: 69; missing baseline methods: 0;
- required stabilization markers were found;
- prohibited-scope scan found no Office Interop/WebView/read-write OpenXML source-open hit;
- no `bin/` or `obj/` directories are included.

These static checks do not replace compilation or runtime tests. The user's Windows/.NET 8 machine remains the final build/WPF/runtime verification source.

## Architecture Impact

YES

The architecture impact is bounded and documented in ADR 0012:

- explicit table-caption Domain semantic;
- project-level front-matter imported asset state;
- narrow Infrastructure DOCX import/composition/persistence services;
- worksheet-owned mapping correction with legacy fallback.

No generic document engine, asset catalog redesign, Word renderer, Excel working-data model, or new application layer was introduced.

## Remaining UX Gaps

- Front-matter Preview is a composition placeholder, not a Word renderer.
- `altChunk` is intentionally not flattened by KKL Word Studio; compatibility depends on the target Word consumer processing WordprocessingML alternative-format import.
- Front-matter replacement is supported via `Değiştir`; there is no separate file-browser-style `Relink` command.
- Multi-file Excel drop accepts the first supported file and reports the limitation; no batch intake transaction was invented.
- Table heading candidates list available Heading/AltHeading elements; no proximity-ranking heuristic was introduced.
- No reliable dirty-state indicator or undo/redo architecture exists.
- True pagination remains deferred.
- Generated KKL `ImageElement` embedding remains deferred; imported front-matter package media is preserved separately.
- Editable Excel working data was **not started**.
- Sprint 9 was **not started**.
