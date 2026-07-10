# Sprint 16 Team D — Word Reference Fidelity + Adversarial QA Report

## 1. Role and scope

Team D implemented supported Word reference-format fidelity and adversarial architecture guards under the Sprint 16 frozen shared contract.

Production changes are limited to the four Team D-owned Word writer files:

- `WordParagraphWriter.cs`
- `WordContentWriter.cs`
- `WordPageLayoutWriter.cs`
- `WordTableWriter.cs`

QA additions are limited to Sprint 16-named Infrastructure/Architecture test files and Team D documentation. No frozen bootstrap contract file was edited.

Team D did not parse `Sero.docx`, did not add a reference-format provider/resolver, and did not implement Engine/UI behavior. Word consumes resolved shared content formats.

## 2. Exact supplied baseline inventory and provenance

The exact supplied `KKL.WordStudio-Sprint16-Contract-Baseline.zip` source inventory contains:

- 318 `[Fact]` / `[Theory]` test methods;
- 52 test source files containing test methods;
- 0 `Skip` attributes.

This is the Sprint 15 stabilized source inventory plus the ten Sprint 16 contract-bootstrap tests described by the bootstrap report. The shared contract refers to the 308-test Sprint 15 stabilized baseline; the exact supplied Sprint 16 contract baseline is therefore correctly inventoried as 318 source test methods after bootstrap.

The supplied bootstrap report also records that the exact patched source archive does not carry proof of a final 308/308 Windows-green rerun. Team D does not reinterpret that provenance and makes no Windows-green claim for the starting package.

## 3. Contract review result — caption format transport gap

A real frozen-contract integration gap was found.

`DocumentFormatProfile` exposes:

```csharp
ResolvedTextFormat TableCaption
```

but `TableContentNode` transports only:

```csharp
ResolvedTableFormat Format
TableCaptionSequenceProfile? CaptionSequence
```

The Word layer receives `TableContentNode`. It is forbidden to parse the reference DOCX, read `Project.ReferenceFormat`, or reproduce the reference resolver. Therefore the resolved caption appearance cannot reach `WordContentWriter` / `WordParagraphWriter`.

This prevents reference-driven emission of the Sero caption properties such as alignment, font/size, spacing, line spacing, indent, and keep-next without Sero-specific hard-coding.

Created:

`docs/CONTRACT_CHANGE_REQUEST-D.md`

The smallest requested compatible extension is an optional/default-null `ResolvedTextFormat? CaptionFormat` on `TableContentNode`, populated from the already resolved `formatProfile?.TableCaption` by `ReportContentBuilder`.

Team D deliberately did not hard-code Sero caption appearance. The structural test `SeroWord_Caption_WritesCenteredReferenceFormat` remains an explicit contract-blocking red gate until the contract request is reviewed and the transport is implemented.

## 4. Word paragraph fidelity

`WordParagraphWriter` now consumes `TextContentNode.Format` for supported direct formatting.

Implemented:

- `RunFonts` ASCII and HAnsi from `FontFamilyName`;
- font size in Word half-points;
- bold;
- italic;
- underline;
- foreground color normalized to Word RGB hex;
- paragraph justification;
- before/after spacing in twentieths of a point;
- automatic line spacing using `round(LineSpacingMultiple * 240)`;
- left indent millimeters to twips;
- first-line indent millimeters to twips;
- keep-with-next.

Heading 1 / Heading 2 style IDs remain applied for existing TOC semantics. Resolved direct formatting is also emitted.

A compatibility path preserves legacy directly constructed `TextContentNode` tests that rely on the old `Bold` and `FontSize` properties while the shared compatibility properties remain present.

## 5. Word page fidelity

`WordPageLayoutWriter` continues to write:

- page width;
- page height;
- orientation;
- top/bottom/left/right margins.

Sprint 16 support adds resolved:

- `HeaderDistanceMillimeters` -> Word header distance twips;
- `FooterDistanceMillimeters` -> Word footer distance twips.

The controlled Sero profile tests use approximately 210 x 297 mm page geometry, approximately 25 mm margins, and approximately 12.488 mm header/footer distances.

## 6. Word caption semantics

When `TableContentNode.CaptionSequence` is present, the caption writer now emits a real Word `SEQ` field rather than authoritative cached plain numbering.

The emitted structure uses:

