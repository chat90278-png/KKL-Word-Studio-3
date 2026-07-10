# KKL Word Studio — Sprint 14 Integration Checklist

Status: Team D QA gate checklist  
Baseline: `KKL.WordStudio-Sprint14-Contract-Baseline-Stabilized.zip`  
Contract: Sprint 14 shared contract v1.0, frozen

Use this checklist when Team A, Team B, and Team C branches are returned and before accepting the integrated Sprint 14 workspace. Do not waive a failed item by deleting or weakening a test. Any contract change must have an explicit reviewed `CONTRACT_CHANGE_REQUEST-<TEAM>.md` and an integration decision.

## TEAM A REVIEW — Engine / Pagination

### Ownership and contract

- [ ] Diff Team A against the contract baseline; every production change is under `src/KKL.WordStudio.Engine/**` and every Team A test change is under `tests/KKL.WordStudio.Engine.Tests/**`.
- [ ] Team A report exists at `docs/sprint14-team-a-layout-report.md`.
- [ ] No Application Layout contract file, ImportedDocuments contract file, `PreviewSnapshot`, solution bootstrap, or unrelated production file was edited.
- [ ] No duplicate Page/Block/Layout contract was introduced in Engine.
- [ ] No `System.Windows`, WPF control/type, UI reference, Rendering reference, Infrastructure reference, `DocumentFormat.OpenXml`, `WordprocessingDocument`, `SpreadsheetDocument`, Excel provider, or source-file read path exists in Engine.
- [ ] Engine consumes `DocumentLayoutRequest.ReportContent` and optional `FrontMatter`; it does not read Project, Report, Excel, `TableElement.Binding`, or `TableElement.Sources` to re-derive semantics.

### Page flow and generated report semantics

- [ ] `DocumentLayoutResult.Pages` is in final preview order and `PageNumber` is sequential, absolute, 1-based: `1..N` with no gaps or duplicates.
- [ ] Generated page geometry comes from `ReportContentDocument.PageLayout`; body placement respects margins.
- [ ] Header/footer layout repeats on every generated report page as required by the semantic document.
- [ ] Page-number payload uses the actual absolute page number, including pages preceded by imported front matter.
- [ ] Heading/AltHeading keep-with-next behavior prevents a heading from being stranded as the last body block when its following fragment can move with it.
- [ ] Table fragmentation is row-based; a data row is atomic in Sprint 14.
- [ ] Continuation table fragments preserve the source table `ElementId`.
- [ ] `FragmentIndex` starts at 0 and increases for fragments of the same semantic table/block.
- [ ] `IsContinuation` is false for the first fragment and true for continuation fragments.
- [ ] Continuation table fragments repeat the table header and set `IsHeaderRepeated = true` when a header exists.
- [ ] Table caption is present only on the first fragment.
- [ ] `StartRowIndex` is the zero-based source data-row index of the first row in that fragment; rows are neither dropped nor duplicated at fragment boundaries.
- [ ] `SourceError` remains visible in a payload/block and is not silently converted to partial output.
- [ ] An over-height atomic row is placed once, emits a warning, advances the layout state, and cannot cause an infinite loop.
- [ ] Every pagination loop has a demonstrable progress invariant: consumed block, consumed row, page advance, or explicit terminal warning path.
- [ ] Long loops and repeated convergence passes call `CancellationToken.ThrowIfCancellationRequested()` at useful boundaries.

### TOC and convergence

- [ ] TOC page numbers map heading `ElementId` values to final absolute generated-document page numbers.
- [ ] TOC projection cannot reference page 0, negative pages, or pages greater than final `Pages.Count`.
- [ ] TOC pagination/convergence is bounded by an explicit iteration limit or another provably finite state transition.
- [ ] A non-convergent/unstable TOC state emits a warning or deterministic fallback; it cannot loop forever.
- [ ] Re-running layout on the same immutable semantic input produces deterministic page/block ordering and page numbers.

### Imported front matter flow

- [ ] Front-matter pages precede every generated report page.
- [ ] Each imported section uses its own `PageLayout` page size, orientation, and margins.
- [ ] `ImportedExplicitPageBreakBlock` forces a page transition; no implicit Word page-break guessing is performed.
- [ ] Imported unsupported blocks are represented by visible `Unsupported` placeholders and/or warnings.
- [ ] Imported blocks have `ElementId = null` and `IsEditableReportElement = false`.

## TEAM B REVIEW — WPF Paginated Preview / Interaction

### Ownership and contract

