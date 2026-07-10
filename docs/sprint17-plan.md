# Sprint 17 — True Print Preview + Built-in Default Format + Sparse Trailing AutoRange

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `b273e6a9a43dbd5585c8b4801f2db913421061da`
- Working branch: `sprint17/p0-foundation`
- Windows `dotnet restore/build/test` remains final execution truth.

## Sprint goal

Make generated KKL documents use the supported built-in default formatting profile, preserve imported reference-format override semantics, fix sparse trailing Excel columns in automatic ranges, separate Preview interaction chrome from final-document metrics, and produce true serial-backed grouped rows from the real project data shape.

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

## Captured Windows gates before P0-D semantic correction

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

The old exact-equality rule resolves the nonblank quantity as `1`, observes two serials, and aggregates them into one cell. That creates `9987\n9988` instead of true grouped rows.

### Accepted quantity source shapes for safe physical serial rows

For a safe same-match-key group with 2+ distinct serials already represented by 2+ physical source rows:

1. Per-group seed quantity plus sparse continuation:

```text
9987 | 1
9988 | blank
```

2. Per-serial unit quantity:

```text
9987 | 1
9988 | 1
```

3. Explicit group total:

```text
9987 | 2
9988 | blank
```

The three shapes above must produce the same semantic result for two distinct serials:

```text
2 | armut | 56789 | 459-485-5 | 9987 | 2
  |       |       |           | 9988 |
```

Rules:

- each distinct serial remains one physical semantic row.
- Serial column is never vertically merged for this grouping.
- every non-serial column receives a vertical `TableCellSpan` across the serial rows.
- canonical `Adet` becomes the distinct serial count when the only explicit quantity value is unit quantity `1` and the serials were observed on multiple physical source rows.
- repeated unit quantity `1` values are treated as per-serial units and therefore total to the distinct physical serial count.
- an already-correct explicit total equal to serial count keeps the existing exact grouping path.
- `Adet 3 + two serials` remains a mismatch: aggregate serial text plus warning, no spans.
- duplicate serials remain unsafe for grouped layout.
- multiple serial tokens packed into one source cell with `Adet 1` do not masquerade as multiple physical source rows.
- conflicting quantity values such as `1` and `2` remain unsafe and preserve original rows.
- conflicting non-serial product data remains unsafe and preserves original rows.

Grouping roles remain stable `TableColumn.Id` identities:

- MatchKey -> Parça Numarası
- Serial -> Seri Numarası
- Quantity -> Adet

Expected Preview:

- `9987` and `9988` are two physical rows.
- No / Tr İsim / Parça Numarası / NSN / Adet are true WPF rowspans.
- merged `Adet` displays `2`.
- serial rows retain their individual row geometry/borders.
- built-in/imported table geometry remains resolved through the shared format path.

Expected Word:

- two data `TableRow` elements for the armut serial group.
- full `TableCell` count remains present in each row.
- No / Tr İsim / Parça Numarası / NSN / Adet emit real `w:vMerge restart/continue`.
- Serial column emits no vertical merge.
- serial values remain in separate cells as `9987` and `9988`.
- merged quantity anchor text is `2`.

Status: Application composition correction and focused source-shape regressions implemented; Engine payload and Word integration regression verification next.

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
