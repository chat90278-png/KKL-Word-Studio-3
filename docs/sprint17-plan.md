# Sprint 17 — True Print Preview + Built-in Default Format + Sparse AutoRange + Serial-backed Grouped Layout

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `b273e6a9a43dbd5585c8b4801f2db913421061da`
- Working branch: `sprint17/p0-foundation`
- Windows `dotnet restore/build/test` remains final execution truth.

## Sprint goal

Make generated KKL documents use the supported built-in default formatting profile, preserve imported reference-format override semantics, fix sparse trailing Excel columns in automatic ranges, separate Preview interaction chrome from final-document metrics, and produce true serial-backed grouped rows from the real WorkingData source shape.

## P0-A — Sparse trailing AutoRange columns

Acceptance:

- A contiguous nonblank header column immediately after the normal detected end column is included when at least one row in the detected data block contains a real value in that column.
- A trailing header-only column with no data values remains excluded.
- Exact regression sample detects `A3:F13`, not `A3:E13`.
- Existing header/no-header and non-A start detection behavior is preserved.

Status: implemented and included in a successful Windows gate.

## P0-B — Built-in default generated-document format

Precedence:

1. Usable `Project.ReferenceFormat` -> imported reference profile.
2. No project reference -> built-in default profile.
3. Configured reference missing/unreadable -> built-in default profile plus format warning.

Built-in profile source:

- `DefaultDocumentFormatProfileFactory`
- Application-format contracts only.
- No document-specific constants in Engine, Preview, Rendering, or Word writers.

Supported built-in baseline:

- A4 portrait geometry and approximately 25 mm margins.
- Arial 10 pt body.
- 12 pt bold/italic primary heading with keep-with-next and reference spacing.
- 12 pt bold secondary heading with reference indent.
- 8 pt bold centered table caption, keep-with-next, 2.0 line spacing.
- `Tablo` caption sequence.
- Two six-column default table profiles with fixed layout, unequal widths, 0.5 pt borders, 1.235 mm horizontal cell margins, 10.195 mm preferred row height, repeated header, and vertical centering.

Status: implemented and included in a successful Windows gate.

## Captured Windows gates before P0-D

Foundation gate:

- `dotnet restore`: success.
- `dotnet build`: success, 0 warnings, 0 errors.
- Domain tests: 18 passed, 0 failed, 0 skipped.
- Application tests: 170 passed, 0 failed, 0 skipped.
- Engine tests: 55 passed, 0 failed, 0 skipped.
- Architecture tests: 54 passed, 0 failed, 0 skipped.
- Infrastructure tests: 115 passed, 0 failed, 0 skipped.
- `NETSDK1057` remained informational preview-SDK output and was not a compile failure.

The user subsequently confirmed the P0-C head `86412321b31337628bec8c9e5155ab87fa9d5824` GREEN on Windows. P0-D changes move the branch head again, so the current head must receive a new Windows gate before Sprint 17 is declared GREEN.

## P0-C — True Print Preview

### Final document layer

Owns only final output geometry and visible document content:

- page geometry and margins
- resolved text formatting
- paragraph spacing/indent/alignment
- table widths and unequal column ratios
- borders and cell margins
- row heights
- captions
- row spans

### Transparent interaction adorner layer

Owns designer-only affordances:

- selection
- hover
- drag/drop
- edit affordances
- empty-caption placeholder

Rules:

- Designer chrome must not change document measurement or pagination.
- Empty caption placeholder consumes zero document-flow height.
- Selection border/tint must not change cell geometry or final background geometry.
- Word export never receives placeholder content.
- Preview and Word continue to consume the same resolved semantic format path.

Implemented:

- positioned block root is `PageBlockInteractionHost`; it owns gestures and hit testing, not visual selection borders.
- selection/hover/drop feedback is rendered by hit-test-free `PageBlockInteractionOverlay` children.
- text editors and table-header editors are hosted in `Canvas` overlays so editor desired size does not alter final document flow.
- table final layer contains real caption and table visuals; block-level designer chrome is outside document flow.
- empty-caption placeholder, table name/continuation badge, and source-error feedback are outside the table document-flow `StackPanel`.
- empty-caption double-click editing uses a caption-area-bounded host gesture.
- Preview table header/body borders use the final-document black border visual while thickness remains resolved from `ResolvedTableFormat.BorderSizePoints`.
- architecture guards protect layer separation and zero-measure interaction overlays.
- Engine pagination and `PreviewPageProjection` block geometry mapping were not changed.

Status: implemented, source-reviewed, and user-confirmed GREEN on Windows at P0-C head `86412321b31337628bec8c9e5155ab87fa9d5824`.

## P0-D — Serial-backed grouped layout correction

### Observed real-data defect

The real WorkingData shape can contain one physical source row per serial while the `Adet` field is sparse or per-row:

| No | Tr İsim | Parça Numarası | NSN | Seri Numarası | Adet |
|---|---|---|---|---|---|
| 1 | elma | 1234 | 45-50-60 | 9999 | 1 |
| 2 | armut | 56789 | 459-485-5 | 9987 | 1 |
|   | armut | 56789 |   | 9988 |   |

