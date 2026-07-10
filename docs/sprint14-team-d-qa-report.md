# KKL Word Studio — Sprint 14 Team D QA / Architecture Report

## Role and scope

Team D operated as the QA, architecture-compliance, and integration-validation branch for Sprint 14. No product feature was implemented and no production source file was modified. Changes are limited to `tests/KKL.WordStudio.Architecture.Tests/**` and the two Team D documentation deliverables.

Baseline used: `KKL.WordStudio-Sprint14-Contract-Baseline-Stabilized.zip` only.  
Shared contract status: Sprint 14 v1.0 authoritative/frozen.

No architecture flaw requiring `docs/CONTRACT_CHANGE_REQUEST-D.md` was found in the frozen contract, so no Team D contract change request was created.

## Baseline regression inventory

The pristine contract baseline was scanned before Team D test additions.

- Baseline `[Fact]`/`[Theory]` test method count: **165**
- Baseline test source files containing tests: **31**
- Baseline skipped tests detected: **0**
- Checked-in manifest: `tests/KKL.WordStudio.Architecture.Tests/TestData/sprint14-contract-baseline-tests.json`
- Manifest granularity: relative test file path + test method names

`TestInventory` and `BaselineRegressionInventoryTests` provide an integration-reusable inventory/comparison path. The guard reports total current test methods, skipped tests, removed baseline test files, and removed/renamed baseline test methods. It rejects any missing baseline file/method and rejects a current test-method count below the manifest baseline.

Static inventory after Team D additions found:

- Current discovered test methods: **183**
- Current discovered test files containing tests: **36**
- Architecture.Tests discovered test methods: **19** (the original baseline architecture test plus 18 Team D additions)
- Skipped tests: **0**
- Removed baseline test files versus manifest: **0**
- Removed/renamed baseline test methods versus manifest: **0**

These are source-inventory results, not `dotnet test` pass totals.

## Architecture guard result

Added solution-root/project-XML guards validate the Sprint 14 dependency boundaries with useful offender messages:

- Domain project references are limited to Shared.
- Application is prohibited from referencing Engine, Infrastructure, Rendering, or UI.
- Engine project references are limited to Application and Shared.
- Infrastructure is prohibited from referencing UI.
- Rendering project references are limited to Domain and Shared.
- Shared has no project references.
- UI is recognized as the composition root.
- No production project may reference `KKL.WordStudio.Architecture.Tests`.
- Engine may not reference an OpenXML package.

The original `EngineProjectDependencyTests.EngineProject_DoesNotReferenceUIInfrastructureOrRendering` baseline test remains present; it was not deleted or renamed.

A separate Engine source prohibition suite rejects stable boundary violations:

- `System.Windows`
- `DocumentFormat.OpenXml`
- `WordprocessingDocument`
- `IDataProvider` / `ExcelDataProvider`
- `.Binding` / `.Sources` semantic access
- `SpreadsheetDocument.Open(...)`

Global guards reject Microsoft Office Interop/COM identifiers and WebView controls/packages. The Sprint 14 “no new PDF implementation work” boundary is guarded by rejecting PDF implementation packages and requiring the existing `PdfExporter` to remain the explicit not-implemented failure stub.

A Team D static parity scan of the current branch found **0 architecture/prohibition issues**. This static result does not replace executable .NET tests.

## Frozen contract guard result

Reflection/compile-time contract tests cover the frozen public Application contract without requiring implementation class names.

### Layout contract

Guarded concepts and shapes include:

- `IDocumentLayoutEngine.LayoutAsync(DocumentLayoutRequest, CancellationToken)` returning `Task<DocumentLayoutResult>`
- `DocumentLayoutRequest`
- `DocumentLayoutResult`
- `DocumentPageLayout`
- `PositionedPageBlock`
- `DocumentPageOrigin`
- `DocumentPageRegion`
- `PageBlockKind`
- abstract `PageBlockPayload`
- `TextPageBlockPayload`
- `TablePageBlockPayload`
- `TocPageBlockPayload`
- `ImagePageBlockPayload`
- `PageNumberPageBlockPayload`
- `UnsupportedPageBlockPayload`
- `TextRunLayout`
- `LaidOutTocEntry`
- frozen enum member names
- required member presence and property types
- concrete payload inheritance/sealed shape

### ImportedDocuments contract

Guarded concepts and shapes include:

