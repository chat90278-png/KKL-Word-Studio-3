# Sprint 14 Contract Bootstrap Report

## Role Completed

Contract Architect / Parallel Integration Boundary Owner bootstrap completed from the uploaded `KKL.WordStudio-Sprint13.zip` as the only code baseline and the authoritative `SPRINT14-SHARED-CONTRACT.txt`.

Input discrepancy: the bootstrap prompt names `KKL.WordStudio-Sprint13-Stabilized.zip`, while the supplied archive is named `KKL.WordStudio-Sprint13.zip`. No older workspace was used. This report does not claim the uploaded archive is the separately named "Stabilized" artifact.

## Contract Result

Added the frozen Application contracts under:

- `src/KKL.WordStudio.Application/Layout`
- `src/KKL.WordStudio.Application/ImportedDocuments`

The public contract concepts and required property names from the shared contract are present. No second page/layout result model was introduced.

`PreviewSnapshot` now includes:

```csharp
public required DocumentLayoutResult Layout { get; init; }
```

Existing compatibility properties remain unchanged.

## Project / Bootstrap Result

Added:

- `src/KKL.WordStudio.Engine/KKL.WordStudio.Engine.csproj`
- `tests/KKL.WordStudio.Engine.Tests/KKL.WordStudio.Engine.Tests.csproj`
- `tests/KKL.WordStudio.Architecture.Tests/KKL.WordStudio.Architecture.Tests.csproj`

All three projects are included in `KKL.WordStudio.sln`.

Engine targets `net8.0` and directly references only:

- `KKL.WordStudio.Application`
- `KKL.WordStudio.Shared`

Engine has no UI, Infrastructure, Rendering, or OpenXML reference.

Added `AddWordStudioEngine()` and registered `IDocumentLayoutEngine` to `FallbackDocumentLayoutEngine`.

The fallback Engine:

- creates one generated report page only;
- uses millimeter coordinates;
- maps generated semantic nodes to contract `PositionedPageBlock` instances;
- preserves generated `ElementId`;
- marks generated real report blocks editable;
- keeps TOC/page-number derived blocks non-editable;
- returns the exact warning `Fallback layout active; true pagination is not enabled.`;
- does not implement true pagination.

Added `FallbackImportedDocumentPreviewProvider` in Infrastructure and registered it through the existing Infrastructure DI extension.

Provider behavior:

- no front matter: `Document = null`, `IsMissing = false`;
- unavailable front matter: `Document = null`, `IsMissing = true`, Turkish missing/unavailable status;
- readable front matter: `Document = null`, honest Turkish status that native DOCX preview extraction is not active;
- no DOCX parsing is performed.

`PreviewRenderer` now injects:

- `IReportContentBuilder`
- `IDocumentLayoutEngine`
- `IImportedDocumentPreviewProvider`

Orchestration is now:

`BuildAsync -> ReadAsync -> DocumentLayoutRequest -> LayoutAsync -> PreviewSnapshot.Layout`

Compatibility block properties continue to be populated from `ReportContentDocument`.

UI composition root now calls `services.AddWordStudioEngine()` and the UI project references Engine.

## Focused Bootstrap Tests Added

Engine tests:

- `FallbackLayout_ReturnsContractCompliantGeneratedPage`
- `GeneratedFallbackBlocks_PreserveElementIdAndEditableIdentity`
- `PreviewSnapshot_KeepsCompatibilityPropertiesAndLayout`

Architecture bootstrap test:

- `EngineProject_DoesNotReferenceUIInfrastructureOrRendering`

No true-pagination tests were added.

## ADR

Added:

`docs/adr/0013-layout-engine-and-parallel-sprint14-contract.md`

The ADR records:

- why the Engine layer is now justified;
- Engine consumption of Application `ReportContentDocument`;
- Rendering remaining interaction-only;
- UI consumption of `DocumentLayoutResult`;
- WordExporter continuing to consume `ReportContentDocument`, not layout coordinates;
- imported DOCX preview as supported-semantic extraction rather than Word pixel rendering.

## Scope Check

Baseline comparison:

- 13 added files
- 6 changed files
- 0 deleted files

No real multi-page pagination, table splitting, WPF page-stack redesign, preview delete/drag, native DOCX semantic extraction, Word writer polish, PDF, COM/Interop, or WebView implementation was added.

## Verification

Required commands were invoked in this environment.

### `dotnet restore`

Actual result:

```text
bash: line 4: dotnet: command not found
EXIT_CODE=127
```

### `dotnet build`

Actual result:

```text
bash: line 4: dotnet: command not found
EXIT_CODE=127
```

### `dotnet test`

Actual result:

```text
bash: line 4: dotnet: command not found
EXIT_CODE=127
```

Therefore restore/build/test success is **not claimed**.

Static/source verification performed because the .NET SDK is unavailable:

- all 12 `.csproj` files parsed as XML;
- every `ProjectReference` target exists;
- no project-reference cycle detected;
- Engine direct references contain Application and Shared only;
- Engine has no UI/Infrastructure/Rendering/OpenXML dependency;
- Application and Domain do not reference Engine;
- solution entries/configuration mappings exist for Engine and both new test projects;
- all frozen contract type names are present;
- required frozen contract property names were checked;
- Engine DI registration is present;
- Infrastructure imported-provider registration is present;
- UI calls `AddWordStudioEngine()`;
- UI references Engine;
- PreviewRenderer creates `DocumentLayoutRequest` and stores `Layout`;
- exact fallback warning is present;
- all `new PreviewSnapshot` initializers in the workspace set `Layout`.

Static contract/bootstrap checks: **PASS**.

## Remaining Blocker

Windows/.NET 8 verification is pending and remains mandatory before Team A/B/C/D start in parallel.

Additionally, the supplied baseline archive filename differs from the bootstrap prompt's `Sprint13-Stabilized` filename. The uploaded archive was used as the sole baseline, but its stabilized-label provenance could not be independently established in this environment.