- [ ] Diff Team B against the contract baseline; changes stay inside the frozen Team B ownership paths: `src/KKL.WordStudio.UI/Preview/**`, `src/KKL.WordStudio.UI/ViewModels/Preview*`, `src/KKL.WordStudio.UI/Views/PreviewView.xaml`, `PreviewView.xaml.cs`, and new UI-only preview converters/styles/viewmodels.
- [ ] Team B report exists at `docs/sprint14-team-b-wpf-report.md`.
- [ ] No Application contract, Engine, Infrastructure, Domain, Rendering, solution/bootstrap, or unrelated UI source was edited.
- [ ] UI renders `PreviewSnapshot.Layout.Pages` as the page source of truth. Compatibility `HeaderBlocks/BodyBlocks/FooterBlocks/TableOfContents/PageLayout` are not used to create a second preview pagination model.

### No UI-owned execution/pagination

- [ ] Reject any `SpreadsheetDocument` or `WordprocessingDocument` use in UI Preview code.
- [ ] Reject body-row pagination loops, table row fragmentation, page-break calculation, keep-with-next logic, or TOC page-number calculation in UI Preview.
- [ ] Reject any UI-only Page/Block/Layout result contract that duplicates the Application Layout contract.
- [ ] Page collections and `PositionedPageBlock` collections are rendered as layout output; they are never mutated as persisted report state or reordered directly to implement report structure changes.

### Page rendering

- [ ] Multiple physical page surfaces render in `Layout.Pages` order with visible page gaps.
- [ ] Page dimensions and orientation are derived from each `DocumentPageLayout.PageLayout`.
- [ ] Blocks are positioned from millimeter coordinates using one consistent mm-to-DIP conversion and zoom is applied as a viewport/render concern, not by changing layout coordinates.
- [ ] Front-matter and generated pages are visually distinguishable only where useful; both remain in one absolute page sequence.
- [ ] The old front-matter placeholder is removed/suppressed only when actual layout front-matter pages exist. Missing/unavailable front matter still has a Turkish friendly status path.
- [ ] Unsupported blocks render an honest visible placeholder; they are not hidden.
- [ ] Page-number blocks display the engine payload number and do not recompute from ItemsControl index.

### Selection, delete, and drag/drop

- [ ] Clicking any generated editable block selects by `ElementId` and synchronizes the same real ReportElement with Contents/Properties.
- [ ] Multiple fragments with the same `ElementId` share selection state; clicking a continuation fragment selects the same source table.
- [ ] Front-matter blocks, page-number blocks, TOC-only/derived items, and blocks with `IsEditableReportElement = false` are not selectable as editable ReportElements.
- [ ] Preview delete shows the required confirmation and delegates real deletion to `IReportStructureService`; it does not remove a layout block from `Pages`/`Blocks` directly.
- [ ] Delete never touches `Project.DataSources`, `Worksheet.WorkingData`, or source Excel files.
- [ ] Drag payload carries a `Guid ElementId`, not a page index, visual object, block index, or serialized layout block.
- [ ] Drop target resolves a target `ElementId`; UI calculates only the gesture intent `Before / Into / After`.
- [ ] Actual move/reorder delegates to `IReportStructureService`; no second ordering engine exists in Preview.
- [ ] Front matter is not draggable/deletable as ReportElements.

### WPF/XAML adversarial review

- [ ] Every ViewModel command exposed for preview interaction is wired from XAML or intentionally documented as unused; no dead command surface is accepted silently.
- [ ] Every XAML event handler exists with the correct WPF signature and code-behind owner.
- [ ] Build output is checked for invalid dependency properties, invalid attached properties, wrong enum values, and stale binding/property names.
- [ ] Binding paths used in `PreviewView.xaml` exist on the actual DataContext/item types.
- [ ] Converter resources and styles are declared before use and have compatible value/target types.
- [ ] Review local WPF property values versus style triggers: local `Background`, `BorderBrush`, `Opacity`, `Visibility`, `IsHitTestVisible`, etc. must not unintentionally override selection/hover/error triggers.
- [ ] Selection trigger precedence is manually verified for first and continuation fragments.
- [ ] `ReportContentChanged` remains the rebuild trigger; broad `WorkspaceChanged` is not substituted as the expensive preview rebuild source. Selection-only synchronization may remain narrow on `WorkspaceChanged`.

## TEAM C REVIEW — Imported DOCX / Word Semantics

### Ownership and contract

