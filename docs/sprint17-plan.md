# Sprint 17 — True Print Preview + Built-in Default Format + Sparse Trailing AutoRange

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `b273e6a9a43dbd5585c8b4801f2db913421061da`
- Working branch: `sprint17/p0-foundation`
- Windows `dotnet restore/build/test` remains final execution truth.

## Sprint goal

Make generated KKL documents use the supported built-in default formatting profile, preserve imported reference-format override semantics, fix sparse trailing Excel columns in automatic ranges, and move Preview toward a true final-document layer with interaction chrome separated from document metrics.

## P0-A — Sparse trailing AutoRange columns

Acceptance:

- A contiguous nonblank header column immediately after the normal detected end column is included when at least one row in the detected data block contains a real value in that column.
- A trailing header-only column with no data values remains excluded.
- Exact regression sample detects `A3:F13`, not `A3:E13`.
- Existing header/no-header and non-A start detection behavior is preserved.

Status: implemented and included in the successful Windows foundation gate captured before P0-C changes.

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

Status: implemented and included in the successful Windows foundation gate captured before P0-C changes.

## Foundation Windows gate captured before P0-C

- `dotnet restore`: success.
- `dotnet build`: success, 0 warnings, 0 errors.
- Domain tests: 18 passed, 0 failed, 0 skipped.
- Application tests: 170 passed, 0 failed, 0 skipped.
- Engine tests: 55 passed, 0 failed, 0 skipped.
- Architecture tests: 54 passed, 0 failed, 0 skipped.
- Infrastructure tests: 115 passed, 0 failed, 0 skipped.
- `NETSDK1057` remained an informational preview-SDK message and was not a compile failure.

P0-C moved the branch head after this gate. The complete branch must be re-run on Windows before Sprint 17 is declared GREEN.

## P0-C — True Print Preview

Target architecture:

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

- positioned block root is now `PageBlockInteractionHost`; it owns gestures and hit testing, not visual selection borders.
- selection/hover/drop feedback moved to hit-test-free `PageBlockInteractionOverlay` children.
- text editors and table-header editors are hosted in `Canvas` overlays so editor desired size does not alter final document flow.
- table final layer contains only real caption and table visuals.
- empty-caption placeholder, table name/continuation badge, and source-error feedback moved out of the table document-flow `StackPanel`.
- empty-caption double-click editing is preserved by a caption-area-bounded host gesture.
- Preview table header/body borders use the final-document black border visual while thickness remains resolved from `ResolvedTableFormat.BorderSizePoints`.
- source-level architecture guards fail if table designer chrome is reintroduced into the final table layer or interaction feedback is moved back into the geometry-owning host.
- Engine pagination and `PreviewPageProjection` block geometry mapping were not changed.

Status: implemented on the Sprint 17 branch; Windows restore/build/test and visual smoke pending for the new head.

## P0-D — Serial/Quantity manual smoke

Use the exact sample:

| No | Tr İsim | Parça Numarası | NSN | Seri Numarası | Adet |
|---|---|---|---|---|---|
| 1 | elma | 1234 | 45-50-60 | 9999 | 1 |
| 2 | armut | 56789 | 459-485-5 | 9988 | 2 |
| 2 | armut | 56789 | 459-485-5 | 9987 | 2 |

Grouping roles:

- MatchKey -> Parça Numarası
- Serial -> Seri Numarası
- Quantity -> Adet

Expected Preview:

- two physical serial rows for `9988` / `9987`
- true rowspan for non-serial cells
- built-in/imported reference table geometry

Expected Word:

- real `w:vMerge` restart/continue
- serial column unmerged
- full table cell count
- resolved default table properties

## Gates

1. Independent branch source diff review.
2. Windows `dotnet restore`.
3. Windows `dotnet build`.
4. Windows `dotnet test` with no deleted/skipped/weakened tests.
5. AutoRange exact regression smoke.
6. Default-format Preview/Word smoke with no imported reference.
7. Imported reference override smoke.
8. True Print Preview interaction-overlay visual smoke.
9. Serial/Quantity Preview and Word smoke.

Do not claim Sprint 17 GREEN before the exact Windows command output is captured for the current branch head.
