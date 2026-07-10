# Sprint 16 Integration Checklist — Reference DOCX Format Fidelity + Grouping Visibility

## Gate status legend

- `PASS` — reviewed and evidenced on the integrated branch.
- `FAIL` — implementation or evidence does not satisfy the frozen Sprint 16 contract.
- `BLOCKED` — a frozen-contract capability is missing; an approved contract change is required before green integration.
- `PENDING` — not yet executed on Windows/.NET 8 or not yet manually smoke-tested.

Do not convert `BLOCKED`, `FAIL`, or `PENDING` to `PASS` from source inspection alone when the item explicitly requires runtime/UI/Word verification.

---

## 1. Baseline and ownership gate

### Baseline provenance

- [ ] Confirm the integration baseline is the exact Sprint 16 Contract Baseline supplied to all teams.
- [ ] Record source test inventory before merge.
- [ ] Preserve the 308 stabilized Sprint 15 regression tests described by the shared contract.
- [ ] Also account for Contract Bootstrap tests: the supplied Sprint 16 Contract Baseline source inventory is 318 `[Fact]`/`[Theory]` methods with 0 skip attributes.
- [ ] Report integrated total/pass/fail/skip from actual `dotnet test` output.
- [ ] Do not claim the exact supplied source is Windows-green solely from the embedded stabilization report; bootstrap provenance explicitly says final Windows verification remained required.

### Team A ownership review

Allowed only:

- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/**`
- `src/KKL.WordStudio.Infrastructure/Persistence/KwsProjectRepository.cs`
- `src/KKL.WordStudio.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/*Sprint16Reference*`
- Team A report / optional request

Review:

- [ ] No frozen contract/bootstrap files edited.
- [ ] Reference DOCX opened read-only.
- [ ] Reference format stored as project-owned embedded asset distinct from Front Matter.
- [ ] No ReportElement creation from Sero paragraphs/tables.
- [ ] Page/text/caption/table supported properties extracted into `DocumentFormatProfile` only.
- [ ] Sero Table 1 and Table 2 remain separate `ReferenceTableFormatProfile` entries.
- [ ] `w:vMerge` is not interpreted as grouping-role configuration.
- [ ] Provider warnings remain non-blocking format warnings.

### Team B ownership review

Allowed only by the shared contract and bootstrap handoff exception.

Review:

- [ ] No frozen interfaces/contracts/default compatibility services edited.
- [ ] Production DI replaces only the default `IReportContentFormatResolver` registration with the real reference-aware resolver.
- [ ] `Heading -> PrimaryHeading`, `AltHeading -> SecondaryHeading`, ordinary text -> BodyText with supported authored overrides preserved.
- [ ] Reference page geometry/margins resolve into generated `PageLayout`.
- [ ] Table format selection uses stable `ReferenceTableFormatKey`, not header text or reference DOCX table index.
- [ ] Grouping diagnostics explicitly explain missing `Adet`/Quantity.
- [ ] Manual PN/Serial/Adet mapping persists three distinct `TableColumn.Id` GUIDs and sets `WasAutoDetected=false`.
- [ ] Successful apply raises `NotifyReportContentChanged` and rebuilds Preview/Word semantics.
- [ ] Reference-format UI is labeled `Biçim Şablonu`, not `Ön Belge`.
- [ ] Reference format does not create front-matter pages.

### Team C ownership review

Review:

- [ ] Engine consumes `ResolvedTextFormat` / `ResolvedTableFormat`; no DOCX parsing.
- [ ] Text measurement uses font size, paragraph spacing, line-spacing multiple, and indent-adjusted available width.
- [ ] `KeepWithNext` preserves heading behavior.
- [ ] Table measurement uses column `WidthWeight`, header/body sizes, cell margins, and preferred row height.
- [ ] Equal-width assumption is not used when a valid resolved column profile exists.
- [ ] Payload preserves resolved table format.
- [ ] Preview uses resolved page geometry/margins, paragraph typography/alignment/spacing/indent, table width/borders/margins/row-height/column widths/column typography/vertical alignment.
- [ ] Sprint 15 `Grid.RowSpan` rendering remains intact.
- [ ] Preview does not parse the reference DOCX or reconstruct grouping from Sero.

### Team D ownership review

Expected Team D changed production files only:

- `WordParagraphWriter.cs`
- `WordTableWriter.cs`
- `WordContentWriter.cs`
- `WordPageLayoutWriter.cs`

Expected tests/docs only in Team D paths.

Review:

- [ ] Paragraph writer consumes `TextContentNode.Format`.
- [ ] Heading1/Heading2 style IDs remain for TOC outline semantics while direct resolved formatting is applied.
- [ ] Word page writer emits header/footer distances.
- [ ] Word table writer consumes `TableContentNode.Format` and complete semantic `CellSpans`.
- [ ] Real `SEQ` field is emitted from `CaptionSequence`.
- [ ] No Sero title or exact raw Sero width array is hard-coded in Word.
- [ ] No reference asset path / reference DOCX parsing in Word writers.
- [ ] `CONTRACT_CHANGE_REQUEST-D.md` is reviewed before claiming caption appearance fidelity.

---

## 2. Contract-change gate — caption format transport

Team D found a frozen-contract gap.

`DocumentFormatProfile.TableCaption` contains the resolved caption `ResolvedTextFormat`, but `TableContentNode` carries only table `Format` and `CaptionSequence`. Word cannot receive caption font/alignment/spacing/indent/keep-next without a side channel or Sero hard-code.

- [ ] Review `docs/CONTRACT_CHANGE_REQUEST-D.md`.
- [ ] Decision recorded: `APPROVED / REJECTED`.
- [ ] Until approved and implemented, `SeroWord_Caption_WritesCenteredReferenceFormat` must remain a red contract-blocking gate.
- [ ] Do not delete, skip, weaken, or rename the gate to make integration green.

If approved, smallest requested migration:

1. add optional `ResolvedTextFormat? CaptionFormat` to `TableContentNode`;
2. set `CaptionFormat = formatProfile?.TableCaption` in both `ReportContentBuilder` table-node creation paths;
3. Team D consumes `CaptionFormat` when present and preserves legacy caption behavior when null;
4. rerun all tests and manual caption case H.

---

## 3. Cross-branch architecture gate

- [ ] Reference Format and Front Matter remain distinct Domain properties/assets.
- [ ] Application/Engine/UI contain no `WordprocessingDocument` / `DocumentFormat.OpenXml` parsing.
- [ ] `ReportContentBuilder` reads `IReferenceDocumentFormatProvider` and resolves text/table/page formatting through `IReportContentFormatResolver`.
- [ ] Serial/Quantity composer remains before table format resolution.
- [ ] Engine/UI/Word contain no Sero-specific titles.
- [ ] Engine/UI/Word contain no literal exact Sero Table 1/2 width arrays.
- [ ] Word consumes resolved shared content format, not `Project.ReferenceFormat`, `OriginalSourcePath`, or `ResolvedFilePath`.
- [ ] `SerialQuantityGrouping` role identities remain `Guid` `TableColumn.Id` values.
- [ ] No `IDocumentStructureAnalyzer`, `DocumentImportProposal`, or equivalent DOCX-to-ReportElement reverse-engineering model.
- [ ] Project Explorer remains separate from the `KAYNAK VERİ` surface.
- [ ] No Office Interop.
- [ ] No WebView/WebView2.
- [ ] Existing placeholder PDF exporter remains unimplemented; no new PDF library/implementation work.
- [ ] `WordExporter` still consumes `ReportContentDocument`, never `DocumentLayoutResult`.
- [ ] altChunk front-matter composition remains unchanged.
- [ ] explicit Styles/Header/Footer `Save` behavior remains.

---

## 4. Word structural fidelity review

### Paragraphs

- [ ] `RunFonts.Ascii` and `RunFonts.HighAnsi` use resolved `FontFamilyName`.
- [ ] font size converts points to half-points.
- [ ] bold, italic, underline, and foreground color are direct formatting from resolved format.
- [ ] paragraph `Justification` maps Left/Center/Right/Justify.
- [ ] spacing before/after converts points to twentieths of a point.
- [ ] auto line spacing is `round(LineSpacingMultiple * 240)`.
- [ ] left and first-line indent convert millimeters to twips.
- [ ] `KeepNext` is emitted from resolved format.
- [ ] Heading1/Heading2 style identity remains for native TOC semantics.

### Page

- [ ] page width/height emitted from resolved `PageLayout`.
- [ ] portrait/landscape orientation remains consistent with dimensions.
- [ ] top/bottom/left/right margins emitted.
- [ ] `HeaderDistanceMillimeters` emitted to `w:pgMar/@w:header`.
- [ ] `FooterDistanceMillimeters` emitted to `w:pgMar/@w:footer`.

### Caption / sequence

- [ ] `CaptionSequence.SequenceIdentifier` creates a real `SEQ <identifier> \\* ARABIC` Word field.
- [ ] visible prefix comes from `DisplayLabel`.
- [ ] separator comes from `Separator`.
- [ ] descriptive `table.Caption` text remains.
- [ ] cached field display number is not authored as an authoritative plain-number-only caption.
- [ ] deterministic duplicate-number avoidance matches only exact leading `{DisplayLabel} {digits}{Separator}`; digits elsewhere are untouched.
- [ ] caption visual format gate follows section 2 contract-change decision.

### Tables

- [ ] `WidthPercent` converts to Word pct units (`100% = 5000`).
- [ ] `FixedLayout` controls `TableLayout`.
- [ ] border points convert to eighth-points (`0.5 pt = 4`).
- [ ] default table cell margins convert mm -> twips.
- [ ] `TableGrid` widths are proportional to valid resolved `WidthWeight` values.
- [ ] per-cell preferred widths are proportional to the same weights.
- [ ] equal-width fallback is used only when the resolved column profile is invalid/incompatible.
- [ ] preferred row height is emitted with `AtLeast` behavior.
- [ ] repeated header follows `ResolvedTableFormat.RepeatHeader`.
- [ ] header/body alignment and font family/size/bold use per-column format.
- [ ] `TableCellVerticalAlignment` uses per-column format.
- [ ] `NoWrap` follows the resolved column profile.
- [ ] every physical data row retains a full `TableCell` count.
- [ ] complete semantic `CellSpans` produce `w:vMerge restart/continue`.
- [ ] semantic row indexes exclude the physical Word header-row offset.
- [ ] Serial column remains unmerged when no semantic span exists there.
- [ ] no Word page-boundary/span projection guesses were added.

---

## 5. Exact manual Windows scenarios

### A. Import Sero.docx as Biçim Şablonu

1. Open a project with no reference format.
2. Use `Biçim Şablonu Ekle` and select `Sero.docx`.
3. Save/reopen the `.kws` project.
4. Confirm the reference format filename/status remains visible.
5. Confirm **no front-matter pages are added** to Preview.
6. Confirm existing Front Matter, when separately configured, remains independent.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

### B. Page

1. Build a generated report using the Sero reference format.
2. Inspect Preview page geometry.
3. Export Word and inspect Page Setup / section XML if necessary.
4. Verify A4 portrait around `210 × 297 mm`.
5. Verify top/bottom/left/right margins around `25 mm`.
6. Verify Word header/footer distance around `12.49 mm`.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

### C. Text

1. Add Heading content.
2. Add Alt Heading content.
3. Add ordinary text with explicit centered, bold, 11 pt authored override.
4. Rebuild Preview and export Word.
5. Verify Heading resolves to Arial 12 pt, bold + italic, 4 pt before / 2 pt after, keep-next.
6. Verify Alt Heading resolves to Arial 12 pt bold and reference left indent where the selected profile supplies it.
7. Verify centered 11 pt bold ordinary text is representable and remains centered/bold/11 pt.
8. Verify native Heading style identity remains sufficient for Word TOC semantics.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

### D. Table 1 style

1. Create/select a six-column table matching the Sero Table 1 profile shape.
2. Select `Referans Tablo 1 — Unmanned Aerial Vehicle`.
3. Rebuild Preview and export Word.
4. Verify full width and fixed layout.
5. Verify visibly unequal widths.
6. Verify Product Name body left aligned.
7. Verify No/Product Number/NSN/Serial/Quantity body centered.
8. Verify 0.5 pt borders.
9. Verify approximately 1.235 mm left/right cell margins.
10. Verify approximately 10.195 mm preferred row height/minimum behavior.
11. Verify repeated table header.
12. Verify per-column Arial header/body sizes and vertical center behavior.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

### E. Table 2 style selection

1. On another compatible table, select `Referans Tablo 2 — Generator & Trailer Configuration List`.
2. Confirm the selection persists through save/reopen.
3. Verify Table 2 remains a **separate** profile and uses its own unequal ratios / 99.32% width behavior.
4. Switch between Table 1 and Table 2 and verify the selected key changes deterministically without header-text identity persistence.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

### F. Grouping diagnostics

Use the current five-column shape:

- No
- Tr İsim
- Parça Numarası
- NSN
- Seri Numarası

Do **not** add Adet yet.

1. Transfer the table.
2. Open table Properties.
3. Verify the grouping section does not silently imply a merge should happen.
4. Verify explicit Turkish diagnosis includes `Adet sütunu bulunamadı` or the frozen equivalent:
   `Seri no/adet düzeni yapılandırılmadı — Adet sütunu bulunamadı.`
5. Verify Preview/Word do not fabricate quantity-driven grouped rows.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

### G. Manual grouping mapping

1. Add an Adet/Quantity column.
2. Open `SERİ NO / ADET DÜZENİ`.
3. Choose:
   - Eşleşme alanı = PN / Parça Numarası
   - Seri no alanı = Seri Numarası
   - Adet alanı = Adet
4. Apply.
5. Verify the three selections must be distinct columns.
6. Verify persisted identities are the stable `TableColumn.Id` GUIDs and `WasAutoDetected=false`.
7. Use same PN with exactly two distinct serials and Qty 2.
8. Verify Preview true rowspan grouping.
9. Export Word and verify true `w:vMerge restart/continue` for safe non-serial columns.
10. Verify Serial cells remain separate.
11. Rename the displayed PN header and verify grouping continues through stable IDs.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

### H. Caption

1. Configure a descriptive table caption such as `Unmanned Aerial Vehicle`.
2. Use the Sero reference format whose sequence profile is:
   - DisplayLabel `Table`
   - SequenceIdentifier `Tablo`
   - Separator `. `
3. Export Word.
4. Inspect the caption field and verify a real `SEQ Tablo \\* ARABIC` field exists.
5. Update fields in Word and verify numbering is field-driven.
6. Verify descriptive caption text remains after the sequence field.
7. Also test a manually authored `Table 7. Description` caption; verify the deterministic leading manual number is not duplicated as `Table <SEQ>. Table 7. ...`.
8. After `CONTRACT_CHANGE_REQUEST-D.md` is approved/implemented, verify Arial 8 pt, bold, centered, 2.0 line spacing, 12.488 mm first-line indent, and keep-next from resolved caption format.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] BLOCKED — caption resolved-format transport not approved/implemented
- [ ] PENDING

### I. Regression

1. Run the complete solution test suite on Windows/.NET 8.
2. Confirm all 308 stabilized Sprint 15 baseline tests remain present.
3. Confirm Contract Bootstrap and Sprint 16 tests are included in the integrated total.
4. Verify existing Front Matter native preview still works.
5. Verify final Word altChunk composition still works.
6. Re-run Sprint 15 Qty 100 / no serial and Qty 100 / one serial cases: one row only, no explosion.
7. Verify exact serial/quantity grouping still preserves Preview `Grid.RowSpan` and Word `vMerge`.
8. Verify long A4 pagination still works.
9. Verify Preview selection across split fragments still selects the same ElementId.
10. Verify Preview drag/delete still mutate real report structure through the structure service.
11. Verify split-text semantic edit reconstruction still works.

Acceptance:

- [ ] PASS
- [ ] FAIL
- [ ] PENDING

---

## 6. Required Windows verification commands

Run from solution root on Windows with the final .NET 8/WPF toolchain:

```text
dotnet restore
dotnet build
dotnet test
```

Record actual output only.

### Acceptance record

| Gate | Result | Evidence / notes |
|---|---|---|
| Team A ownership | PASS / FAIL / PENDING | |
| Team B ownership | PASS / FAIL / PENDING | |
| Team C ownership | PASS / FAIL / PENDING | |
| Team D ownership | PASS / FAIL / PENDING | |
| Frozen contract drift | PASS / FAIL / BLOCKED | Team D caption-format request decision required |
| Baseline 308 regression preserved | PASS / FAIL / PENDING | |
| Contract-baseline 318 source tests accounted for | PASS / FAIL / PENDING | |
| Integrated total tests | value | |
| Skipped tests | value | |
| `dotnet restore` | PASS / FAIL | |
| `dotnet build` warnings | value | |
| `dotnet build` errors | value | |
| `dotnet test` pass | value | |
| `dotnet test` fail | value | |
| `dotnet test` skip | value | |
| Manual A | PASS / FAIL / PENDING | |
| Manual B | PASS / FAIL / PENDING | |
| Manual C | PASS / FAIL / PENDING | |
| Manual D | PASS / FAIL / PENDING | |
| Manual E | PASS / FAIL / PENDING | |
| Manual F | PASS / FAIL / PENDING | |
| Manual G | PASS / FAIL / PENDING | |
| Manual H | PASS / FAIL / BLOCKED / PENDING | |
| Manual I | PASS / FAIL / PENDING | |

## Integration decision rule

Do not mark Sprint 16 integrated-green until:

1. Windows `restore/build/test` actual output is recorded;
2. no baseline regression/test weakening is present;
3. manual A–I are executed;
4. caption format transport is either approved and implemented with the named fidelity test green, or the Sprint is explicitly accepted with that fidelity capability deferred and the red gate/request retained honestly.