- [ ] Diff Team C against the contract baseline; changes stay inside the frozen Infrastructure Word/export/composition/DI ownership paths and Sprint 14 Infrastructure tests.
- [ ] Team C report exists at `docs/sprint14-team-c-docx-report.md`.
- [ ] No Application Layout/ImportedDocuments contract, PreviewSnapshot, Engine, UI, Domain, Rendering, or solution/bootstrap structure was edited.
- [ ] Exactly one reviewed Infrastructure implementation of `IImportedDocumentPreviewProvider` is introduced and DI registration is unambiguous.

### Source DOCX extraction safety

- [ ] Every source/imported DOCX `WordprocessingDocument.Open(...)` call is reviewed; source packages are opened with edit mode `false`.
- [ ] No source/embedded DOCX is modified, saved, rewritten, normalized in place, or opened for write access during preview extraction.
- [ ] Extraction reads the current `Project.FrontMatter` state and returns the frozen `ImportedDocumentPreviewResult` semantics for no front matter versus persisted-but-missing front matter.
- [ ] Image extraction resolves the image part through the run/drawing relationship ID and the owning OpenXML part; it does not guess package paths or filenames.
- [ ] Image bytes and content type are preserved; available extents are converted deterministically to millimeters.
- [ ] Section page size/orientation/margins are extracted into `PageLayout` with documented defaults for absent properties.
- [ ] Explicit page-break extraction recognizes explicit page-break constructs only. Reject inferred breaks based on text length, paragraph count, rendered page assumptions, or Word UI page positions.
- [ ] Paragraph runs preserve supported text, bold/italic/underline, font size/family when available, and paragraph alignment.
- [ ] Tables are extracted as cell text; repeat-first-row semantic is represented when explicitly available.
- [ ] Unsupported shapes/text boxes, SmartArt, charts, equations, arbitrary fields, complex floating anchors, footnotes/endnotes, comments/revisions, advanced numbering fidelity, and custom XML layout behavior are not silently dropped. Warnings and/or `ImportedUnsupportedBlock` identify unsupported constructs.

### Final Word composition and writer regressions

- [ ] Final front-matter composition still uses the reviewed altChunk path unless an explicit integration decision approves a replacement.
- [ ] Reject raw `Body.Append(...)` of imported source document body children as a substitute for altChunk composition.
- [ ] Generated Word export still consumes `ReportContentDocument`; it does not consume `DocumentLayoutResult` coordinates/pages as an absolute-position DOCX model.
- [ ] Heading paragraphs use keep-with-next semantics where supported so Word and preview remain semantically aligned.
- [ ] Repeated table header rows are marked with the correct WordprocessingML table-row repeat semantic.
- [ ] Deterministic table width/column behavior does not corrupt editable Word table structure.
- [ ] Page layout/orientation values are written correctly for supported generated report semantics.
- [ ] `Styles.Save(stylesPart)` remains explicit where required by the current `autoSave: false` writer path.
- [ ] Header and Footer roots are explicitly saved to their parts where required.
- [ ] Main document save behavior remains explicit and existing Word writer regression tests are preserved.

## CROSS-BRANCH REVIEW — Integration Gate

### Diff, ownership, and frozen contract

- [ ] Review Team A/B/C/D diffs independently against the same contract baseline before merging; no branch is reviewed only against another branch tip.
- [ ] Team D changes stay under `tests/KKL.WordStudio.Architecture.Tests/**`, `docs/sprint14-team-d-qa-report.md`, and `docs/sprint14-integration-checklist.md`, plus `CONTRACT_CHANGE_REQUEST-D.md` only if actually required.
- [ ] No team edited shared Application Layout contracts, ImportedDocuments contracts, PreviewSnapshot shape, or solution/bootstrap structure unless a specific contract change request was approved during integration.
- [ ] Search the full integrated tree for duplicate `IDocumentLayoutEngine`, `DocumentLayoutResult`, page-layout result, imported-preview contract, or UI-only pagination result models.
- [ ] Review every `CONTRACT_CHANGE_REQUEST-<TEAM>.md`; approve/reject explicitly and apply only the smallest compatible contract change centrally.

### Dependency and DI gate

- [ ] Domain references Shared only.
- [ ] Application does not reference Engine, Infrastructure, Rendering, or UI.
- [ ] Engine references only Application/Shared and has no OpenXML package.
- [ ] Infrastructure has no UI reference; OpenXML remains Infrastructure-owned.
- [ ] Rendering remains Domain/Shared interaction-only and owns no pagination/document execution.
- [ ] No production project references `KKL.WordStudio.Architecture.Tests`.
- [ ] DI contains one effective `IDocumentLayoutEngine` registration for the integrated engine behavior and one effective `IImportedDocumentPreviewProvider` registration for the real Infrastructure provider.
- [ ] Bootstrap fallback registrations are removed/replaced deliberately; service registration order does not accidentally leave duplicate competing implementations.

