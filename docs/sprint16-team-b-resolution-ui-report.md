# Sprint 16 Team B — Format Resolution + UX / Grouping Diagnostics Report

## 1. Role and scope

Team B implemented the Sprint 16 shared format-resolution and UX/grouping-diagnostics slice against `KKL.WordStudio-Sprint16-Contract-Baseline.zip` only.

Implemented ownership areas:

- real Application `ReferenceReportContentFormatResolver`;
- production resolver DI replacement;
- separate reference-format document command/state in the paginated Preview toolbar;
- reference table-format selection in Table Properties;
- Serial/Quantity grouping diagnosis, auto-detect action, manual stable-column role mapping, and confirmed removal;
- focused Sprint 16 Application tests.

Not implemented:

- DOCX/OpenXML parsing;
- Engine measurement or pagination changes;
- Preview cell-format rendering changes;
- Word formatting writers;
- DOCX-to-ReportElement reverse engineering.

No frozen Sprint 16 bootstrap contract file was edited. The bootstrap DI file was changed only at its explicit Team B handoff seam: the effective `IReportContentFormatResolver` registration now resolves to the real Team B resolver.

## 2. Reference-aware format resolver

Added:

- `src/KKL.WordStudio.Application/Formatting/ReferenceReportContentFormatResolver.cs`

Production DI now contains exactly one effective resolver registration:

```text
IReportContentFormatResolver -> ReferenceReportContentFormatResolver
```

There is no second `IReportContentFormatResolver` registration.

### 2.1 No reference profile

When `DocumentFormatProfile` is null, the resolver delegates to the bootstrap compatibility resolver. Existing KKL-authored text and generic table/page behavior therefore remain deterministic rather than being replaced by guessed reference styling.

### 2.2 Reference text resolution

With a profile:

- `Heading` resolves from `PrimaryHeading`;
- `AltHeading` resolves from `SecondaryHeading`;
- other generated text resolves from `BodyText`.

A semantic-default baseline is used per content kind:

- Heading baseline = current `HeadingStylePresets.CreateHeadingStyle()`;
- AltHeading baseline = current `HeadingStylePresets.CreateAltHeadingStyle()`;
- ordinary text baseline = current default `Style`.

This distinction is necessary because the current Heading/AltHeading identities are themselves encoded by non-default `Style` preset values. Treating the 18 pt / 14 pt heading presets as authored overrides would prevent the Sero reference heading sizes from ever taking effect.

Values that genuinely differ from the applicable semantic baseline are preserved as supported authored overrides for:

- font family;
- font size;
- bold;
- italic;
- underline;
- horizontal alignment.

Reference spacing, line spacing, indent, keep-with-next, and foreground remain profile-resolved.

### 2.3 Reference table resolution

Selection order is:

1. valid explicit `TableElement.ReferenceTableFormatKey`;
2. first reference profile with the same column count;
3. first available reference table profile;
4. deterministic generic fallback.

No displayed-header text is persisted as table-format identity.

### 2.4 Reference page layout

When a profile exists, the resolved `PageLayout` uses the reference page:

- width/height;
- top/bottom/left/right margins;
- header distance;
- footer distance.

The authored `ShowPageNumbers` semantic is retained.

## 3. Reference-format document command

Added a separate compact command beside the existing Ön Belge workflow:

```text
Biçim Şablonu Ekle
Biçim Şablonunu Değiştir
```

The command is distinct from `AddFrontMatterCommand` and uses a dedicated UI-only `.docx` picker titled `Biçim Şablonu Seç`.

Added Team B Application formatting boundary:

- `IReferenceFormatDocumentService`
- `ReferenceFormatDocumentService`

The service validates file availability and `.docx` extension and creates `ReferenceFormatDocument` with:

- filename;
- original source path;
- frozen embedded asset entry name;
- runtime resolved file path.

It does not open or parse the DOCX package.

On successful import:

- `Project.ReferenceFormat` is updated;
- compact filename/status state is refreshed;
- `NotifyReportContentChanged()` rebuilds shared content/layout;
- no front-matter page is added;
- the front-matter command path is not reused.

The bootstrap DI handoff explicitly authorizes only resolver-registration replacement, so the focused Team B helper service is used through its Application interface without adding extra effective Application DI registrations.

## 4. Table format Properties UI

Added compact `TABLO BİÇİMİ` section.

The combo contains:

- `Varsayılan`;
- provider-returned reference table profile display names, for example `Referans Tablo 1`, `Referans Tablo 2` when supplied by the integrated real provider.

Added:

- `ITableFormatSelectionService`
- `TableFormatSelectionService`

Selection behavior:

- null/default selection persists `ReferenceTableFormatKey = null`;
- a reference selection must exist in the current profile;
- valid selection persists only the stable format key;
- current table selection is not changed;
- rows are not changed;
- `SerialQuantityGrouping` is not changed;
- `NotifyReportContentChanged()` refreshes Preview/Word semantic content.

`PropertiesViewModel` listens to `ReportContentChanged` as well as shared selection changes so newly imported/changed reference profiles refresh the combo without requiring a table reselection.

## 5. Serial / Quantity grouping visibility

Added Team B-focused Application TableComposition types:

- `SerialQuantityGroupingDiagnosis`
- `ISerialQuantityGroupingConfigurationService`
- `SerialQuantityGroupingConfigurationService`

