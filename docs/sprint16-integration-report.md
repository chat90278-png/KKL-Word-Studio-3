# Sprint 16 Integration Report — Reference DOCX Format Fidelity

## 1. Branch diff / ownership decision

Integration started from `KKL.WordStudio-Sprint16-Contract-Baseline.zip` only. A/B/C/D were reviewed independently against that baseline and the reviewed branch file sets were reapplied rather than overlaying one branch ZIP on another.

Observed branch deltas matched the reviewed decision:

- Team A: 2 changed + 8 added, 0 removed. The duplicate Infrastructure-owned `IReferenceFormatDocumentService.cs` was deliberately not accepted.
- Team B: 5 changed + 12 added, 0 removed. The simple Application `ReferenceFormatDocumentService.cs` implementation was deliberately not retained.
- Team C: 12 changed + 4 added, 0 removed.
- Team D: 4 changed + 5 added, 0 removed.

A/B/C production overlap was zero except `PreviewView.xaml`. The integrated XAML reapplies both accepted areas: Team B's Biçim Şablonu command/status hunk and Team C's resolved text/table rendering hunks.

Branch reapply hash audit found no missing reviewed file and no unexpected mismatch among reviewed files not intentionally touched by central integration fixes. Exact reapply counts after central-fix exceptions were A 6, B 14, C 6, D 5.

## 2. Frozen contract hash result

All Sprint 16 frozen bootstrap Domain/Application formatting/content/layout contract files were byte-identical to the Contract Baseline in each A/B/C/D branch.

The integrated tree changes only the approved shared-contract transport surface:

- `src/KKL.WordStudio.Application/Content/ReportContentNode.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentBuilder.cs`
- `src/KKL.WordStudio.Application/Layout/DocumentLayoutContracts.cs`

The related bootstrap/shape test files were changed only to add integration coverage and recognize the approved optional payload extension:

- `tests/KKL.WordStudio.Application.Tests/Sprint16ContractBootstrapTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/FrozenContractShapeTests.cs`

No persisted Domain contract was added for caption format.

## 3. CONTRACT_CHANGE_REQUEST-D decision = APPROVED

`docs/CONTRACT_CHANGE_REQUEST-D.md` is approved.

The request correctly identifies that `DocumentFormatProfile.TableCaption` was resolved but dropped before consumers. A Word-only side channel, reference-DOCX parsing in Word, or Sero caption constants would violate the Sprint 16 architecture. The approved additive runtime transport is therefore:

`DocumentFormatProfile.TableCaption`
→ `TableContentNode.CaptionFormat`
→ Engine / payload / Preview

and independently:

`TableContentNode.CaptionFormat + CaptionSequence`
→ Word resolved caption appearance + real SEQ field.

## 4. Caption-format semantic transport

Added default-compatible `ResolvedTextFormat? CaptionFormat` to `TableContentNode` and `TablePageBlockPayload`.

`ReportContentBuilder` assigns `formatProfile?.TableCaption` in both `BuildComposedTableNode` and `BuildMultiSourceErrorNode`. Composition remains before format assignment.

`GeneratedDocumentPaginator` passes the format into `DeterministicTablePaginator`. Resolved caption measurement uses `MeasureResolvedText`, including indent-adjusted available width and resolved line-spacing, plus `SpaceBeforePoints` and `SpaceAfterPoints`. A null format preserves the Sprint 14/15 compatibility estimate: 11 pt bold plus the existing 1 mm gap.

Every generated/fallback table payload carries `CaptionFormat`; caption text remains first-fragment-only.

Preview projection carries the same `ResolvedTextFormat`. A real caption uses `PreviewTextBlockControl` and projects font family, size, bold, italic, underline, foreground, alignment, line height, and first-line indent. The placeholder remains a separate italic/subdued path with the exact text `Tablo başlığı eklemek için çift tıklayın`. The legacy 3-DIP caption gap is kept only when `CaptionFormat` is null; a resolved caption does not re-add paragraph spacing after Engine measurement.

Word `WordContentWriter` passes `CaptionFormat` to `WordParagraphWriter.BuildTableCaptionParagraph`. When non-null, paragraph properties use resolved keep-next, spacing, auto line spacing, left/first-line indent, and justification. Display label, SEQ field-result run, and separator/description runs all receive the same resolved run properties. When null, the old bold compatibility caption behavior remains.

