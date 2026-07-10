# Sprint 17 — True Print Preview + Built-in Default Format + Sparse AutoRange + Grouped Serial Layout + Automatic Table Captions

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `b273e6a9a43dbd5585c8b4801f2db913421061da`
- Working branch: `sprint17/p0-foundation`
- Windows `dotnet restore/build/test` output remains final execution truth.
- Do not mark the current branch GREEN from source review alone.

## Sprint goal

Stabilize the generated KKL document path so automatic Excel ranges retain sparse trailing data, generated documents have a deterministic built-in format, Preview behaves like a true print preview, serial-backed products produce real grouped rows, and table captions are visible and automatically numbered in Preview and Word without persisting stale numbering in the report model.

## P0-A — Sparse trailing AutoRange

Acceptance:

- Preserve the existing occupancy heuristic.
- Extend the detected end column only across contiguous nonblank header columns that contain at least one real value in the detected data block.
- Exact sparse `Adet` sample detects `A3:F13`, not `A3:E13`.
- A trailing header-only column remains excluded.

Status: implemented and included in a successful Windows gate.

## P0-B — Built-in default generated-document format

Format precedence:

1. Usable `Project.ReferenceFormat` -> imported analyzed profile.
2. No configured project reference -> `OpenXmlReferenceDocumentFormatProvider` returns the complete built-in default profile.
3. Configured reference missing/unreadable -> preserve the frozen Sprint 16 missing-state contract (`Profile = null`, `IsMissing = true`) plus warning. The reference-aware resolver still supplies built-in text/table/page fallback; complete caption-metadata fallback for this missing-reference state is a separate contract decision and must not be hidden inside P0-F.

Built-in baseline:

- A4 portrait, approximately 25 mm margins.
- Arial 10 pt body.
- 12 pt bold/italic primary heading with keep-with-next and reference spacing.
- 12 pt bold secondary heading with reference indent.
- Arial 8 pt bold centered table caption, black foreground, keep-with-next, 2.0 line spacing.
- Built-in table caption display label / sequence identifier `Tablo`.
- Built-in caption separator `: `.
- Two six-column table profiles with fixed layout, unequal column ratios, 0.5 pt borders, 1.235 mm horizontal cell margins, 10.195 mm preferred row height, repeated header, and vertical centering.

Status: implemented for normal no-reference generated-document flow. Earlier built-in format work passed a Windows gate before later P0-D/P0-E/P0-F commits moved the branch head.

## Captured Windows gate before current-head work

Captured foundation gate:

- `dotnet restore`: success.
- `dotnet build`: success, 0 warnings, 0 errors.
- Domain: 18 passed / 0 failed / 0 skipped.
- Application: 170 passed / 0 failed / 0 skipped.
- Engine: 55 passed / 0 failed / 0 skipped.
- Architecture: 54 passed / 0 failed / 0 skipped.
- Infrastructure: 115 passed / 0 failed / 0 skipped.
- `NETSDK1057` was informational preview-SDK output, not a compile failure.

The user also confirmed P0-C head `86412321b31337628bec8c9e5155ab87fa9d5824` GREEN on Windows. Later P0-D/P0-E/P0-F and caption-pipeline hotfix commits require a new exact-head gate.

## P0-C — True Print Preview

Final document layer owns only final document content and resolved geometry:

- page geometry and margins;
- resolved text formatting;
- paragraph spacing/indent/alignment;
- table widths and unequal column ratios;
- borders and cell margins;
- row heights;
- real captions;
- row spans.

Interaction layer owns designer-only affordances:

- selection;
- hover;
- drag/drop;
- inline editors;
- empty-caption affordance;
- table designer badge;
- source-error feedback.

Implemented rules:

- `PageBlockInteractionHost` owns hit testing and gestures, not selection border/tint geometry.
- Selection/hover/drop feedback is rendered by hit-test-free overlays.
- Text/header editors are Canvas overlays and do not participate in final document flow measurement.
- Table final layer contains real caption/table content only.
- Designer chrome remains outside table document flow.
- Preview table header/body borders use final-document black border visuals while thickness remains resolved from `ResolvedTableFormat.BorderSizePoints`.
- Engine pagination and `PreviewPageProjection` X/Y/Width/Height mapping remain authoritative.

Status: implemented, source-reviewed, and user-confirmed GREEN at the P0-C head.

## P0-D — Serial-backed grouped layout correction

Observed real WorkingData can contain one physical row per serial while `Adet` is sparse or per-row.

Supported safe two-serial source shapes:

```text
9987 | Adet 1
9988 | Adet blank
```

```text
9987 | Adet 1
9988 | Adet 1
```

```text
9987 | Adet 2
9988 | Adet blank
```

All resolve to the same semantic grouped structure:

```text
2 | armut | 56789 | 459-485-5 | 9987 | 2
  |       |       |           | 9988 |
```

Composition rules:

- Every distinct serial remains a separate physical semantic row.
- Serial column is never vertically merged for the grouping.
- Non-serial columns receive vertical `TableCellSpan`s.
- Unit quantity represented per physical serial row may infer canonical total from distinct physical serial count.
- Exact explicit total equal to serial count keeps the existing exact grouping path.
- `Adet 3 + two serials` remains mismatch/warning/no spans.
- Duplicate serials remain unsafe.
- Packed serial cells do not masquerade as physical serial rows.
- Mixed packed/physical serial cells remain conservative.
- Conflicting quantity or product data preserves original rows.

Shared semantic path:

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
Preview RowSpan       w:vMerge
```

Regression coverage includes Application source shapes, real `ReportContentBuilder`, Engine table payload, and Word OpenXML `w:vMerge` output.

Real project visual smoke passed:

- Preview shows `9988` and `9987` on separate physical rows.
- No / Tr İsim / Parça Numarası / NSN / Adet render as one merged group.
- Merged `Adet` displays `2`.
- Generated Word output shows the same grouped structure with separate Serial cells.

Status: implemented and real Preview/Word grouped-layout visual smoke passed. Current-head Windows gate remains required.

## P0-E — Empty-caption interaction hint placement

Observed defect:

- the old interaction-only text `Tablo başlığı eklemek için çift tıklayın` was outside document flow but top-aligned at the table origin and covered the first header row.

Implemented correction:

- Remove the long top-aligned placeholder `TextBlock`.
- Use `EmptyCaptionHintPopup`, a designer-only WPF `Popup` containing compact `+ Tablo başlığı` chrome.
- Popup is closed by default.
- Popup opens only for editable empty-caption tables while selected or while `TableBlockHost` is hovered.
- `Placement="Top"` with small offsets keeps the affordance outside document measurement and above the header.
- Popup explicitly binds `PlacementTarget` and `DataContext` to `TableBlockHost`.
- The final document table layer remains clipped.
- Double-click routes through the existing caption-edit handler.
- The existing bounded host double-click caption gesture remains available.
- Engine geometry, row spans, caption semantics, and Word production output are untouched.

Architecture guards prevent the old long placeholder and table-template clip escape from returning.

Status: implemented and source-reviewed. Current-head Windows and visual smoke remain required.

## P0-F — Automatic numbered table captions and Preview visibility

### Product behavior

The user edits only the descriptive caption text:

```text
İnsansız Hava Aracı
```

The generated document displays automatic document-order numbering:

```text
Tablo 1: İnsansız Hava Aracı
Tablo 2: İkinci Başlık
Tablo 3: Üçüncü Başlık
```

The automatic prefix is derived metadata and is never persisted into `TableElement.Caption`.

### Built-in caption visual contract

- Arial 8 pt.
- Bold.
- Centered.
- Black foreground.
- Keep-with-next.
- 2.0 line spacing.
- Display label `Tablo`.
- Sequence identifier `Tablo`.
- Separator `: `.

Imported reference-format profiles continue to supply their own extracted caption format, display label, sequence identifier, and separator. Built-in `: ` is not hard-coded into Engine, Preview, or Word writers.

### Shared sequence rules

`TableCaptionSequenceFormatter` owns deterministic sequence presentation and document-order counter semantics:

- Counter key is `SequenceIdentifier`.
- Only nonblank captioned tables consume a number.
- Blank captions do not consume a number.
- Numbering begins at 1 for the generated body sequence.
- Table fragments reuse one semantic table number and do not increment the counter.
- Exact authored manual prefixes such as `Tablo 7: ...` are removed before the automatic prefix is displayed/written.
- Unrelated digits or differently shaped text are never interpreted as sequence prefixes.

### Engine / Preview transport

`TablePageBlockPayload` receives default-compatible optional metadata:

- `CaptionSequence`;
- `CaptionSequenceNumber`.

`GeneratedDocumentPaginator` derives the body table sequence number once in semantic document order before table pagination. `DeterministicTablePaginator` transports the same metadata to every fragment and measures the visible numbered caption text for actual fragment geometry.

Preview:

- keeps raw authored `Caption` for inline editing;
- builds visible `CaptionRuns` through `TableCaptionSequenceFormatter.BuildDisplayText`;
- therefore the editor sees only the description while the document layer shows `Tablo n: description`;
- missing or invalid caption foreground falls back to black.

### Word

Word keeps real OpenXML sequence fields:

```text
SEQ Tablo \* ARABIC
```

The generated body writer derives the same document-order number and writes the correct cached field result (`1`, `2`, `3`, ...). The document should initially open with the correct visible numbering while the caption remains a real updatable Word SEQ field.

### Failed real visual smoke — 2026-07-10

The user supplied real Preview and generated Word screenshots after the initial P0-F implementation.

Observed in both outputs:

```text
deneme başlık
```

Expected:

```text
Tablo 1: deneme başlık
```

The caption was also left-aligned instead of the built-in centered caption format. The small `Tablo 1` text visible at the Preview table's top-right was designer badge/chrome, not the real document caption.

Root cause:

- `OpenXmlReferenceDocumentFormatProvider` returned `Profile = null` when `Project.ReferenceFormat` was not configured.
- `ReferenceReportContentFormatResolver` correctly synthesized a built-in default for text/table/page resolution.
- `ReportContentBuilder` transports caption metadata directly from `formatProfile?.TableCaption` and `formatProfile?.TableCaptionSequence` rather than through those resolver methods.
- Therefore live no-reference tables received default table geometry but `CaptionFormat = null` and `CaptionSequence = null`.
- Leaf sequence/Preview/Word tests passed because they constructed complete caption metadata directly and did not exercise the real no-reference provider -> builder integration path.

### Live no-reference caption pipeline hotfix

Implemented narrow correction:

- When no project reference is configured, `OpenXmlReferenceDocumentFormatProvider` now returns `DefaultDocumentFormatProfileFactory.Create()` as the complete profile.
- Imported usable reference behavior is unchanged.
- Configured missing/unreadable reference still preserves the frozen Sprint 16 null-profile missing-state contract.
- `ReportContentBuilder` is unchanged; its existing direct caption metadata transport now receives a complete built-in profile in the normal no-reference product path.
- Production Infrastructure DI already replaces `IReferenceDocumentFormatProvider` with `OpenXmlReferenceDocumentFormatProvider`, so the hotfix applies to live Preview and Word export composition.

Added regressions:

- no-reference provider must return a complete built-in profile including Arial 8 / centered / black caption format and `Tablo` / `: ` sequence metadata;
- real `OpenXmlReferenceDocumentFormatProvider` + real `ReportContentBuilder` + production reference-aware resolver must transport that caption format and sequence into `TableContentNode` while preserving raw authored caption text;
- existing Sprint 16 missing-reference `Profile = null` / `IsMissing = true` contract remains covered and unchanged.

P0-F status: initial source/leaf implementation failed real visual smoke. Root cause identified from the user screenshots and a live no-reference provider/builder pipeline hotfix has been applied. Current exact-head Windows gate and repeat Preview/Word visual smoke are pending. Do not call P0-F visually passed yet.

## Remaining Sprint 17 closure gates

1. Pull the current exact branch head and run on Windows:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - no deleted/skipped/weakened tests.
2. Empty caption smoke:
   - no long header overlap;
   - compact `+ Tablo başlığı` Popup appears on hover/selection;
   - double-click opens caption editing.
3. Repeat the exact failed automatic-caption smoke with no project reference:
   - authored `deneme başlık` displays `Tablo 1: deneme başlık` in Preview;
   - caption is visible black, Arial 8 and centered;
   - the top-right designer badge is not mistaken for the real caption;
   - double-click editor contains raw `deneme başlık`, not `Tablo 1:`.
4. Add/verify a second captioned table:
   - Preview displays `Tablo 2: <description>`.
5. Word caption smoke:
   - first/second generated captions initially display 1/2;
   - captions remain real `SEQ` fields;
   - built-in separator is `: `;
   - caption typography/alignment follows resolved caption format.
6. Re-run imported reference-format override smoke and confirm imported sequence separator/profile remains authoritative.
7. Confirm grouped serial Preview/Word output remains unchanged: separate Serial rows, non-serial merges, `Adet=2`.
8. Record exact final Windows evidence and visual-smoke result in the PR.
9. Mark the Sprint 17 PR ready only after the exact current head is GREEN and the repeated P0-F screenshots show the expected real caption.

## Post-Sprint-17 next work

After Sprint 17 closes, the next tranche should focus on product-level document authoring and long-document fidelity:

- caption/title editing polish and explicit empty/non-empty states;
- decide and implement complete caption-metadata fallback semantics for configured missing/unreadable reference documents without silently changing the frozen Sprint 16 missing-state contract;
- table-level format selection UX for built-in and imported table profiles;
- richer report structure/property editing without leaking designer chrome into document metrics;
- long-caption and heading+table keep-with-next page-boundary QA;
- multi-page table pagination/continuation QA with repeated headers and row groups near page boundaries;
- broader generated-DOCX fidelity fixtures for headings, captions, tables, margins, fields, and continuation behavior.

Do not claim Sprint 17 GREEN before the exact current-head Windows command output and repeated P0-E/P0-F visual smokes are complete.