The previous exact-equality-only rule resolved the nonblank quantity as `1`, observed two serials, and aggregated them into one serial cell. That produced `9987\n9988` instead of true grouped rows.

### Accepted quantity source shapes

For a safe same-match-key group with 2+ distinct serials represented by 2+ physical source rows, all of these are supported:

Per-group seed quantity plus sparse continuation:

```text
9987 | 1
9988 | blank
```

Per-serial unit quantity:

```text
9987 | 1
9988 | 1
```

Explicit group total:

```text
9987 | 2
9988 | blank
```

The three shapes produce the same semantic result:

```text
2 | armut | 56789 | 459-485-5 | 9987 | 2
  |       |       |           | 9988 |
```

### P0-D composition rules

- Each distinct serial remains one physical semantic row.
- Serial column is never vertically merged for this grouping.
- Every non-serial column receives a vertical `TableCellSpan` across the serial rows.
- When the only resolved explicit quantity is unit quantity `1`, there are 2+ serial-bearing physical source rows, every serial-bearing source row carries exactly one serial token, and serials are distinct, canonical `Adet` is inferred from distinct serial count.
- Repeated unit quantity `1` values are treated as per-serial units and total to the distinct physical serial count.
- An already-correct explicit total equal to serial count keeps the existing exact grouping path.
- `Adet 3 + two serials` remains a mismatch: aggregate serial text plus warning, no spans.
- Duplicate serials remain unsafe for grouped layout.
- Packed serial cells such as `9987\n9988` do not masquerade as multiple physical source rows for unit-quantity inference.
- Mixed packed + physical serial cells do not trigger unit-quantity inference.
- Conflicting quantity values such as `1` and `2` remain unsafe and preserve original rows.
- Conflicting non-serial product data remains unsafe and preserves original rows.

Grouping roles remain stable `TableColumn.Id` identities:

- MatchKey -> Parça Numarası
- Serial -> Seri Numarası
- Quantity -> Adet

### Shared semantic path

```text
Excel / WorkingData normalized rows
                ↓
ReportContentBuilder
                ↓
ITableContentRowComposer
                ↓
TableContentNode.Rows / CellSpans / RowGroups
                ↓
       ┌────────┴────────┐
     Engine             Word
       ↓                  ↓
fragment spans      complete spans
       ↓                  ↓
Preview Grid.RowSpan   w:vMerge
```

Production DI remains:

`ITableContentRowComposer -> SerialQuantityTableContentRowComposer`

Excel transfer continues to auto-detect Turkish Product/Serial/Quantity roles and persist stable `TableColumn.Id` grouping identities.

### Regression coverage added

Application source-shape behavior:

- `1 + blank` physical serial rows -> inferred total and true grouping.
- `1 + 1` physical serial rows -> total `2` and true grouping.
- explicit total `2 + blank` -> existing exact grouping.
- explicit total `3` with two serials -> mismatch remains.
- multiple serial tokens in one cell with `Adet 1` -> no false physical-row inference.
- packed + physical serial cell mix -> no false physical-row inference.

Live builder pipeline:

- real `ReportContentBuilder` + real composer transports the exact WorkingData shape into `TableContentNode` with separate serial rows, `Adet=2`, five non-serial spans, and one two-row group.

Engine / Preview payload pipeline:

- real composer result enters `DeterministicDocumentLayoutEngine`.
- `TablePageBlockPayload.Rows` retains `9999`, `9987`, `9988` as separate serial rows.
- armut group payload contains five fragment-local non-serial spans at the expected row index.
- Serial column has no span.

Word pipeline:

- real composer result enters real `WordTableWriter`.
- two armut data rows retain full six-cell shape.
- columns No / Tr İsim / Parça Numarası / NSN / Adet emit `w:vMerge restart/continue`.
- Serial column emits no vertical merge.
- `9987` and `9988` remain separate serial cells.
- merged quantity anchor text is `2`.

Status: production correction implemented and source-reviewed. Application, ReportContentBuilder, Engine payload, and Word OpenXML regression coverage is present. Current-head Windows `restore/build/test` and the real Preview/Word visual smoke remain pending.

## Gates

1. Independent branch source diff review.
2. Windows `dotnet restore`.
3. Windows `dotnet build`.
4. Windows `dotnet test` with no deleted/skipped/weakened tests.
5. AutoRange exact regression smoke.
6. Built-in default-format Preview/Word smoke with no imported reference.
7. Imported reference override smoke.
8. True Print Preview interaction-overlay visual smoke.
9. P0-D real WorkingData sample produces two physical serial rows in shared semantic composition.
10. P0-D Engine fragment payload contains true non-serial spans and separate serial rows.
11. P0-D Preview renders true WPF rowspan with serial values on separate rows.
12. P0-D Word emits real `w:vMerge` restart/continue and keeps serial cells unmerged.

Do not claim Sprint 17 GREEN before the exact Windows command output is captured for the current branch head and the real Preview/Word smoke matches the P0-D expected structure.