### Semantic orchestration gate

- [ ] `PreviewRenderer` orchestrates `IReportContentBuilder.BuildAsync(project, report)` plus `IImportedDocumentPreviewProvider.ReadAsync(project)`, creates `DocumentLayoutRequest`, calls `IDocumentLayoutEngine.LayoutAsync`, and stores the result in `PreviewSnapshot.Layout`.
- [ ] UI renders the Engine result from `PreviewSnapshot.Layout.Pages`.
- [ ] Word export still consumes `ReportContentDocument` and remains semantically tied to `IReportContentBuilder` for generated KKL content.
- [ ] Preview and Word do not independently re-derive heading classification, bound rows, multi-source composition, or per-source field normalization.
- [ ] Real report ordering remains `Section.Root.Children` through `IReportStructureService`; there is no `ParentId` on `ReportElement` and no duplicate persisted/preview ordering model.
- [ ] `TableElement.Sources` ordered multi-source composition, legacy `Binding` fallback, `TableColumn.Id`, `Header`/`SourceField` separation, source normalization, captions, worksheet mappings, working-data precedence, pinned/auto DataRange behavior, and narrow workspace events remain intact.

### Regression/test inventory gate

- [ ] Run `KKL.WordStudio.Architecture.Tests`; frozen contract, project references, Engine prohibitions, source mutation guards, historical markers, and baseline manifest tests pass.
- [ ] Baseline manifest reports **165 Sprint 14 contract-baseline test methods across 31 baseline test files**.
- [ ] Baseline manifest comparison reports 0 removed baseline test files and 0 removed/renamed baseline test methods.
- [ ] Inventory reports total test methods and skipped tests; every skip is reviewed and explained. No new skip is accepted merely to make integration green.
- [ ] No existing test project/file was deleted or weakened to accommodate Team A/B/C behavior.
- [ ] Run all Engine, UI-relevant compile paths, Infrastructure, Application, Domain, and full solution tests.

### Windows/.NET 8 command gate

Run from the integrated solution root on the Windows/.NET 8 environment that is the final WPF/runtime truth:

```powershell
dotnet restore
dotnet build
dotnet test
```

Record exact command output and totals. `NETSDK1057` preview-SDK warnings alone are not compile failures; actual compiler/XAML/test errors must be investigated by file and diagnostic code. Do not claim success from static review or from a non-Windows environment when the WPF build has not run.

## MANUAL WINDOWS SMOKE TEST

Perform after restore/build/test is green:

1. Open a multi-row Excel workbook in KKL Word Studio.
2. Use **Word'e Aktar**.
3. Add headings and a long table to the report.
4. Verify the Preview shows **2 or more physical A4 pages** with page gaps and correct page geometry.
5. Verify the table continues across pages by rows and the table header repeats on continuation fragments.
6. Click a continuation fragment; verify the **same table** becomes selected in Contents and Properties.
7. Drag the table in Preview; verify Contents order changes through the real report structure and refreshes consistently.
8. Delete a heading/table from Preview; verify the real report mutates, Contents/Properties update, and project data/WorkingData are not deleted.
9. Add a DOCX front matter containing supported text, a table, an image, and an explicit page break.
10. Verify front-matter pages appear before generated report pages; imported blocks are non-editable and unsupported constructs show honest warnings/placeholders.
11. Export Word and open the generated DOCX in Microsoft Word.
12. Compare supported semantics and page order honestly: front matter order, generated heading/table content, repeated table-header/keep-next semantics, layout/orientation, header/footer, and page-number behavior. Do **not** require or claim Microsoft Word pixel-identical rendering.

## ACCEPTANCE RECORD

Before final Sprint 14 packaging, record:

- Team A diff/ownership: PASS / FAIL
- Team B diff/ownership: PASS / FAIL
- Team C diff/ownership: PASS / FAIL
- Team D diff/ownership: PASS / FAIL
- Contract drift/duplicate contract scan: PASS / FAIL
- Architecture.Tests: total / passed / failed / skipped
- Baseline manifest: baseline count / removed files / removed methods
- Full solution `dotnet restore`: PASS / FAIL
- Full solution `dotnet build`: PASS / FAIL
- Full solution `dotnet test`: total / passed / failed / skipped
- Manual Windows smoke: PASS / FAIL, with observed warnings and unsupported DOCX constructs listed
- Approved contract change requests: none / list exact files and decision
