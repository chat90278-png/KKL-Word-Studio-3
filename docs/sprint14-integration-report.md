# KKL Word Studio — Sprint 14 Reviewed Integration Report

## 1. Branch diff / ownership decision

Integration baseline: `KKL.WordStudio-Sprint14-Contract-Baseline-Stabilized.zip` only.

The four supplied branch ZIPs were independently diffed against the same stabilized contract baseline before integration. The observed diffs matched the reviewed integration decision:

- **Team A — ACCEPT**: 10 changed/added files, limited to `src/KKL.WordStudio.Engine/**`, `tests/KKL.WordStudio.Engine.Tests/**`, and `docs/sprint14-team-a-layout-report.md`.
- **Team B — ACCEPT, then central fixes**: 8 changed/added files, limited to the frozen Preview UI ownership paths and `docs/sprint14-team-b-wpf-report.md`.
- **Team C — ACCEPT**: 9 changed/added/deleted files, limited to Infrastructure Word/OpenXML/DI, Sprint 14 Infrastructure tests, and `docs/sprint14-team-c-docx-report.md`.
- **Team D — ACCEPT**: 13 changed/added files, limited to `tests/KKL.WordStudio.Architecture.Tests/**`, `docs/sprint14-team-d-qa-report.md`, and `docs/sprint14-integration-checklist.md`.

No A/B/C production file overlap exists. No branch was used as a winning ZIP and no branch was overlaid wholesale onto another branch. Reviewed files were reapplied to a fresh stabilized baseline.

No `docs/CONTRACT_CHANGE_REQUEST-A/B/C/D.md` file exists. No contract change was approved or applied.

Frozen contract verification:

- `src/KKL.WordStudio.Application/Layout/**`: baseline hash set unchanged.
- `src/KKL.WordStudio.Application/ImportedDocuments/**`: baseline hash set unchanged.
- `src/KKL.WordStudio.Application/Preview/PreviewSnapshot.cs`: baseline hash unchanged.
- duplicate declaration scan: exactly one `IDocumentLayoutEngine`, `DocumentLayoutRequest`, `DocumentLayoutResult`, `IImportedDocumentPreviewProvider`, and `ImportedDocumentPreviewResult` declaration.

Accepted branch files that were not central-fix targets match their reviewed branch bytes exactly.

## 2. Branch files applied

### Team A — Engine / pagination

Applied:

- `src/KKL.WordStudio.Engine/Layout/DeterministicDocumentLayoutEngine.cs`
- `src/KKL.WordStudio.Engine/Layout/DeterministicTablePaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/DeterministicTextMeasurement.cs`
- `src/KKL.WordStudio.Engine/Layout/FrontMatterPaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/GeneratedDocumentPaginator.cs`
- `src/KKL.WordStudio.Engine/Layout/LayoutPageFlow.cs`
- `src/KKL.WordStudio.Engine/Layout/TextRunLayoutFactory.cs`
- `tests/KKL.WordStudio.Engine.Tests/DeterministicDocumentLayoutEngineTests.cs`
- `docs/sprint14-team-a-layout-report.md`

Changed:

- `src/KKL.WordStudio.Engine/DependencyInjection/EngineServiceCollectionExtensions.cs`

Preserved Team A semantics include deterministic millimeter layout, imported pages before generated pages, absolute page numbering, generated page layout/margins, repeated header/footer, heading keep-with-next, row-atomic table fragmentation, continuation headers, first-fragment-only captions, `StartRowIndex`, `FragmentIndex`, `IsContinuation`, same `ElementId` across fragments, source-error visibility, oversized-row progress/warning, cancellation checks, and a bounded `MaximumTocPasses = 5` convergence loop.

### Team B — WPF physical document surface

Applied:

- `src/KKL.WordStudio.UI/Preview/PreviewTextBlockControl.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewInteractionHelpers.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewPageViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewViewModel.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml.cs`
- `docs/sprint14-team-b-wpf-report.md`

Then the two reviewed cross-branch Preview fixes described in sections 3 and 4 were applied centrally.

The UI remains a projection of `PreviewSnapshot.Layout.Pages`; no page/block collection reorder or UI-owned pagination path was introduced.

### Team C — Infrastructure Word/OpenXML

Applied:

- `src/KKL.WordStudio.Infrastructure/Word/OpenXmlImportedDocumentPreviewProvider.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint14ImportedDocumentPreviewTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint14WordFidelityTests.cs`
- `docs/sprint14-team-c-docx-report.md`

Changed:

- `src/KKL.WordStudio.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordPageLayoutWriter.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordParagraphWriter.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordTableWriter.cs`

Deleted:

- `src/KKL.WordStudio.Infrastructure/Word/FallbackImportedDocumentPreviewProvider.cs`

The read-only `WordprocessingDocument.Open(path, false)` extraction path is present. Final front-matter composition still contains `AlternativeFormatImportPart` plus `w:altChunk`; `WordExporter` does not consume `DocumentLayoutResult`.

### Team D — QA / architecture

Applied the reviewed Team D tree under:

- `tests/KKL.WordStudio.Architecture.Tests/**`
- `docs/sprint14-team-d-qa-report.md`
- `docs/sprint14-integration-checklist.md`

Then three integration source-contract guards were added in:

- `tests/KKL.WordStudio.Architecture.Tests/Sprint14IntegrationGuardTests.cs`

## 3. Split-text edit data-loss fix

Team A can split one semantic `TextContentNode` into multiple visible `PreviewTextPageBlockViewModel` fragments with the same `ElementId` and ordered `FragmentIndex` values. Team B's original edit flow initialized and compared `EditText` against one fragment's `PlainText`. A commit could therefore replace the real `TextElement` with only a page fragment.

Central integration fix in `PreviewViewModel`:

```text
GetSemanticTextForElement(Guid elementId)
  -> visible PreviewTextPageBlockViewModel blocks with ElementId
  -> GroupBy(FragmentIndex)
  -> one representative per FragmentIndex
  -> OrderBy(FragmentIndex)
  -> concatenate PlainText
```

This intentionally deduplicates repeated generated header/footer blocks, which may appear on multiple pages with the same `ElementId` and `FragmentIndex = 0`.

`BeginTextEdit` now initializes `EditText` from the reconstructed complete semantic text.

`CommitTextEdit` now reconstructs the same complete semantic text and performs an ordinal comparison against it before calling `IReportEditingService.CommitHeadingText` once for the real element.

The direct `block.EditText == block.PlainText` fragment-only comparison is absent.

Preserved behavior:

- shared selection;
- double-click inline editing;
- Escape cancel;
- Enter/focus-loss commit;
- paragraph/header/footer/heading semantic edit path;
- front matter remains non-editable;
- `Workspace.NotifyReportContentChanged()` remains the refresh path.

No full semantic text was added to `PositionedPageBlock` or another layout/content contract.

Integration guard added:

- `PreviewTextEditing_ReconstructsSemanticTextAcrossFragments`

## 4. Body-only structure gesture fix

`IReportStructureService` owns body report structure and excludes `SectionKind.PageHeader` / `PageFooter`. Team B originally reused `CanInteract` for selection/edit and delete/drag/drop, which allowed generated header/footer blocks to appear structurally mutable even though the authoritative structure service does not own those sections.

Central integration fix in `PreviewPageBlockViewModel`:

```text
CanInteract
  = IsEditableReportElement && ElementId.HasValue

CanStructureInteract
  = CanInteract && Region == DocumentPageRegion.Body
```

`CanInteract` remains available for selection and semantic inline editing.

`CanStructureInteract` is now required by:

- `DeleteSelectedPreviewElement` lookup;
- `DeletePreviewElement`;
- neighbor selection order after deletion;
- right-click **Sil** context-menu creation;
- drag-source assignment to `_dragSourceElementId`;
- `TryResolveReportDrop` target resolution.

Resulting semantics:

- body report elements: select/edit and structure delete/drag/drop as supported;
- header/footer: select/edit where existing semantic behavior supports it, but no structure delete, drag source, or drop target;
- front matter: remains non-interactive as a `ReportElement`;
- page number / TOC derived blocks: remain non-interactive.

No layout `Pages`/`Blocks` collection mutation was added. Actual delete/move still delegates to `IReportStructureService`.

Integration guard added:

- `PreviewStructureGestures_AreBodyRegionOnly`

## 5. Imported preview status propagation fix

Team C correctly returns `ImportedDocumentPreviewResult.Document`, `IsMissing`, and `StatusMessage`, including malformed/unreadable DOCX status. The bootstrap `PreviewRenderer` originally passed only `frontMatter.Document` into Engine and discarded `StatusMessage`.