`SeroWord_Caption_WritesCenteredReferenceFormat` was retained under the same test name and converted from a reflection contract-block gate to a real Word behavior test.

## 5. Single reference-format import boundary fix

The single authoritative service boundary is now:

`KKL.WordStudio.Application.Formatting.IReferenceFormatDocumentService`.

There is exactly one public production interface with that name in `src/**`.

Removed/not integrated:

- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/IReferenceFormatDocumentService.cs`
- `src/KKL.WordStudio.Application/Formatting/ReferenceFormatDocumentService.cs`

`OpenXmlReferenceFormatDocumentService` imports the Application formatting namespace and implements the Application interface. Infrastructure DI registers:

`IReferenceFormatDocumentService -> OpenXmlReferenceFormatDocumentService`.

`PreviewViewModel` now receives `IReferenceFormatDocumentService` through its constructor and assigns the injected service. It no longer constructs `new ReferenceFormatDocumentService()` and the UI does not reference Infrastructure ReferenceFormatting.

The resulting import path is the real Team A validation path: `.docx` extension → file existence → `WordprocessingDocument.Open(path, false)` → readable `MainDocumentPart/Body` → `ReferenceFormatDocument`. The real service returns a friendly failure for non-DOCX bytes renamed to `.docx` before UI code assigns `Project.ReferenceFormat`.

## 6. Team A exact Sero analyzer / persistence integration

Accepted Team A reference-format analyzer, read-only provider, style resolver, source-path resolver, `.kws` reference asset persistence, and real provider DI replacement.

Preserved:

- source/reference DOCX opened read-only;
- Reference Format distinct from Front Matter;
- embedded reference entry `resources/reference-format/reference-format.docx`;
- private materialization path on reopen;
- `PreferredObjectCreationHandling.Populate` behavior;
- page geometry and header/footer distance extraction;
- bounded style-chain resolution with direct formatting precedence;
- resolved font, paragraph spacing/alignment/indent/keep-next;
- caption/SEQ metadata;
- separate deterministic table-001/table-002 profiles;
- table width/layout/border/margins/row height/repeat header/grid ratios/per-column formatting;
- no grouping inference from Word `w:vMerge`;
- no Sero-title hard-coding.

The exact raw Sero regression gate is present as `ExactSeroProfile_ExtractsAuthoritativeSupportedProperties` and calls the real `OpenXmlReferenceDocumentFormatProvider` after checking the exact SHA-256.

**Input blocker:** the File Library contains a `Sero.docx` reference and the frozen profile/bootstrap documents record SHA-256 `8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`, but the raw DOCX bytes were not mounted in this execution sandbox and were not present inside any supplied Sprint 16 ZIP. No synthetic fixture or reconstructed DOCX was substituted. Consequently `tests/KKL.WordStudio.Infrastructure.Tests/TestData/Sero.Reference.docx` could not honestly be populated. The test project contains a conditional copy rule that becomes active when the exact fixture is present; the exact-Sero test remains an intentional red gate until those exact bytes are supplied to the workspace.

## 7. Team B resolver / grouping diagnostics / UI integration

Accepted Team B's reference-aware resolver, table profile selection, grouping diagnosis/configuration services, Application DI replacement, Properties UI, and Biçim Şablonu command/status UI.

Preserved:

- `IReportContentFormatResolver -> ReferenceReportContentFormatResolver`;
- Heading → PrimaryHeading and AltHeading → SecondaryHeading;
- semantic defaults are not treated as authored overrides;
- supported real authored overrides remain;
- explicit stable `ReferenceTableFormatKey` selection, then same-column-count profile, then first profile, then generic fallback;
- reference page geometry with authored `ShowPageNumbers` semantics;
- separate Biçim Şablonu and Ön Belge flows;
- no reference-format front-matter pages;
- `TABLO BİÇİMİ` selection;
- explicit missing-quantity diagnosis (`Adet sütunu bulunamadı.`);
- manual MatchKey/Serial/Quantity mapping by three distinct stable `TableColumn.Id` values with `WasAutoDetected=false`;
- detector reuse and valid-config preservation on detection failure;
- `NotifyReportContentChanged()` on successful changes without provider/Excel reload.

## 8. Team C Engine / Preview format integration

Accepted Team C format-aware deterministic text/table measurement and Preview rendering.

Engine continues to consume resolved formats only and contains no DOCX/OpenXML/WPF dependency. Text measurement uses deterministic font categories, font size/bold, line spacing, left/first-line indent, and paragraph before/after spacing in page flow. Resolved `KeepWithNext` is authoritative with the narrow legacy default-format compatibility path.

Table measurement uses width percent, unequal column `WidthWeight`, per-column header/body metrics, cell margins, preferred row-height minimum, and NoWrap while preserving Sprint 15 row-group pagination and semantic-span clipping/restart.

Preview uses resolved page geometry, text typography/alignment/color/line height/indent, table width ratios/borders/margins/row height/per-column formatting/vertical alignment/no-wrap, and the same unequal widths in the editable header panel.

Sprint 15 same-ElementId interaction behavior and true rowspan rendering remain intact.

## 9. Team D Word fidelity integration

Accepted the four Team D Word writer changes and the Sprint 16 Word/architecture tests.

Preserved and integrated:

- resolved `RunFonts` Ascii/HighAnsi, half-point sizes, bold/italic/underline/color;
- paragraph justification, spacing, automatic line spacing, left/first-line indentation;
- Heading1/Heading2 native style IDs for TOC;
- page width/height/orientation/margins/header/footer distances;
- real `SEQ <SequenceIdentifier> \* ARABIC` fields and deterministic leading manual-number removal;
- resolved table width percentage, fixed/autofit layout, border eighth-points, cell margins, proportional grid/cell widths, preferred row height, repeat header, per-column typography/alignment/vertical alignment/NoWrap;
- complete semantic `CellSpans` to `w:vMerge` restart/continue with full physical cell count;
- `WordExporter` continues to consume `ReportContentDocument`, not `DocumentLayoutResult`;
- no reference asset path or reference DOCX parsing in Word;
- altChunk front-matter composition and explicit Styles/Header/Footer save paths remain untouched.

## 10. Grid.SetRowSpan stabilization

`PreviewTableGridControl` now explicitly calls:

- `Grid.SetRow(cell, rowIndex)`
- `Grid.SetColumn(cell, columnIndex)`
- `Grid.SetRowSpan(cell, rowSpan)`

This is a clarity/source-guard stabilization only. The Sprint 15 real Grid rowspan semantics and covered-cell omission behavior are unchanged. The span-consumer guard was not weakened.

## 11. KeepWithNext Engine / Word semantic alignment

`WordParagraphWriter` no longer forces KeepNext merely because a node is Heading/AltHeading.

The integrated rule is:

```text
format.KeepWithNext
|| (usesCompatibilityDefault && isHeading)
```

A real non-default resolved heading with `KeepWithNext=false` keeps native Heading1/Heading2 style identity but does not receive `KeepNext`. A positive resolved `KeepWithNext=true` still emits it. This aligns Word with Team C Engine semantics while preserving historical direct-node compatibility.

## 12. Exact raw Sero regression fixture / result

Expected exact SHA-256:

`8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`

The integration source contains the exact-provider regression test and authoritative assertions for:

- ~210.009 × 297.004 mm page;
- ~24.994 mm margins;
- ~12.488 mm header/footer distances;
- Arial 10 pt BodyText;
- 12 pt bold/italic keep-next PrimaryHeading;
- Arial centered bold TableCaption and real Tablo sequence metadata;
- exactly two table profiles;
- Table 1 100%/fixed/0.5 pt/~1.235 mm/~10.195 mm/repeated header and expected normalized weights/alignment;
- separate Table 2 key, ~99.32% width, and expected normalized weights.

**Actual result in this sandbox: BLOCKED — exact raw DOCX bytes unavailable as a mounted file.** The provider was not claimed to have passed against the exact SHA fixture. The existing Team A synthetic Sero-like fixture tests remain useful focused tests but are not treated as a substitute for the exact fixture.

## 13. DI registrations

Static composition-root audit verifies the intended registrations:

- Application bootstrap provider: one `IReferenceDocumentFormatProvider -> NoReferenceDocumentFormatProvider`, replaced once by Infrastructure with `OpenXmlReferenceDocumentFormatProvider`;
- exactly one Application resolver registration: `IReportContentFormatResolver -> ReferenceReportContentFormatResolver`;
- exactly one composer registration: `ITableContentRowComposer -> SerialQuantityTableContentRowComposer`;
- exactly one Application import-boundary registration in Infrastructure: `IReferenceFormatDocumentService -> OpenXmlReferenceFormatDocumentService`;
- exactly one `IReportContentBuilder -> ReportContentBuilder` registration.

The production DI graph has all dependencies required for the four-argument `ReportContentBuilder` constructor, so Microsoft DI can select the provider/resolver/composer-aware construction path.

## 14. Test inventory

Source inventory:

- Contract Baseline: 318 `[Fact]` / `[Theory]` methods, 52 test files, 0 skipped;
- Team A branch: 335;
- Team B branch: 332;
- Team C branch: 335;
- Team D branch: 345;
- reviewed pre-integration total expectation: 393;
- integrated source: **400 methods, 58 test files, 0 skipped**.

Baseline regression comparison:

- removed baseline test files: 0;
- removed/renamed baseline test methods: 0.

All seven required integration test names are present exactly once. `SeroWord_Caption_WritesCenteredReferenceFormat` remains present exactly once under its original name.

Static audit: **65/67 PASS**. The only two failed checks are the same exact-Sero input blocker expressed as fixture-presence and fixture-SHA checks. All csproj/XAML files parse as XML; all 24 `ProjectReference` targets exist after Windows-path normalization; all 18 Preview XAML handler names resolve in code-behind; 278 C# files pass the lexical delimiter-balance scan.

## 15. Restore / build / test actual output

Commands were executed from the integrated solution root.

### `dotnet restore`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

### `dotnet build`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

### `dotnet test`

```text
bash: line 5: dotnet: command not found
EXIT_CODE=127
```

Therefore restore/build/test were **not run successfully** in this sandbox. No green build or passing-test count is claimed. Source inventory/static scans are not reported as xUnit execution. Windows/.NET 8 remains final runtime/build truth.

The exact command outputs and static audit are included under `docs/verification/sprint16-*`.

## 16. Manual smoke status

Mandatory Windows A–I scenarios were not executed because build/test green was not established in this environment.

- A — Reference import/persistence/corrupt DOCX rejection: PENDING
- B — Page geometry/margins/header-footer distance: PENDING
- C — Text/heading/authored override/TOC semantics: PENDING
- D — Reference Table 1 Preview/Word agreement: PENDING
- E — Reference Table 2 selection/persistence: PENDING
- F — Grouping diagnosis without Adet: PENDING
- G — Manual stable-ID grouping + Preview rowspan + Word vMerge: PENDING
- H — Caption resolved appearance + real SEQ + manual prefix rule: PENDING
- I — Sprint 14/15 regressions, front matter/altChunk/Qty100/pagination/interaction/edit/Word reopen: PENDING

No manual PASS is inferred from source inspection.

## 17. Remaining unsupported Word-fidelity gaps / blockers

The supported reference-profile target remains structural/property fidelity, not Microsoft Word pixel identity. Unsupported or intentionally out-of-scope areas remain as frozen in Sprint 16: advanced numbering fidelity beyond supported SEQ caption semantics, arbitrary fields, complex floating shapes/anchors, SmartArt, charts, equations, revisions/comments, advanced Word layout inheritance and other unsupported constructs already surfaced by warnings/placeholders.

No DOCX-to-ReportElement reverse engineering, Project Explorer → KAYNAK VERİ integration, PDF implementation, Office COM/Interop, WebView/WebView2, or font-file embedding/distribution was added.

Two external verification blockers remain before Sprint 16 can be called integrated-green:

1. Windows/.NET 8 `dotnet restore`, `dotnet build`, and `dotnet test` actual execution.
2. Mount/copy the exact raw Sero.docx bytes with SHA-256 `8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20` into `tests/KKL.WordStudio.Infrastructure.Tests/TestData/Sero.Reference.docx`, then execute `ExactSeroProfile_ExtractsAuthoritativeSupportedProperties` against the real provider.

Integration source assembly and approved caption/import/consumer fixes are complete, but this package is **not green-certified** until those gates are executed successfully.
