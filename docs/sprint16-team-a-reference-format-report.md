# Sprint 16 Team A — Reference DOCX Format Analyzer / Project Asset Report

## 1. Role and ownership

Role: **Reference DOCX Format Analyzer / Project Asset Engineer**.

Implemented only inside the Team A ownership surface:

- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/**`
- `src/KKL.WordStudio.Infrastructure/Persistence/KwsProjectRepository.cs`
- `src/KKL.WordStudio.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint16ReferenceFormatTests.cs`
- this report

No Domain, frozen Application formatting/content/layout contract, Engine, UI, Word writer, or Architecture test file was edited.

The frozen Sprint 16 reference profile in the contract baseline is byte-identical to the separately supplied `SERO-REFERENCE-FORMAT-PROFILE (1).md` (`SHA-256 ff64c213d616d049668b1501c143e3ba56d4d0260c75ceb5d992b341c8480d18`).

No `CONTRACT_CHANGE_REQUEST-A.md` was required.

## 2. Reference-format import service

Added:

- `IReferenceFormatDocumentService`
- `OpenXmlReferenceFormatDocumentService`
- `ReferenceFormatSourcePathResolver`

The import service:

- accepts `.docx` only;
- rejects a missing source;
- opens the package with `WordprocessingDocument.Open(path, false)`;
- requires a readable `MainDocumentPart` / `Body`;
- returns the frozen Domain `ReferenceFormatDocument` with:
  - original file name;
  - informational original source path;
  - runtime `ResolvedFilePath`;
  - `ReferenceFormatDocument.DefaultEmbeddedAssetEntryName`;
- never edits or rewrites the source package.

The service interface is intentionally Infrastructure-owned under Team A's `ReferenceFormatting/**` ownership area. The frozen Application bootstrap does not define `IReferenceFormatDocumentService`, while the Team A prompt explicitly requires that service and its production DI registration. No frozen Application file was changed to invent a competing contract.

## 3. Project-owned `.kws` reference asset persistence

`KwsProjectRepository` was extended narrowly in parallel with the existing front-matter path.

Save flow now:

1. normalizes `Project.ReferenceFormat.EmbeddedAssetEntryName` to the frozen default;
2. writes `project.json` with the existing `PreferredObjectCreationHandling.Populate` behavior unchanged;
3. writes front matter to its existing entry;
4. separately writes the reference DOCX to:
   `resources/reference-format/reference-format.docx`.

Open flow now:

1. materializes front matter using the existing behavior;
2. separately materializes the reference DOCX under a private temp tree:
   `KKL.WordStudio/reference-format/<project-id>/...`;
3. sets only runtime `ResolvedFilePath` to the materialized path;
4. falls back to an existing original source path only for compatibility when the package has no embedded asset.

The reference-format and front-matter assets have distinct Domain properties, distinct embedded entry names, and distinct private materialization directories.

After a successful save, the original reference DOCX can be removed and the `.kws` package remains sufficient for reopen + provider analysis.

## 4. Production reference-format provider

Added:

- `OpenXmlReferenceDocumentFormatProvider`
- `OpenXmlReferenceFormatAnalyzer`
- `OpenXmlStyleResolver`

The provider implements the frozen `IReferenceDocumentFormatProvider` contract.

Behavior:

- no configured reference format → `Profile=null`, `IsMissing=false`;
- configured but unavailable asset → friendly Turkish missing state;
- readable asset → read-only DOCX open and normalized `DocumentFormatProfile` extraction;
- fatal unreadable package/profile → friendly Turkish unavailable/read failure state rather than mutating project content.

Production analyzer code contains no Sero-specific heading text, product-column title arrays, `SerialQuantityGrouping`, or `w:vMerge` grouping inference.

## 5. Supported WordprocessingML extraction

### Page

The analyzer reads the effective document section page geometry:

- `w:pgSz/@w:w`
- `w:pgSz/@w:h`
- `w:pgMar` top/bottom/left/right
- `w:pgMar` header/footer distance

Twips are converted deterministically to millimeters using `25.4 / 1440`.

The raw width/height geometry is preserved; the analyzer does not force an orientation by swapping dimensions.

### Style resolution

`OpenXmlStyleResolver` uses a bounded style chain with a maximum depth of 32 and cycle detection.

Supported precedence is applied in this order:

1. deterministic fallback;
2. document defaults;
3. base-to-derived paragraph style chain;
4. direct paragraph properties / paragraph mark run properties;
5. base-to-derived run-style chain;
6. direct run properties.

Supported effective properties:

- font family;
- font size;
- bold;
- italic;
- underline;
- foreground RGB color;
- paragraph alignment;
- space before/after;
- auto line spacing as `line / 240` multiple;
- left indent;
- first-line / hanging indent;
- keep-with-next.

Role text profiles use dominant effective non-empty run formatting while retaining resolved paragraph-level alignment, spacing, indent, line-spacing, and keep-next semantics. This is required for reference samples where direct run formatting supplies bold/font-size over a paragraph style.

### Text roles

- `BodyText` resolves from supported Normal / Body Text style aliases.
- `PrimaryHeading` is the first non-empty heading-like paragraph before the first table.
- `SecondaryHeading` is the next distinct heading-like paragraph before the first table.
- heading-like detection uses style identity/name, outline level, and supported keep-next + emphasis evidence; it does not hard-code Sero heading text.
- `TableCaption` comes from caption-style or real SEQ-bearing paragraphs immediately preceding a table.

The style resolver can represent the centered, bold, 11 pt authored body override demonstrated by the UAV identifier sample without adding a new element/profile type.

### SEQ table-caption metadata

Both simple and complex Word fields are scanned.

For a real `SEQ` instruction the analyzer resolves:

- visible prefix before the field → `DisplayLabel`;
- identifier from the `SEQ` instruction → `SequenceIdentifier`;
- leading visible punctuation/spacing after the field → `Separator`.

Cached field-result numbers are ignored as numbering semantics.

The analyzer does not hard-code `Table` or `Tablo` as the sequence label or identifier.

Mixed visible labels for the same sequence identifier are emitted as non-blocking profile warnings and the first encountered sequence profile remains canonical.

### Table profiles

Every body `w:tbl` is extracted as a separate `ReferenceTableFormatProfile` in document order.

Deterministic keys:

- `table-001`
- `table-002`
- etc.

Display names:

- `Referans Tablo 1`
- `Referans Tablo 2`
- etc.

Extracted supported table properties:

- visible header text;
- pct width (`5000 = 100%`);
- fixed layout;
- border size from eighth-points;
- top/bottom/left/right table-cell margins;
- representative/dominant preferred row height;
- repeated first/header row;
- table-grid widths normalized to relative percentage weights;
- per-column header/body alignment;
- dominant header/body font family, size, and bold;
- dominant vertical alignment;
- detail/body `NoWrap` behavior.

Dominant formatting selection preserves deterministic first-occurrence tie-breaking. Mixed body-run format variants are surfaced as non-blocking warnings rather than silently claiming exact uniformity.

No `w:vMerge` path exists in production `ReferenceFormatting/**`; vertical merge remains Sprint 15 semantic evidence and is not converted into grouping configuration.

## 6. DI integration

`InfrastructureServiceCollectionExtensions` now:

- replaces the single bootstrap `IReferenceDocumentFormatProvider -> NoReferenceDocumentFormatProvider` descriptor with `OpenXmlReferenceDocumentFormatProvider`;
- registers `IReferenceFormatDocumentService -> OpenXmlReferenceFormatDocumentService` once.

The Application DI source remains frozen and unchanged.

A focused test asserts that Application DI followed by Infrastructure DI leaves exactly one `IReferenceDocumentFormatProvider` descriptor and exactly one import-service descriptor with the expected implementation types.

## 7. Focused tests

Added `tests/KKL.WordStudio.Infrastructure.Tests/Sprint16ReferenceFormatTests.cs`.

All 16 prompt-required test names are present:

1. `ReferenceImport_RejectsNonDocx`
2. `ReferenceImport_DoesNotModifySource`
3. `ReferencePersistence_EmbedsAndReopensWithoutOriginalPath`
4. `ReferenceAndFrontMatter_UseSeparateAssets`
5. `SeroProfile_ExtractsA4And25MmMargins`
6. `SeroProfile_ExtractsArialBase`
7. `SeroProfile_ExtractsPrimaryAndSecondaryHeadingFormats`
8. `SeroProfile_ExtractsCenteredBodySupport`
9. `SeroProfile_ExtractsCaptionAndSeqMetadata`
10. `SeroProfile_ExtractsTwoSeparateTableProfiles`
11. `SeroTable1_ExtractsUnequalColumnWeights`
12. `SeroTable1_ExtractsPerColumnAlignment`
13. `SeroTable1_ExtractsBorderCellMarginsAndRowHeight`
14. `SeroTable2_PreservesSeparateColumnRatios`
15. `Provider_OpensDocxReadOnly`
16. `MissingReferenceAsset_ReturnsFriendlyMissingState`

Additional focused DI test:

17. `ProductionDi_ReplacesBootstrapProviderWithoutDuplicateEffectiveRegistration`

The generated focused reference fixture encodes the frozen Sero supported-property evidence:

- 11906 × 16838 twips page;
- 1417 twips margins;
- 708 twips header/footer distances;
- Arial 10 pt Normal baseline;
- distinct Balk4/Balk5 heading style chains and direct formatting;
- centered bold 11 pt identifier override;
- caption style with a real `SEQ Tablo` field;
- mixed visible sequence labels and mixed caption effective size evidence;
- Table 1 pct 5000, fixed layout, border size 4 eighth-points, 70-twip left/right margins, 578-twip preferred row height, repeated header, and grid `465/2722/1404/1661/1910/900`;
- Table 2 pct 4966 and separate grid `469/2550/1579/1579/1802/1021`;
- per-column alignment/font behavior;
- a Table 2 `w:vMerge` sample that the production analyzer intentionally ignores.

### Exact Sero execution caveat

The current code-execution sandbox mounted the baseline ZIP, shared contract, Team A prompt, bootstrap report, and SERO reference profile, but did **not** expose raw `Sero.docx` bytes as a sandbox file path. The conversation file index confirms the document exists as an uploaded reference, but that reference is not a local byte path usable by the code runner.

Therefore no claim is made that `OpenXmlReferenceDocumentFormatProvider` was executed against the exact reference SHA-256 `8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20` in this environment.

The implementation was driven by the authoritative frozen SERO profile and generated focused DOCX fixtures carrying the required supported properties. Exact raw-Sero provider execution remains part of Windows/final integration verification.

## 8. Actual verification

The environment has no `dotnet` CLI.

Actual availability check:

```text
command -v dotnet
<no path returned>
exit code: 1
```

Therefore:

- `dotnet restore`: NOT RUN — .NET CLI unavailable
- `dotnet build`: NOT RUN — .NET CLI unavailable
- `dotnet test`: NOT RUN — .NET CLI unavailable

No green build/test claim is made.

Static/source checks actually performed:

- all 9 changed/new C# files parse with the C# tree-sitter grammar;
- total syntax parse errors: `0`;
- all 12 `.csproj` files parse as XML;
- every `ProjectReference` target exists;
- baseline source test inventory: `318` Fact/Theory methods, `0` skips;
- current source test inventory: `335` Fact/Theory methods, `0` skips;
- all 16 required Team A test names are present;
- frozen bootstrap contract files changed: `0`;
- Team A ownership violations: `0`;
- production `ReferenceFormatting/**` contains no `SerialQuantityGrouping`, `vMerge`, `Sero`, `Product Name`, `Product Number`, or Generator-specific literal;
- both import and provider source contain `WordprocessingDocument.Open(..., false)`;
- `PreferredObjectCreationHandling.Populate` remains unchanged in `KwsProjectRepository`;
- reference-format and front-matter embedded entry constants remain distinct;
- Application bootstrap DI file remains unchanged;
- the frozen SERO profile supplied separately is byte-identical to the copy embedded in the baseline.

Official API shape was also checked against Microsoft documentation for:

- `OpenXmlPart.GetStream(FileMode, FileAccess)`;
- `ServiceDescriptor.Singleton<TService,TImplementation>()`;
- `ServiceCollectionDescriptorExtensions.Replace`.

Windows/.NET 8 restore/build/test and exact raw-Sero provider execution remain mandatory final truth.

## 9. Change record

Added:

- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/IReferenceFormatDocumentService.cs`
- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/ReferenceFormatSourcePathResolver.cs`
- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/OpenXmlReferenceFormatDocumentService.cs`
- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/OpenXmlReferenceDocumentFormatProvider.cs`
- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/OpenXmlReferenceFormatAnalyzer.cs`
- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/OpenXmlStyleResolver.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint16ReferenceFormatTests.cs`
- `docs/sprint16-team-a-reference-format-report.md`

Modified:

- `src/KKL.WordStudio.Infrastructure/Persistence/KwsProjectRepository.cs`
- `src/KKL.WordStudio.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

No contract change request created.
