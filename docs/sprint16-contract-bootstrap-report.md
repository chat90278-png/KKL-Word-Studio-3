# Sprint 16 Contract Bootstrap Report

## 1. Role and scope

Contract Architect / Reference Format Contract Architect bootstrap completed against the supplied `KKL.WordStudio-Sprint15-Stabilized.zip` as the only code baseline. `SPRINT16-SHARED-CONTRACT.txt` was treated as authoritative and the supplied `Sero.docx` was treated as the visual-format reference.

This bootstrap establishes only the shared project/domain, Application formatting, shared content/layout payload, builder-orchestration, compatibility-DI, contract-test, ADR, and reference-profile seams required before Team A/B/C/D work.

No real OpenXML reference analyzer, `.kws` reference-asset persistence, reference-aware production resolver, grouping Properties UI, reference-format command UI, Engine format-aware measurement/pagination, WPF format renderer, or Word format-fidelity implementation was added.

Sprint 15 serial/quantity grouping semantics were not weakened or reimplemented.

## 2. Baseline provenance discrepancy

The authoritative Sprint 16 contract describes the expected input as `KKL.WordStudio-Sprint15-Stabilized-WindowsGreen.zip` with a 308-test Windows-green baseline.

The supplied archive is named:

`KKL.WordStudio-Sprint15-Stabilized.zip`

Its source inventory does contain exactly 308 `[Fact]` / `[Theory]` methods and 0 `Skip` attributes.

However, the embedded `docs/sprint15-windows-stabilization-report.md` does **not** prove that this exact patched source was rerun green. It records a prior Windows execution with:

- restore success;
- build success with 1 warning / 0 errors;
- Application 141 passed / 2 failed;
- Engine 34 passed / 2 failed;
- all other test projects green;

then documents three narrow production/test-warning fixes and explicitly states that final Windows verification remains required and no green result is claimed for the patched ZIP until those commands run.

Therefore this supplied archive does not carry embedded evidence of the exact 308/308 Windows-green artifact named by the Sprint 16 contract. The source inventory matches 308/0-skipped, but Windows-green provenance is not established by the supplied package. This bootstrap does not repair or reinterpret unrelated Sprint 15 behavior.

## 3. Sero.docx reference profile

`Sero.docx` was inspected as OpenXML and rendered to three pages for visual review.

Reference SHA-256:

`8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`

Created:

`docs/SERO-REFERENCE-FORMAT-PROFILE.md`

The profile freezes the supported-property evidence for parallel teams, including:

- A4 portrait page geometry;
- approximately 25 mm margins;
- approximately 12.49 mm header/footer distances;
- Arial 10 pt Normal baseline;
- 12 pt bold/italic primary heading with keep-next and 4/2 pt spacing;
- 12 pt bold secondary heading and reference indent evidence;
- centered 11 pt bold identifier override support;
- caption style and real `SEQ Tablo` field evidence;
- two distinct reference table profiles;
- 100% versus 99.32% table width evidence;
- fixed layout, 0.5 pt borders, 1.235 mm left/right cell margins, 10.195 mm preferred row height, repeated header;
- exact raw unequal table-grid width weights;
- per-column header/body alignment, font, vertical alignment, and no-wrap evidence;
- observed Table 2 `w:vMerge` as visual evidence only, explicitly not as grouping-role inference.

The profile also records deterministic warning cases for mixed caption labels, direct caption-size overrides, a Table 1 ordinal bold anomaly, a Serial No paragraph-style exception, and inconsistent secondary-heading direct indent.

## 4. Domain and project-owned reference asset contract

Added:

`src/KKL.WordStudio.Domain/Projects/ReferenceFormatDocument.cs`

It contains:

- `FileName`
- `OriginalSourcePath`
- `EmbeddedAssetEntryName`
- `ResolvedFilePath [JsonIgnore]`

with the exact default embedded entry:

`resources/reference-format/reference-format.docx`

`Project` now exposes:

```csharp
public ReferenceFormatDocument? ReferenceFormat { get; set; }
```

This is distinct from `FrontMatterDocument` and is not a `ReportElement`.