Central integration fix in `src/KKL.WordStudio.UI/Preview/PreviewRenderer.cs`:

1. Build generated semantic content.
2. Read imported document preview result.
3. Run `IDocumentLayoutEngine.LayoutAsync` with `frontMatter.Document`.
4. When `frontMatter.StatusMessage` is non-empty:
   - start from `layout.Warnings`;
   - append the imported preview status;
   - deduplicate identical warnings using ordinal equality;
   - create a new `DocumentLayoutResult`;
   - preserve `Pages = layout.Pages` exactly;
   - assign the merged warnings.
5. Store the resulting layout in `PreviewSnapshot.Layout`.

Team B already surfaces `Layout.Warnings` through `SurfaceStatusText`, so an existing but unreadable/corrupt DOCX can no longer silently disappear from preview status merely because file availability is true.

No fake front-matter page was created and no frozen contract changed.

Integration guard added:

- `PreviewRenderer_SurfacesImportedPreviewStatus`

## 6. Engine / Preview / DOCX integrated flow

Integrated Preview flow:

```text
Project + Report
  -> IReportContentBuilder.BuildAsync
  -> ReportContentDocument

Project.FrontMatter
  -> IImportedDocumentPreviewProvider.ReadAsync
  -> ImportedDocumentPreviewResult
     - Document
     - IsMissing
     - StatusMessage

ReportContentDocument + ImportedDocumentPreviewResult.Document
  -> DocumentLayoutRequest
  -> IDocumentLayoutEngine.LayoutAsync
  -> DocumentLayoutResult

Imported StatusMessage, when non-empty
  -> merge into DocumentLayoutResult.Warnings
  -> preserve DocumentLayoutResult.Pages

PreviewSnapshot.Layout
  -> Layout.Pages
  -> PreviewPageProjection
  -> physical WPF page stack
```

Integrated Word flow remains separate from physical preview coordinates:

```text
IReportContentBuilder
  -> ReportContentDocument
  -> WordExporter / Word writers
  -> DOCX
```

`WordExporter` does not consume `DocumentLayoutResult`.

Final front-matter Word composition remains package-preserving:

```text
AlternativeFormatImportPart
  + w:altChunk
  + explicit page break
  + generated KKL report
```

No raw imported `Body` append path was introduced.

## 7. DI registrations

Source-level DI scan result:

```text
IDocumentLayoutEngine
  -> DeterministicDocumentLayoutEngine
  registrations found: 1

IImportedDocumentPreviewProvider
  -> OpenXmlImportedDocumentPreviewProvider
  registrations found: 1
```

`FallbackDocumentLayoutEngine` remains as an unregistered compatibility class.

`FallbackImportedDocumentPreviewProvider.cs` is deleted and was not restored.

No duplicate competing layout/imported-preview contract declarations were found.

## 8. Test inventory

Expected Sprint 14 source inventory:

```text
Contract baseline     165
Team A                +18
Team B                  +0 xUnit methods
Team C                +18
Team D                +18
Integration guards     +3
--------------------------
Expected               222
```

Actual source inventory using the same `[Fact]` / `[Theory]` method scan model as Team D:

```text
Total test methods:                       222
Skipped tests:                              0
Baseline manifest method count:           165
Baseline manifest test files:              31
Removed baseline test files:                0
Removed/renamed baseline test methods:      0
```

Per-project source method inventory:

```text
KKL.WordStudio.Application.Tests       100
KKL.WordStudio.Architecture.Tests       22
KKL.WordStudio.Domain.Tests             16
KKL.WordStudio.Engine.Tests             21
KKL.WordStudio.Infrastructure.Tests     63
Total                                  222
```

The three added integration guard method names are present exactly as required.

Additional mechanical validation completed:

- 64/64 integrated static checks passed (`docs/verification/static-integration-check.txt`);
- 25/25 Team D source-level architecture/prohibition/stabilization gates passed (`docs/verification/team-d-source-gates.txt`);
- Preview XAML parses as XML;
- all referenced Preview XAML handlers resolve to code-behind method names;
- all `.csproj` files parse as XML;
- changed C# files pass lexical delimiter-balance scanning;
- accepted branch ownership diffs are clean;
- frozen contract hashes are unchanged.

These source/static checks do not replace executable xUnit results.

## 9. Restore / build / test actual output

Commands were run from the integrated solution root.