- `IImportedDocumentPreviewProvider.ReadAsync(Project, CancellationToken)` returning `Task<ImportedDocumentPreviewResult>`
- `ImportedDocumentPreviewResult`
- `ImportedDocumentPreviewDocument`
- `ImportedDocumentSection`
- abstract `ImportedDocumentBlock`
- `ImportedParagraphBlock`
- `ImportedTableBlock`
- `ImportedImageBlock`
- `ImportedExplicitPageBreakBlock`
- `ImportedUnsupportedBlock`
- `ImportedTextRun`
- required member presence/property types and concrete block inheritance

### PreviewSnapshot compatibility contract

The guard requires `PreviewSnapshot.Layout` and preserves the Sprint 14 compatibility properties:

- `HeaderBlocks`
- `BodyBlocks`
- `FooterBlocks`
- `TableOfContents`
- `PageLayout`

No contract drift was identified in the stabilized baseline during source review. No `CONTRACT_CHANGE_REQUEST-D.md` was needed.

## Historical stabilization marker guards

Added narrow reflection/source guards for critical fixes already present in the contract baseline:

- UI csproj retains `Serilog.Extensions.Hosting`.
- `KwsProjectRepository` retains `PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate`.
- `DataRange.RangeReference` remains `[JsonIgnore]`.
- `Binding.WorksheetName` exists.
- `TableColumn.Header` and `TableColumn.SourceField` remain distinct properties.
- `TableElement.Sources` exists.
- `Worksheet.WorkingData` exists.
- `Worksheet.ColumnMappings` exists.
- Excel OpenXML source paths are rejected if `SpreadsheetDocument.Open(..., true)` appears.
- The existing front-matter reader is required to open its DOCX source read-only with `WordprocessingDocument.Open(filePath, false)`.
- Word styles, header, and footer writers retain explicit `Save(part)` calls.
- Preview rebuild remains subscribed to `ReportContentChanged`; broad `WorkspaceChanged` is rejected as a `RefreshAsync`/`RenderAsync` rebuild subscription.
- `ReportStructureService` remains based on `section.Root.Children` and `ReportElement` must not gain `ParentId`.

The guards deliberately use reflection for model/property shape and narrow source scans for stable implementation markers rather than whitespace/comment formatting tests.

## Integration checklist result

Created `docs/sprint14-integration-checklist.md` with four integration review gates:

1. Team A Engine/pagination review
2. Team B WPF paginated preview/interaction review
3. Team C imported DOCX/Word semantics review
4. Cross-branch integration gate

The checklist explicitly covers ownership, frozen-contract drift, duplicate semantic/page models, project references, Engine progress/infinite-loop risk, cancellation checks, sequential/absolute page numbers, header/footer repeat, table fragment identity and row indices, TOC convergence bounds, front-matter order, UI-only pagination rejection, same-ElementId selection across fragments, delete/drag delegation to `IReportStructureService`, XAML command/event wiring, local-value/style-trigger precedence, read-only DOCX access, image relationship resolution, explicit-only page-break extraction, unsupported construct warnings, altChunk preservation, raw Body append rejection, Word repeat-header/keep-next semantics, explicit part saves, DI uniqueness, PreviewRenderer orchestration, baseline inventory preservation, Windows restore/build/test, and manual WPF smoke verification.

The manual smoke section contains the required 12-step Excel → Word'e Aktar → long multi-page report → continuation selection/drag/delete → DOCX front matter → Word export flow and requires an honest supported-semantics/page-order comparison rather than a pixel-identical Word claim.

## Verification

Requested commands:

```text
dotnet restore
dotnet build
dotnet test
```

Actual local command result:

```text
bash: line 1: dotnet: command not found
```

Therefore:

- Architecture.Tests discovered methods: **19**
- Architecture.Tests pass/fail: **NOT EXECUTED — .NET SDK unavailable in this environment**
- Full solution restore: **NOT EXECUTED — .NET SDK unavailable**
- Full solution build: **NOT EXECUTED — .NET SDK unavailable**
- Full solution tests: **NOT EXECUTED — .NET SDK unavailable**
- Baseline manifest count: **165**
- Windows/.NET 8 final verification: **PENDING**

No build or test success is claimed. The integration checklist requires exact Windows `dotnet restore`, `dotnet build`, and `dotnet test` output before Sprint 14 integration acceptance.

## Deliverables

- `tests/KKL.WordStudio.Architecture.Tests/**` — architecture, contract, prohibition, regression-inventory, and stabilization guards
- `docs/sprint14-team-d-qa-report.md`
- `docs/sprint14-integration-checklist.md`
- `KKL.WordStudio-Sprint14-TeamD-QA.zip`

No `docs/CONTRACT_CHANGE_REQUEST-D.md` was created because no frozen-contract architecture flaw was found.