`TableElement` now exposes:

```csharp
public string? ReferenceTableFormatKey { get; set; }
```

No reference DOCX table index or displayed-header persistence was added as table-format identity.

## 5. Application formatting contract

Added namespace/folder:

`KKL.WordStudio.Application.Formatting`

Frozen contract types:

- `ReferenceDocumentFormatResult`
- `DocumentFormatProfile`
- `PageFormatProfile`
- `ResolvedTextFormat`
- `TableCaptionSequenceProfile`
- `ReferenceTableFormatProfile`
- `ResolvedTableFormat`
- `ResolvedTableColumnFormat`
- `VerticalContentAlignment`
- `IReferenceDocumentFormatProvider`
- `IReportContentFormatResolver`

The provider contract is synchronous only at the already-async asset boundary:

```csharp
Task<ReferenceDocumentFormatResult> ReadAsync(
    Project project,
    CancellationToken cancellationToken = default);
```

The resolver contract exposes the three frozen operations:

- `ResolveText`
- `ResolveTable`
- `ResolvePageLayout`

Added compatibility/bootstrap services:

- `NoReferenceDocumentFormatProvider`
- `DefaultReportContentFormatResolver`
- `DefaultFormatProfiles`

The default provider returns `Profile=null`, `IsMissing=false`, and no status.

The bootstrap resolver deliberately does **not** interpret a reference DOCX profile. It preserves current authored KKL text/page semantics and provides deterministic generic table defaults. Team B owns the real reference-aware resolver.

## 6. Shared content and layout contract extensions

`TextContentNode` retains:

- `Text`
- `Bold`
- `FontSize`

and now adds default-compatible:

- `ResolvedTextFormat Format`

`TableContentNode` retains all Sprint 15 rows/spans/groups/warnings and adds:

- `ResolvedTableFormat Format`
- `TableCaptionSequenceProfile? CaptionSequence`

`ReportContentDocument` adds default-empty:

- `IReadOnlyList<string> FormatWarnings`

`PageLayout` adds default-compatible:

- `HeaderDistanceMillimeters`
- `FooterDistanceMillimeters`

Both default to `12.7d` for old direct initializers.

`TextPageBlockPayload` adds default-compatible:

- `ResolvedTextFormat Format`

`TablePageBlockPayload` adds default-compatible:

- `ResolvedTableFormat Format`

The old compatibility properties and Sprint 15 `CellSpans` contract remain in place. No second content tree, second table payload, WPF type, or OpenXML type was added to Application.

## 7. ReportContentBuilder orchestration and DI

`ReportContentBuilder` now consumes:

- `IReferenceDocumentFormatProvider`
- `IReportContentFormatResolver`

while retaining both existing source-compatible construction paths:

```csharp
ReportContentBuilder(IDataProviderRegistry)
ReportContentBuilder(IDataProviderRegistry, ITableContentRowComposer)
```

Both compatibility constructors use:

- `NoReferenceDocumentFormatProvider`
- `DefaultReportContentFormatResolver`

A four-argument DI-compatible constructor accepts the production composer/provider/resolver seams.

Builder flow is now:

```text
Project / Report
↓
IReferenceDocumentFormatProvider.ReadAsync
↓
DocumentFormatProfile?
↓
existing semantic node build
↓
ITableContentRowComposer for successful tables
↓
IReportContentFormatResolver
↓
TextContentNode.Format
TableContentNode.Format / CaptionSequence
ReportContentDocument.PageLayout / FormatWarnings
```

For successful legacy-bound, static, and multi-source tables, Sprint 15 composition still happens first in `BuildComposedTableNode`; table format resolution occurs only after the composition call.

`BuildMultiSourceErrorNode` still bypasses the composer. The incomplete table is nevertheless assigned a resolved table format because formatting and source-completeness semantics are independent.

Profile warnings flow into `ReportContentDocument.FormatWarnings`. A friendly provider status flows there only when `IsMissing=true`; informational success status is not relabeled as a warning.

`ReportContentBuilder` contains no `DocumentFormat.OpenXml` / OpenXML extraction logic.

Application DI adds exactly one bootstrap registration for each new seam:

```text
IReferenceDocumentFormatProvider -> NoReferenceDocumentFormatProvider
IReportContentFormatResolver -> DefaultReportContentFormatResolver
```

Existing production registration remains:

```text
ITableContentRowComposer -> SerialQuantityTableContentRowComposer
```

Team A can later register the real provider in Infrastructure DI, which is applied after Application DI in the current composition root. Team B owns the explicit Application DI replacement from the default resolver to its reference-aware resolver.

## 8. Focused contract tests

Added ten focused Sprint 16 bootstrap tests:

1. `Project_HasPersistedReferenceFormatAssetIdentity`
2. `ReferenceFormatDocument_UsesProjectOwnedEmbeddedEntryAndJsonIgnore`
3. `TableElement_HasPersistedReferenceTableFormatKey`
4. `NoReferenceProvider_ReturnsNoProfileAndNotMissing`
5. `DefaultResolver_PreservesExistingAuthoredTextAndPageLayout`
6. `SharedContentAndPayloadContracts_DefaultToCompatibilityFormatsAndWarnings`
7. `ReportContentBuilder_ResolvesFormatsAfterCompositionAndPropagatesProfileWarnings`
8. `ReportContentBuilder_DirectConstructors_KeepNoReferenceFallbackCompatibility`
9. `ReportContentBuilder_MissingReferenceStatus_FlowsToFormatWarnings`
10. `ReportContentBuilder_SourceError_DoesNotComposePartialRows_ButStillResolvesTableFormat`

The orchestration test uses spy provider/resolver/composer instances and verifies:

- one reference-format read;
- composition before the table-format resolver result is placed on the node;
- text/table/page resolver calls receive the same profile;
- composed rows remain authoritative;
- caption sequence propagation;
- profile warning propagation.

No real reference DOCX analyzer, table-profile selection algorithm, Engine formatting behavior, WPF formatting renderer, grouping diagnostic UI, or Word fidelity behavior is tested or implemented in the bootstrap.

Baseline source inventory:

- 308 test methods
- 0 skip attributes

Current source inventory:

- 318 test methods
- 0 skip attributes

## 9. ADR

Added:

`docs/adr/0015-reference-docx-format-profile-pipeline.md`

The ADR records:

- why reference format and front matter are distinct project-owned assets;
- Application ownership of the normalized supported-property format contract;
- Infrastructure-only reference DOCX extraction behind `IReferenceDocumentFormatProvider`;
- format resolution in `ReportContentBuilder` after semantic row composition;
- resolved formats on shared content and layout payloads;
- stable `ReferenceTableFormatKey` selection identity;
- real `SEQ` field semantics instead of cached visible numbering;
- direct-construction compatibility fallbacks;
- rejection of Engine/UI/Word DOCX parsing;
- rejection of `w:vMerge`-to-grouping inference;
- no pixel-identical Word claim.

## 10. Exact change record and frozen bootstrap ownership

Modified:

- `src/KKL.WordStudio.Domain/Projects/Project.cs`
- `src/KKL.WordStudio.Domain/Elements/TableElement.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentBuilder.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentDocument.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentNode.cs`
- `src/KKL.WordStudio.Application/Layout/DocumentLayoutContracts.cs`
- `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `tests/KKL.WordStudio.Architecture.Tests/FrozenContractShapeTests.cs`

Added:

- `src/KKL.WordStudio.Domain/Projects/ReferenceFormatDocument.cs`
- `src/KKL.WordStudio.Application/Formatting/ReferenceDocumentFormatContracts.cs`
- `src/KKL.WordStudio.Application/Formatting/IReferenceDocumentFormatProvider.cs`
- `src/KKL.WordStudio.Application/Formatting/IReportContentFormatResolver.cs`
- `src/KKL.WordStudio.Application/Formatting/DefaultFormatProfiles.cs`
- `src/KKL.WordStudio.Application/Formatting/NoReferenceDocumentFormatProvider.cs`
- `src/KKL.WordStudio.Application/Formatting/DefaultReportContentFormatResolver.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint16ContractBootstrapTests.cs`
- `docs/SERO-REFERENCE-FORMAT-PROFILE.md`
- `docs/adr/0015-reference-docx-format-profile-pipeline.md`
- `docs/sprint16-contract-bootstrap-report.md`

The core Sprint 16 contract/orchestration bootstrap files are frozen for Teams A/B/C/D:

- `src/KKL.WordStudio.Domain/Projects/Project.cs`
- `src/KKL.WordStudio.Domain/Projects/ReferenceFormatDocument.cs`
- `src/KKL.WordStudio.Domain/Elements/TableElement.cs`
- `src/KKL.WordStudio.Application/Formatting/ReferenceDocumentFormatContracts.cs`
- `src/KKL.WordStudio.Application/Formatting/IReferenceDocumentFormatProvider.cs`
- `src/KKL.WordStudio.Application/Formatting/IReportContentFormatResolver.cs`
- `src/KKL.WordStudio.Application/Formatting/DefaultFormatProfiles.cs`
- `src/KKL.WordStudio.Application/Formatting/NoReferenceDocumentFormatProvider.cs`
- `src/KKL.WordStudio.Application/Formatting/DefaultReportContentFormatResolver.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentBuilder.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentDocument.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentNode.cs`
- `src/KKL.WordStudio.Application/Layout/DocumentLayoutContracts.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint16ContractBootstrapTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/FrozenContractShapeTests.cs`
- `docs/SERO-REFERENCE-FORMAT-PROFILE.md`
- `docs/adr/0015-reference-docx-format-profile-pipeline.md`
- `docs/sprint16-contract-bootstrap-report.md`

Explicit handoff exceptions required by the shared ownership contract:

- Team B may edit `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs` only to replace `IReportContentFormatResolver -> DefaultReportContentFormatResolver` with its real `ReferenceReportContentFormatResolver` registration and may add new Team B-owned files under `Application/Formatting/**`; it may not alter the frozen interfaces/contracts/default compatibility services listed above.
- Team A may register its real `IReferenceDocumentFormatProvider` from `InfrastructureServiceCollectionExtensions.cs`; no frozen Application provider interface or builder contract change is authorized.

No other Team A/B/C/D edit to the frozen list is authorized without a contract change request.

## 11. Actual verification

The required runtime commands could not be executed because this environment has no `dotnet` CLI.

Actual availability check:

```text
command -v dotnet
<no path returned>
```

Therefore:

- `dotnet restore`: NOT RUN — .NET CLI unavailable
- `dotnet build`: NOT RUN — .NET CLI unavailable
- `dotnet test`: NOT RUN — .NET CLI unavailable

No green build/test claim is made.

Static/source checks actually performed:

- all 12 `.csproj` files parse as XML;
- every `ProjectReference` target exists;
- `Project.ReferenceFormat` and `TableElement.ReferenceTableFormatKey` exist;
- default reference asset entry is exact;
- `ResolvedFilePath` is `[JsonIgnore]`;
- every frozen formatting contract type has exactly one production definition;
- provider and resolver method shapes are present;
- content/document/layout payload format extensions are present with compatibility defaults;
- the frozen layout-shape architecture guard recognizes `Format` as the intentional Sprint 16 optional/default-compatible payload extension while retaining all prior required members;
- one-argument and composer-aware `ReportContentBuilder` constructors remain present;
- reference-format provider call occurs before report traversal;
- successful table composition call occurs before table-format resolution in the shared helper;
- `BuildMultiSourceErrorNode` does not call the composer;
- `ReportContentBuilder` contains no OpenXML reference;
- exactly one bootstrap provider DI registration exists;
- exactly one bootstrap resolver DI registration exists;
- Sero page size, margins, header/footer distances, two table grids, fixed layout, border size, cell margins, repeated header, row height, and `SEQ Tablo` evidence were asserted directly from OOXML;
- Sero rendered successfully to three page PNGs and every page was visually reviewed;
- current source test inventory is 318 methods with 0 skip attributes;
- no `bin` / `obj` directories are present.

Windows/.NET 8 restore/build/test remains mandatory final truth before parallel Sprint 16 team implementation starts from this contract baseline.