### `dotnet restore`

```text
exit 127
bash: line 5: dotnet: command not found
```

### `dotnet build`

```text
exit 127
bash: line 5: dotnet: command not found
```

### `dotnet test`

```text
exit 127
bash: line 5: dotnet: command not found
```

The execution environment has no system `.NET` CLI.

A best-effort attempt was made to obtain the official .NET 8 Linux x64 SDK locally. The shell could not resolve the Microsoft binary host:

```text
curl: (6) Could not resolve host: builds.dotnet.microsoft.com
```

Therefore:

- `dotnet restore`: **NOT GREEN / NOT EXECUTABLE IN THIS ENVIRONMENT**;
- `dotnet build`: **NOT GREEN / NOT EXECUTABLE IN THIS ENVIRONMENT**;
- `dotnet test`: **NOT GREEN / NOT EXECUTABLE IN THIS ENVIRONMENT**;
- Architecture.Tests executable pass/fail: **NOT AVAILABLE**;
- full solution executable test total/pass/fail: **NOT AVAILABLE**.

No green restore/build/test claim is made. Windows/.NET 8 WPF remains the final build/runtime truth.

Raw command captures are included under `docs/verification/`.

## 10. Manual smoke status

Status: **NOT RUN**.

The reviewed integration prompt requires the manual Windows smoke only after restore/build/test is green. That prerequisite could not be established because the current environment has no executable .NET SDK and is not a Windows WPF/Microsoft Word runtime.

The manual smoke checklist remains in `docs/sprint14-integration-checklist.md` and must be executed on Windows/.NET 8. In particular, the integrated code requires runtime confirmation for:

- 2+ physical page rendering and page gaps;
- row continuation and repeated table header;
- same `ElementId` selection from a continuation fragment;
- long split text edit preserving the complete real text;
- header/footer semantic editing without structure **Sil**/drag affordances;
- body drag/delete routing through `IReportStructureService`;
- supported DOCX front-matter text/run/table/image/explicit-break projection;
- corrupt/unreadable DOCX warning visibility;
- Microsoft Word open/export semantics including altChunk order, keep-next, repeated table header, page orientation/layout, header/footer, PAGE, and TOC behavior.

No pixel-identical Microsoft Word rendering claim is made.

## 11. Warnings / remaining fidelity gaps

### Verification gap

The release candidate is source-integrated and statically checked, but is **not executable-green-certified** because `dotnet restore`, `dotnet build`, and `dotnet test` could not run in this environment. Windows/.NET 8 build/test and manual WPF/Word smoke remain required before release acceptance.

### Imported DOCX fidelity limits

The imported preview remains intentionally structured and bounded rather than a Microsoft Word renderer. Team C surfaces unsupported or reduced-fidelity constructs such as complex shapes/text boxes, SmartArt/diagrams, charts, equations, embedded objects/controls, advanced floating positioning, footnote/endnote body content, comments/revisions, advanced numbering fidelity, and other unsupported Word constructs through warnings/placeholders where meaningful.

Floating images are flow-projected rather than positioned with full anchor fidelity. Section inheritance and multi-column layout have documented preview limits. Fields use stored visible results when available; they are not executed as a Word field engine.

### Preview / Word pagination fidelity

Engine text measurement is a deterministic approximation for supported layout semantics. It is not a pixel-identical Microsoft Word metric engine. Word remains its own pagination engine and the export path intentionally consumes `ReportContentDocument`, not Preview layout coordinates.

### Integration acceptance record

```text
Team A diff/ownership:                  PASS (source diff review)
Team B diff/ownership:                  PASS (source diff review)
Team C diff/ownership:                  PASS (source diff review)
Team D diff/ownership:                  PASS (source diff review)
Contract drift/duplicate contract scan: PASS
Static integration checks:              64 / 64 PASS
Team D source-level gates:               25 / 25 PASS
Source test inventory:                  222 methods / 0 skipped
Baseline manifest:                      165 / 0 removed files / 0 removed methods
Architecture.Tests executable result:   NOT EXECUTED — dotnet unavailable
Full solution dotnet restore:            exit 127 — dotnet unavailable
Full solution dotnet build:              exit 127 — dotnet unavailable
Full solution dotnet test:               exit 127 — dotnet unavailable
Manual Windows smoke:                    NOT RUN — executable green gate unavailable
Approved contract change requests:       none
```
