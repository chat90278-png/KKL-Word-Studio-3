# Sero.docx — Reference Format Profile

## 1. Reference and scope

Authoritative reference: `Sero.docx`

SHA-256:

`8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`

The document was inspected as OpenXML and rendered to three pages for visual review. This profile records only the Sprint 16 supported page, paragraph, caption, and table properties. It does **not** treat the reference document as front matter and does not convert its paragraphs/tables into KKL `ReportElement` objects.

All OOXML unit conversions below use:

- `1 point = 20 twips`
- `1 inch = 1440 twips`
- `1 inch = 25.4 mm`
- table percentage width `5000 = 100%`
- border `w:sz` is measured in eighth-points

## 2. Page profile

| Property | OpenXML evidence | Resolved value |
|---|---:|---:|
| Width | `w:pgSz/@w:w = 11906` | 210.009 mm |
| Height | `w:pgSz/@w:h = 16838` | 297.004 mm |
| Top margin | `1417` twips | 24.994 mm |
| Bottom margin | `1417` twips | 24.994 mm |
| Left margin | `1417` twips | 24.994 mm |
| Right margin | `1417` twips | 24.994 mm |
| Header distance | `708` twips | 12.488 mm |
| Footer distance | `708` twips | 12.488 mm |

Contract-facing interpretation: A4 portrait, approximately `210 × 297 mm`, approximately `25 mm` margins on all sides, and approximately `12.49 mm` header/footer distance.

## 3. Base and text profiles

### Normal / body baseline

`Normal` resolves to:

- Arial
- 10 pt
- regular
- black
- left alignment
- 0 pt paragraph after
- 1.0 line-spacing multiple (`w:line=240`, auto)
- no paragraph indent
- no keep-with-next

This is the canonical `BodyText` reference.

The paragraph `UAV Tail Number: x` demonstrates a supported authored override on top of body semantics:

- Arial
- 11 pt
- bold
- centered

Sprint 16 does not need a new element type for this paragraph; a normal `TextElement` can express the supported explicit override.

### Primary heading

Reference sample: first `System Component Configuration List`, paragraph style `Balk4` / heading 4.

Effective supported profile:

- Arial
- 12 pt
- bold (direct run formatting)
- italic (paragraph style)
- underline false
- black (direct run formatting)
- left alignment
- 4 pt before (`80` twips)
- 2 pt after (`40` twips)
- 1.0 line-spacing multiple
- left indent 0 mm
- first-line indent 0 mm
- keep-with-next true

### Secondary heading

Reference sample: second `System Component Configuration List`, paragraph style `Balk5` / heading 5.

Effective supported profile:

- Arial
- 12 pt
- bold
- italic false
- underline false
- black
- left alignment
- 4 pt before
- 2 pt after
- 1.0 line-spacing multiple
- left indent 21.749 mm on the first reference sample (`1233` twips)
- first-line indent 0 mm
- keep-with-next true

A later `Balk5` sample omits the direct left indent. The first heading sample is the reference indent for the Sprint 16 profile; the analyzer may surface the inconsistency as a warning.

## 4. Table-caption profile and sequence

Paragraph style `ResimYazs` / caption resolves through `GvdeMetni` and `Normal` to:

- Arial
- 8 pt
- bold
- italic false
- underline false
- black
- centered
- 0 pt after
- 2.0 line-spacing multiple (`w:line=480`, auto)
- first-line indent 12.488 mm (`708` twips)
- keep-with-next true

The second caption has a direct 12 pt run override. The canonical profile remains the caption style's effective 8 pt value; the direct override difference is a profile warning rather than a second caption contract.

Real table sequence fields are present. Both use:

- `SequenceIdentifier = "Tablo"`
- `Separator = ". "`

The first encountered sequence paragraph uses visible label `Table`; a later paragraph uses `Tablo` with the same sequence identifier. For the single Sprint 16 `TableCaptionSequenceProfile`, use:

- `DisplayLabel = "Table"`
- `SequenceIdentifier = "Tablo"`
- `Separator = ". "`

and emit a warning that the reference contains mixed visible labels for the same sequence identifier. Cached field numbers in the package are not reliable numbering semantics; Word must emit a real `SEQ` field.

## 5. Reference Table 1

Recommended deterministic profile key:

`reference-table-1`

Display name:

`Referans Tablo 1 — Unmanned Aerial Vehicle`

Reference headers:

1. `No`
2. `Product Name`
3. `Product Number`
4. `NSN`
5. `Serial No`
6. `Quantity`

### Table-level format

| Property | Resolved value |
|---|---:|
| WidthPercent | 100.00 |
| FixedLayout | true |
| BorderSizePoints | 0.5 |
| CellMarginTopMillimeters | 0 |
| CellMarginBottomMillimeters | 0 |
| CellMarginLeftMillimeters | 1.235 |
| CellMarginRightMillimeters | 1.235 |
| PreferredRowHeightMillimeters | 10.195 |
| RepeatHeader | true |

Raw table-grid widths and normalized ratios:

| Column | Grid width | Normalized weight % |
|---|---:|---:|
| No | 465 | 5.1313 |
| Product Name | 2722 | 30.0375 |
| Product Number | 1404 | 15.4933 |
| NSN | 1661 | 18.3293 |
| Serial No | 1910 | 21.0770 |
| Quantity | 900 | 9.9316 |

The raw grid widths are suitable `WidthWeight` values because consumers normalize weights by their sum.

### Column format

| Column | Header align | Body align | Header font | Body font | Vertical | NoWrap |
|---|---|---|---|---|---|---|
| No | Center | Center | Arial 9 pt bold | Arial 9 pt regular | Center | true |
| Product Name | Left | Left | Arial 10 pt bold | Arial 10 pt regular | Center | true |
| Product Number | Center | Center | Arial 10 pt bold | Arial 10 pt regular | Center | true |
| NSN | Center | Center | Arial 10 pt bold | Arial 12 pt regular | Center | true |
| Serial No | Center | Center | Arial 10 pt bold | Arial 10 pt regular | Center | true |
| Quantity | Center | Center | Arial 10 pt bold | Arial 10 pt regular | Center | true |

Notes:

- Ordinal body runs are dominantly 9 pt regular; the first `1` run is bold. The dominant regular format is the table-column profile and the first-run anomaly is a warning candidate.
- NSN body paragraphs consistently use `TabloMetni`, whose effective size is 12 pt.
- Serial No body paragraphs are dominantly `Normal` 10 pt with one `TabloMetni` 12 pt exception; the dominant Normal profile is selected.
- All detail cells carry `w:noWrap`; header cells do not. Sprint 16's single per-column `NoWrap` contract follows the body/detail behavior.

## 6. Reference Table 2

Recommended deterministic profile key:

`reference-table-2`

Display name:

`Referans Tablo 2 — Generator & Trailer Configuration List`

Reference headers:

1. `No`
2. `Product Name`
3. `Product No`
4. `NSN`
5. `Serial No`
6. `Quantity`

### Table-level format

| Property | Resolved value |
|---|---:|
| WidthPercent | 99.32 |
| FixedLayout | true |
| BorderSizePoints | 0.5 |
| CellMarginTopMillimeters | 0 |
| CellMarginBottomMillimeters | 0 |
| CellMarginLeftMillimeters | 1.235 |
| CellMarginRightMillimeters | 1.235 |
| PreferredRowHeightMillimeters | 10.195 |
| RepeatHeader | true |

Raw table-grid widths and normalized ratios:

| Column | Grid width | Normalized weight % |
|---|---:|---:|
| No | 469 | 5.2111 |
| Product Name | 2550 | 28.3333 |
| Product No | 1579 | 17.5444 |
| NSN | 1579 | 17.5444 |
| Serial No | 1802 | 20.0222 |
| Quantity | 1021 | 11.3444 |

### Column format

| Column | Header align | Body align | Header font | Body font | Vertical | NoWrap |
|---|---|---|---|---|---|---|
| No | Center | Center | Arial 12 pt bold | Arial 12 pt bold | Center | true |
| Product Name | Left | Left | Arial 12 pt bold | Arial 10 pt regular | Center | true |
| Product No | Center | Center | Arial 12 pt bold | Arial 10 pt regular | Center | true |
| NSN | Center | Center | Arial 12 pt bold | Arial 12 pt regular | Center | true |
| Serial No | Center | Center | Arial 12 pt bold | Arial 10 pt regular | Center | true |
| Quantity | Center | Center | Arial 12 pt bold | Arial 10 pt regular | Center | true |

The table contains real `w:vMerge` restart/continue cells over the two physical serial rows in columns No, Product Name, Product No, NSN, and Quantity. Serial No is not vertically merged. This is evidence that `Sero.docx` visually matches Sprint 15 grouped-table semantics; **it is not analyzer input for grouping-role detection and must not create grouping logic in the reference-format provider**.

## 7. Expected profile warnings

The provider may report concise warnings for these supported-property inconsistencies without failing the profile:

1. The same `SEQ Tablo` sequence uses visible labels `Table` and `Tablo`; the first visible label is selected for the single sequence profile.
2. Caption paragraphs have mixed effective font sizes because the second caption carries a direct 12 pt override over the 8 pt caption style.
3. The first Table 1 ordinal body run is bold while the dominant ordinal body format is regular.
4. Table 1 Serial No body paragraph styling contains one 12 pt `TabloMetni` exception among dominant 10 pt Normal paragraphs.
5. Secondary-heading samples disagree on a direct left indent; the first secondary reference sample carries 21.749 mm.

These warnings belong in `DocumentFormatProfile.Warnings` / `ReportContentDocument.FormatWarnings`. They are non-blocking and must not become `SourceError`.

## 8. Unsupported / intentionally not inferred

The profile does not claim or require:

- DOCX-to-`ReportElement` reverse engineering
- arbitrary shapes or text boxes
- pixel-identical Word rendering
- font-file embedding
- extracting `w:vMerge` as serial/quantity grouping configuration
- using cached SEQ field display numbers as authored plain text

Sprint 16 consumers resolve the supported profile once in Application content and consume the shared resolved format contract in Engine/Preview/Word.