- visible `DisplayLabel`;
- `SimpleField` with `SEQ <SequenceIdentifier> \\* ARABIC` instruction;
- configured separator;
- descriptive `table.Caption` text.

A narrow deterministic duplicate-number rule handles only the exact leading form:

`<DisplayLabel> <digits><Separator><description>`

It removes that leading authored number before field-based numbering. It does not broadly regex arbitrary digits elsewhere in the caption.

Resolved caption visual formatting remains blocked by the frozen transport gap described in section 3.

## 7. Word table fidelity

`WordTableWriter` now consumes `TableContentNode.Format`.

Implemented supported properties:

- width percentage -> Word table pct width;
- fixed/autofit layout;
- border point size -> eighth-points;
- default table cell top/bottom/left/right margins;
- proportional `TableGrid` widths from column `WidthWeight`;
- proportional per-cell preferred widths from the same weights;
- repeated header according to resolved format;
- preferred row height as an at-least height;
- per-column header paragraph alignment;
- per-column body paragraph alignment;
- per-column header font family/size/bold;
- per-column body font family/size/bold;
- per-column vertical alignment;
- per-column no-wrap.

A valid resolved column profile is used only when its column count matches the physical table and every width weight is finite and positive. Otherwise deterministic compatibility defaults preserve the old equal-width behavior.

The Sprint 15 complete semantic `TableContentNode.CellSpans` path remains authoritative. Word still emits:

- `w:vMerge restart` on semantic span anchors;
- `w:vMerge continue` on covered cells;
- a full `TableCell` count on every physical row.

No page-fragment spans or `DocumentLayoutResult` are consumed by Word.

## 8. Sprint 16 Word structural fidelity tests

Added `Sprint16WordReferenceFidelityTests.cs` with the exact 16 requested structural test methods:

1. `SeroWord_Page_WritesA4And25MmMargins`
2. `SeroWord_Page_WritesHeaderFooterDistance`
3. `SeroWord_PrimaryHeading_WritesArial12BoldItalicSpacingKeepNext`
4. `SeroWord_SecondaryHeading_WritesReferenceIndent`
5. `SeroWord_CenteredIdentifier_WritesCenterBold11`
6. `SeroWord_Caption_WritesCenteredReferenceFormat`
7. `SeroWord_Caption_WritesRealSeqField`
8. `SeroWord_Table_WritesFixedFullWidthAndHalfPointBorders`
9. `SeroWord_Table_WritesReferenceCellMargins`
10. `SeroWord_Table_WritesPreferredRowHeight`
11. `SeroWord_Table_WritesUnequalGridWidths`
12. `SeroWord_Table_ProductNameLeftOthersCentered`
13. `SeroWord_Table_HeaderRepeats`
14. `SeroWord_Table_GroupedSerial_PreservesTrueVMerge`
15. `SeroWord_Table_SerialColumn_RemainsUnmerged`
16. `SeroWord_GeneratedDocx_ReopensWithExpectedReferenceProperties`

The tests use a controlled resolved profile matching the supported values frozen in `SERO-REFERENCE-FORMAT-PROFILE.md`. They do not parse the reference DOCX.

Test 6 is intentionally contract-blocking while `TableContentNode` cannot carry the resolved caption format. It fails with a narrow message pointing to `docs/CONTRACT_CHANGE_REQUEST-D.md` rather than accepting a hard-coded approximation.

## 9. Sprint 16 architecture guards

Added `Sprint16ReferenceFidelityArchitectureGuardTests.cs` with 11 guards covering:

- `Project.ReferenceFormat` and `Project.FrontMatter` remain distinct project-owned assets;
- Application/Engine/UI do not parse reference DOCX/OpenXML;
- `ReportContentBuilder` depends on the reference provider and format resolver;
- table composition remains before table format resolution;
- Engine/UI/Word do not contain known Sero-specific hard-coded titles;
- Engine/UI/Word do not contain literal exact Sero table-width arrays;
- Word writers consume resolved formats and do not read reference asset paths;
- `SerialQuantityGrouping` role identities remain `Guid` column IDs;
- no DOCX-to-`ReportElement` reverse-engineering types are introduced;
- Project Explorer remains separate from the KAYNAK VERİ surface;
- no Office Interop, WebView, or new PDF implementation is introduced;
- the caption-format transport gap must either be implemented through the shared contract or remain represented by an explicit Team D contract request.

