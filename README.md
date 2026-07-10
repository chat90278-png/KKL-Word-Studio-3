# KKL Word Studio

Enterprise-grade WPF report designer (.NET 8, C#, MVVM, Clean Architecture).

## Solution layout

| Project | Depends on | Responsibility |
|---|---|---|
| `KKL.WordStudio.Shared` | — | Result type, guards, extensions, framework-independent geometry, theme key constants |
| `KKL.WordStudio.Domain` | Shared | Abstract report model: `Report`, `Page`, `Section`, element hierarchy, visitor pattern, styling. No I/O, no export, no UI. |
| `KKL.WordStudio.Application` | Domain, Shared | Use cases and extensibility contracts: `IReportExporter`, `IDataProvider`, plugin module system |
| `KKL.WordStudio.Infrastructure` | Application, Domain, Shared | `.kws` persistence, concrete exporters (Word/PDF/HTML/Image/Excel), concrete data providers |
| `KKL.WordStudio.Rendering` | Domain, Shared | Design-surface interaction only: hit-testing, selection, snapping, rulers, zoom. No layout/pagination/execution logic (that is a future `Engine` project — see ADR 0002). |
| `KKL.WordStudio.UI` | everything | WPF shell, ViewModels, composition root |

See `docs/adr/` for the reasoning behind these boundaries.


## Sprint 8 scope (this drop)
- **Shell cleanup**: Project Explorer now launches from the top command bar next to `Farklı Kaydet`; Settings/Help placeholders and the now-empty left rail are removed, returning that width to the Excel Workspace.
- **Excel drag & drop**: the empty Kaynak Veri surface accepts `.xlsx` and `.xlsm`, shows active drop feedback, rejects unsupported files in Turkish, and routes valid drops through the existing `OpenWorkbookFromPathAsync` workflow. The reader remains read-only and never writes the source workbook.
- **Real table captions**: `TableElement.Caption` is persisted Domain state (separate from author-note `Description`), flows through `ReportContentDocument`, renders above tables in Preview, and is emitted above the same table in Word. Caption text is directly editable in Properties and inline on the design surface.
- **Başlıktan Al workflow**: the selected table can copy the current text of a real Heading/AltHeading into its caption. This is a value copy; the source heading is not deleted, moved, or live-linked.
- **Word front matter / Ön Belge**: a `.docx` cover or preface can be added/replaced/dropped on Rapor Tasarımı. The original is validated read-only and, on project save, copied as a project-owned `.kws` ZIP asset at `resources/frontmatter/front-matter.docx`. Missing source/asset state is non-fatal and shown as `Ön belge bulunamadı`.
- **Final DOCX composition**: Infrastructure adds the whole front-matter DOCX as a WordprocessingML alternative-format import part (`altChunk`) before generated KKL content, followed by an explicit page break. No raw `Body` child append, Office COM/Interop, PDF conversion, or general DOCX editor was introduced. Preview uses an honest composition placeholder rather than claiming Word fidelity.
- **Worksheet mapping independence**: new mappings are owned by `Worksheet.ColumnMappings`; provider/content/transfer paths read them per worksheet. Legacy `DataSource.ColumnMappings` remains a read fallback for pre-Sprint-8 projects.

See `docs/adr/0012-sprint8-front-matter-captions-and-worksheet-mappings.md`.

## Variant 2.5 UI (Sprint 7 baseline)
Full shell rebuild on top of Sprint 6's stabilized codebase, implementing the approved "Right Docked Context Workspace" design (see `docs/adr/0010-variant-2.5-shell.md`):
- **New shell**: compact title bar + command bar (New/Open/Save/Save As/Export Word), a narrow left navigation rail, and a three-surface main layout — Excel Workspace and Live Word Preview as the two dominant hero surfaces, with a resizable right Context Dock.
- **Project Explorer** moved off the permanent left column onto a rail-triggered slide-out overlay.
- **Excel Workspace**: large spreadsheet-filling grid (column letters, row numbers), spreadsheet-style sheet tabs driving real worksheet switching, a compact range status strip, header-row/data-start/data-end visualization directly in the grid (via `IMultiValueConverter`s, no new Domain state), and an Excel-side Column Mapping drawer.
- **Live Word Preview**: centered, shadowed A4 page on a neutral gray workspace; Fit Page / Fit Width / 75–150% zoom (`PreviewZoomOption`); same `ReportContentDocument` pipeline as before, untouched.
- **Right Context Dock**: segmented Contents/Properties selector, Normal/Collapsed/Expanded states (`DockViewModel`), a Contents outline that projects the real report tree while hiding Body/Section/Container, per-table binding status (Bound/Not configured/Source missing — genuinely resolved, not a `DataSourceName` null-check), contextual Properties (Heading vs Table), and a Change Binding page reached via dock navigation (not a modal).
- **Critical binding defect found and fixed before building the binding UI** (see ADR 0009): `Binding.WorksheetName` now pins a table to a specific worksheet, independent of `ExcelDataSource.ActiveWorksheetName` — previously two tables bound to different worksheets of the same Excel file would both silently follow whichever worksheet was merely "active".
- Real **Open Project** (existing `IProjectService.OpenAsync`) and **Save vs Save As** (remembers the last save path) added to the command bar. No fake dirty-state (`*`) marker — see remaining UX gaps.

## Sprint 6 scope (this drop)
- **Real table grids in Preview.** `PreviewBlock`/`PreviewBlockViewModel` split into Text/Table variants (mirroring `ReportContentNode`); tables now render as an actual `DataGrid`, not "`|`"-joined text — the biggest remaining Preview/Export visual gap.
- **`WordExporter` split into small writer classes** (`Word/WordStyleWriter`, `WordHeaderFooterWriter`, `WordPageLayoutWriter`, `WordParagraphWriter`, `WordTableWriter`, `WordContentWriter`) — no interfaces, no DI per writer; `WordExporter` is now a thin orchestrator. Writer classes are independently unit-tested (`InternalsVisibleTo` for the test project).
- **User-friendly error messages** — `Result.Failure` text no longer leaks raw `ex.Message`; technical detail still goes to the log, the user gets actionable guidance instead.
- **Open file / Open containing folder** after a successful export (`IShellLauncher`, gated by `CanExecute` on `MainViewModel.LastExportedFilePath`).
- Header/Footer/Body preview bands now have a subtle, distinct background tint instead of looking identical to the body.
- Evaluated and explicitly deferred (per this sprint's own instruction): PDF export; `ExcelDataProvider` streaming/lazy enumeration for very large worksheets — **known limitation, not implemented**: the current OpenXML DOM-based reader loads a full worksheet part into memory before iterating. If a real large-file performance problem appears, the fix is the OpenXML SDK's streaming `OpenXmlReader`/`OpenXmlWriter` API (SAX-style), not attempted here.

## Sprint 5 scope (this drop)
- **Real A4 preview**: correctly-proportioned page (from the Report's actual `Page` dimensions), zoom in/out/reset, distinct Header/Body/Footer regions, an optional Table of Contents — all sharing the exact same `ReportContentDocument` `WordExporter` consumes.
- **Real Word page layout**: `.docx` output now has real page size/margins (from Domain `Page`, converted to twips), real `HeaderPart`/`FooterPart` content, a `PAGE` field for page numbers, and a native, updatable `TOC` field.
- **Real heading styles**: `WordExporter` now builds a `StyleDefinitionsPart` with `Heading1`/`Heading2` (with `outlineLvl`) — required for Word's native TOC field to find headings; this was explicitly deferred in ADR 0006 and is now done because the TOC requirement forced it.
- `Report.IncludeTableOfContents` and `Page.ShowPageNumbers` (new, Domain) — TOC entries are *derived* from existing Heading/AltHeading content, never separately authored.
- Report Designer: "Add Header Text" / "Add Footer Text" commands (reusing the existing section-aware `InsertElement`), and a Table-of-Contents toggle.
- `IReportContentBuilder` restructured from a flat node list to `ReportContentDocument` (Header/Body/Footer/TOC/PageLayout regions) — a real gap found when evaluating whether the shared model could serve future PDF/HTML exporters.
- Fixed a real Preview/Export redundancy: `Workspace.ReportContentChanged` (new, narrower than `WorkspaceChanged`) means selecting a tree node no longer forces every bound table's Excel file to be re-read.
- Evaluated and explicitly deferred: `ReportContentBuilder` Strategy/Visitor split (not enough element-type growth yet) and `ExcelDataProvider` streaming (no observed large-dataset problem yet) — see ADR 0007.

## Sprint 4 scope
- **First real Word export.** `WordExporter` (Infrastructure) uses the OpenXML SDK to produce an actual `.docx` — headings, alt headings, paragraphs, and tables (bound or static).
- **Preview and Word Export now share one interpretation.** `Application.Content.IReportContentBuilder` walks the Report exactly once and both `PreviewRenderer` and `WordExporter` consume its output — neither re-decides what counts as a heading or what a bound table's rows are.
- **First real (non-in-memory) data provider.** `ExcelDataProvider` reads actual configured Excel ranges via OpenXML; `IDataProviderRegistry` added (mirrors `IReportExporterRegistry`) since a second provider now exists.
- `Binding.SortFields` is now genuinely applied (structured, no evaluator needed); `Binding.Filter` is intentionally not yet applied (needs an expression evaluator — future Engine work) and is surfaced via `TableContentNode.FilterWasIgnored` rather than silently dropped.
- `Workbook.SourcePath` added — exporting/reading real data requires the actual file location, which wasn't being stored before.
- `Export to Word` wired into the UI (File menu + toolbar), completing New → design → bind data → export end-to-end.

## Sprint 3 scope
- Project Explorer (Data Sources → Excel Files → Worksheets, Reports, Templates placeholder, Settings) — pure projection of the existing Project model, no Domain change
- First working Report Designer: tree view of the active Report, "Add Heading / Add Alt Heading / Add Table" commands funneled through one DnD-ready insertion method
- Table Properties panel: Name, Description, Style (Bold/FontSize), Show Header
- Binding UI: pick a DataSource, see its resolved Worksheet/DataRange live (no new Domain state — a pure lookup, validating ADR 0004)
- Preview foundation: `IReportPreviewRenderer` abstraction (Application) + placeholder implementation (UI), reacts live to `Workspace.WorkspaceChanged`
- `Section.AutoHeight` (default true) — sections flow with content instead of assuming fixed banded-report heights
- `TableElement.Description`
- `ReportElementFlattener` — shared visitor-based tree lookup used by the Designer, Table Properties, and Preview
- MainWindow redesigned: five panels open simultaneously (Project Explorer, Excel Workspace, Report Designer, Table Properties, Preview)
- `Save Project` wired into the UI

## Sprint 2 scope
- Working Excel Workspace: open multiple .xlsx files, list/switch sheets, preview grid with row/column headers, pick a start row, auto-detect data end with manual override, generate column mappings, save the resulting DataSource into the active Project
- `IExcelWorkbookReader` (Application) / `OpenXmlExcelWorkbookReader` (Infrastructure), built on the OpenXML SDK (no new dependency — reused the package already planned for Word export)
- `DataRange` restructured to explicit start/end row, header row, columns, and an auto-vs-manual provenance flag; `RangeReference` is now computed, never stored
- `Binding` extended with `Filter` (Expression) and structured `SortFields`
- `Workspace` refined: generalized `SelectedReportElementId`, added `ActiveDataSourceName`, added a lightweight `IsPreviewActive` flag
- Round-trip test that writes a real .xlsx with the OpenXML SDK and reads it back through the new reader

## Sprint 1.5 scope
- `Project` established as the aggregate root (was `Report`)
- `DataSource` hierarchy: `ExcelDataSource`, `Workbook`, `Worksheet`, `DataRange`, `ColumnMapping`
- First-class `Binding` type on `TableElement`/`DataRegion`
- `Workspace` (Application layer) for cross-panel session state
- `.kws` persistence updated to serialize `Project`

## Sprint 1 scope
- Full solution/project skeleton with correct reference directions
- Abstract report model (Domain) with visitor pattern
- Plugin-oriented extensibility contracts (Application)
- Native `.kws` persistence (zip + JSON), round-trip tested
- Strategy-based exporter registry with stub exporters (Word/PDF/HTML/Image/Excel)
- DI composition root using `Microsoft.Extensions.Hosting` + Serilog

## Not yet implemented
- No dirty-state (`*`) indicator — no reliable change-tracking mechanism exists yet; the title bar simply omits it rather than showing something that could be wrong.
- Dock collapse/expand/restore has no animation (explicitly optional per the Variant 2.5 task; state/layout behavior is correct).
- Column Mapping drawer's "Cancel" does not revert in-progress edits (functionally same as Apply) — the mapping rows are live-bound directly, no snapshot/rollback exists yet.
- Binding.Filter still not executed (needs an expression evaluator — future Engine work, unchanged since ADR 0004/0006).
- PDF/HTML/Image/Excel export (still stubs — only Word is real; PDF explicitly out of scope through Sprint 6)
- True multi-page pagination (Preview renders one accurately-shaped, margined, headed/footed A4 page; splitting body content across multiple physical pages is future Engine work — see ADR 0002/0005/0007)
- `Binding.Filter` execution (needs an expression evaluator — future Engine work; surfaced via `FilterWasIgnored` rather than silently dropped)
- Generated KKL `ImageElement` embedding in Word output (still needs the generic Asset catalog — ADR 0004; imported front-matter DOCX packages are preserved separately by Sprint 8 composition)
- `ExcelDataProvider` streaming/lazy enumeration for very large sheets (evaluated in Sprint 5 and again in Sprint 6, deliberately deferred both times — no observed problem yet, see ADR 0007/0008; the fix if needed is the OpenXML SDK's streaming `OpenXmlReader`/`OpenXmlWriter` API)
- Drag-and-drop report **element reordering** remains deferred; Sprint 8 only adds file-source drop ownership (Excel on Kaynak Veri, DOCX on Rapor Tasarımı).
- Filter/Sort editing UI for Binding (Sort now executes; UI to configure it is still deferred)
- Expression/formula engine, project-level Asset catalog (deliberately deferred, see ADR 0004)
- AvalonDock-based docking (current layout is Grid + GridSplitter, chosen so it can be swapped later without ViewModel changes)

## Restoring and running
```
dotnet restore
dotnet build
dotnet run --project src/KKL.WordStudio.UI/KKL.WordStudio.UI.csproj
```
(Requires a Windows target for the UI project, since it uses WPF.)