This is the focused diagnosis/configuration seam explicitly permitted by the Team B prompt. It does not redefine the Team A detector or alias sets.

Diagnosis uses the existing `ColumnRoleAliasNormalizer` role semantics and the existing `ISerialQuantityGroupingDetector`.

For the Sprint 16 screenshot case where Match Key and Serial exist but Quantity/Adet does not, the user-visible diagnosis is:

```text
Yapılandırılmadı
Adet sütunu bulunamadı.
```

Configured diagnosis resolves the persisted role IDs back to the current `TableColumn` instances and displays the current column names. Header renaming therefore does not break role identity.

Additional deterministic diagnosis covers:

- missing Serial No;
- missing match key;
- multiple Quantity candidates;
- multiple Serial candidates;
- multiple match-key candidates;
- overlapping role-column identities;
- invalid persisted role references.

No Product No / Serial / Quantity alias list is duplicated in UI code.

## 6. Manual role mapping and grouping actions

Added `SERİ NO / ADET DÜZENİ` Table Properties section with:

- status text;
- `Otomatik Algıla`;
- `Düzenle`;
- `Kaldır`.

The manual editor lists the current `TableColumn` set for:

- Eşleşme alanı;
- Seri no alanı;
- Adet alanı.

Manual apply:

- requires all three selections;
- requires three distinct `TableColumn.Id` values;
- verifies all IDs still belong to the selected table;
- persists `SerialQuantityGrouping` with stable IDs;
- writes `WasAutoDetected = false`;
- keeps the same table selected;
- calls `NotifyReportContentChanged()`.

Auto-detect:

- calls the existing Team A detector;
- applies its returned IDs only on success;
- leaves an existing valid configuration untouched on failure;
- shows the focused diagnosis message on failure.

Remove:

- is enabled only when configuration exists;
- requires destructive confirmation;
- sets `TableElement.SerialQuantityGrouping = null`;
- rebuilds report content.

No role identity is persisted by header string or column index.

## 7. Focused tests added

Added:

`tests/KKL.WordStudio.Application.Tests/Sprint16ResolutionAndGroupingTests.cs`

14 focused tests:

1. `Resolver_UsesReferencePrimaryHeading`
2. `Resolver_UsesReferenceSecondaryHeading`
3. `Resolver_DefaultStyleDoesNotOverrideReference`
4. `Resolver_ExplicitTextStyleOverrideWins`
5. `Resolver_SelectsExplicitReferenceTableKey`
6. `Resolver_NullTableKey_SelectsSameColumnCountProfile`
7. `Resolver_ReferencePageGeometryWins`
8. `GroupingDiagnosis_MissingQuantityExplainsAdetColumn`
9. `GroupingDiagnosis_ConfiguredShowsStableColumns`
10. `ManualGroupingApply_PersistsStableColumnIds`
11. `ManualGroupingApply_RejectsDuplicateRoleColumns`
12. `AutoDetectFailure_DoesNotDestroyValidExistingConfig`
13. `RemoveGrouping_SetsConfigurationNull`
14. `TableFormatSelection_PersistsKeyWithoutChangingRowsOrGrouping`

Baseline source inventory recorded by the Sprint 16 bootstrap was 318 test methods / 0 skips.

Current source inventory after Team B additions is:

- 332 `[Fact]` / `[Theory]` methods;
- 0 `Skip` attributes.

These tests were added but were not executed in this environment because the .NET CLI is unavailable.

## 8. Static/source acceptance actually performed

Actual static checks performed:

- Team B source/test change set before this report: 16 files;
- ownership violations: 0;
- frozen bootstrap format/content/layout/domain contracts unchanged;
- Application DI contains exactly one effective `IReportContentFormatResolver` registration;
- that registration targets `ReferenceReportContentFormatResolver`;
- no extra Team B helper-service registrations were added to the restricted bootstrap DI handoff;
- Preview/Properties XAML XML parse: 2/2 successful;
- Preview XAML event/EventSetter handlers resolve in existing code-behind: 21/21;
- changed C# source basic brace/parenthesis balance: no mismatch found;
- separate `AddReferenceFormatCommand` present;
- reference-format command does not reuse `AddFrontMatterCommand`;
- `TABLO BİÇİMİ` section and profile combo bindings present;
- `SERİ NO / ADET DÜZENİ` status/actions/manual selectors present;
- diagnosis path consumes existing `ColumnRoleAliasNormalizer` semantics;
- auto-detect assigns configuration only after detector success;
- no `DocumentFormat.OpenXml` reference in Team B changed production/test source files;
- no Engine production change;
- no Infrastructure production change;
- source test inventory: 332 methods / 0 skips;
- no `bin` or `obj` directories present.

## 9. Runtime verification

The environment does not contain a `dotnet` executable.

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

The Sprint 16 bootstrap report also records that the supplied baseline archive does not itself contain proof that the exact patched Sprint 15 source was rerun 308/308 green on Windows. Team B did not reinterpret that provenance discrepancy.

Windows/.NET 8 restore/build/test remains mandatory integration truth.

## 10. Contract change request

No `docs/CONTRACT_CHANGE_REQUEST-B.md` was created.

The frozen contracts were sufficient. The focused grouping diagnosis/configuration helper was added under `Application/TableComposition` using the explicit Team B prompt allowance, and the missing reference-format import boundary was added under Team B-owned `Application/Formatting/**` without changing frozen bootstrap interfaces or payload contracts.