The scans target production source and avoid treating test/profile documentation strings as product hard-coding.

## 10. Regression preservation review

Static compatibility review was performed against existing Word and Sprint 15 tests.

Preserved intentionally:

- Heading1/Heading2 TOC style semantics;
- old direct-node `Bold` / `FontSize` compatibility behavior;
- default fixed/full-width table behavior;
- equal-width fallback values used by historical tests;
- repeated Word table header defaults;
- merged-cell vertical centering in the compatibility path;
- unmerged compatibility cells not receiving a new forced vertical alignment;
- complete semantic vMerge lookup behavior;
- every row retaining the full physical cell count;
- `WordExporter` continuing to consume `ReportContentDocument`;
- altChunk/front-matter composition paths untouched;
- explicit Word Styles/Header/Footer save paths untouched.

## 11. Test inventory after Team D changes

Current source inventory:

- 345 `[Fact]` / `[Theory]` test methods;
- 54 test source files containing test methods;
- 0 `Skip` attributes.

Delta from exact supplied Sprint 16 contract baseline:

- +16 Sprint 16 Word structural fidelity tests;
- +11 Sprint 16 architecture guard tests;
- +27 total test methods;
- +2 test source files;
- 0 baseline test files removed;
- 0 baseline test methods removed/renamed by Team D;
- 0 skipped tests added.

## 12. Ownership audit

Exact pristine-baseline comparison shows Team D changes only in allowed ownership paths:

Modified production files:

- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordContentWriter.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordPageLayoutWriter.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordParagraphWriter.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordTableWriter.cs`

Added tests:

- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint16WordReferenceFidelityTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/Sprint16ReferenceFidelityArchitectureGuardTests.cs`

Added Team D docs:

- `docs/sprint16-team-d-word-qa-report.md`
- `docs/sprint16-integration-checklist.md`
- `docs/CONTRACT_CHANGE_REQUEST-D.md`

No frozen bootstrap contract file was edited.

## 13. Actual verification output

The required commands were attempted from the Team D workspace.

Actual output:

```text
=== dotnet restore ===
bash: line 4: dotnet: command not found
EXIT_CODE=127
=== dotnet build ===
bash: line 8: dotnet: command not found
EXIT_CODE=127
=== dotnet test ===
bash: line 12: dotnet: command not found
EXIT_CODE=127
SUMMARY restore=127 build=127 test=127
```

An attempt to obtain the official .NET install script also failed because this container could not resolve `dot.net`:

```text
curl: (6) Could not resolve host: dot.net
```

Therefore:

- `dotnet restore`: NOT RUN SUCCESSFULLY — CLI unavailable;
- `dotnet build`: NOT RUN SUCCESSFULLY — CLI unavailable;
- `dotnet test`: NOT RUN SUCCESSFULLY — CLI unavailable;
- Windows/.NET 8 verification: PENDING;
- green claim: NONE.

## 14. Static checks actually completed

The following static checks were executed successfully:

- exact baseline/current test inventory and skip count;
- pristine-baseline file hash comparison and Team D ownership audit;
- production scans for prohibited reference-DOCX/OpenXML parsing in Application/Engine/UI;
- production scans for known Sero-specific title literals;
- production scans for literal exact Sero grid-width arrays;
- production scans for Office Interop/WebView/new PDF implementation markers;
- Word source marker check for resolved `text.Format` consumption;
- Word source marker check for resolved `tableNode.Format` consumption;
- page writer marker check for header/footer distance consumption;
- Word source check rejecting `ReferenceFormat`, `OriginalSourcePath`, `ResolvedFilePath`, and `WordprocessingDocument.Open` dependencies;
- architecture contract review confirming the caption format transport gap is represented by `CONTRACT_CHANGE_REQUEST-D.md`.

## 15. Integration status

Team D delivery is complete within ownership, but Sprint 16 is **not green** from this environment.

Two explicit gates remain:

1. Windows/.NET 8 `restore`, `build`, and `test` must run on the integrated source.
2. `CONTRACT_CHANGE_REQUEST-D.md` must be reviewed. Sero caption visual fidelity cannot be honestly declared complete until resolved caption formatting is transported to the Word layer through the shared semantic content path.

The detailed branch/cross-branch review and exact manual Windows scenarios A-I are recorded in `docs/sprint16-integration-checklist.md`.
