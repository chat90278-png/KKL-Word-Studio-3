# Sprint 18 — Document Authoring and Long-Document Fidelity

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `a60fa77d1487b4db041d694b4c26056d6ff1c3b9`
- Working branch: `sprint18/document-authoring-fidelity`
- Windows `dotnet restore/build/test` output is execution truth.
- Do not mark the current branch GREEN from source review alone.
- Preserve Sprint 17 grouped Serial/Quantity semantics, automatic caption numbering, true print-preview layering, and built-in/default-format precedence.

## Sprint goal

Turn the stabilized generated-document path into a clearer authoring experience and exercise it against realistic multi-page documents. The sprint must not move pagination ownership into WPF and must not duplicate Word/Preview format-selection rules.

## P0-A — Effective table-format selection UX

Baseline gap:

- `TableElement.ReferenceTableFormatKey` already persisted an optional explicit profile key.
- the Properties panel already exposed `TableFormatOptions`.
- null key displayed only `Varsayılan`, while the production resolver selected the first compatible profile by table column count and then fell back to the first profile.
- with multiple compatible profiles, the user could not see which profile was effectively active.

Implemented:

- `TableFormatProfileSelector` centralizes explicit-key / compatible-column-count / first-profile resolution in Application.
- `ReferenceReportContentFormatResolver` consumes the shared selector.
- null persisted key remains automatic and is never replaced with the resolved key.
- `SelectAutomatic` exposes the automatic choice without mutating authored table state.
- Properties UI renders the null-key option as `Otomatik — <effective profile>` when a profile is available.
- Properties UI separately reports the current effective profile.
- existing warning/status text remains a separate channel.
- explicit stable-key selection continues to use `ITableFormatSelectionService` and invalid selections remain rejected.
- imported profiles remain the source profile supplied to the shared selector.

Regression coverage:

- automatic compatible selection and null-key preservation;
- explicit stable-key precedence;
- invalid legacy key fallback without silent authored-state rewrite;
- production reference-aware resolver consumes the same selected format object.

Status: implemented and source-reviewed. Current-head Windows and UI smoke pending.

## P0-B — Multi-page table pagination and continuation fidelity

A deterministic A4 70-row fixture exercises at least three generated pages with:

- a real numbered table caption;
- repeated headers;
- normal detail rows;
- a two-serial grouped product at semantic row index 21 near the first-page boundary;
- five non-serial vertical spans;
- an unmerged Serial column;
- enough rows to create multiple continuation fragments.

The existing Engine paginator already owns the required behavior:

- `TableRowGroup.KeepTogetherWhenPossible` moves a group to a fresh fragment when the complete group fits on a fresh page but not in the current remaining capacity;
- caption is emitted only for fragment zero;
- repeated-header metadata is set for continuation fragments when the resolved table format requests it;
- projected fragment spans are sliced from complete-table semantic spans.

The Sprint 18 integrated Engine regression verifies:

- at least three pages and table fragments;
- caption only on the first fragment;
- one caption sequence number reused by all fragments;
- continuation headers are repeated;
- the first fragment ends before the boundary group;
- the boundary fragment starts at row 21 with `SER-A` / `SER-B` as separate physical rows;
- exactly five non-serial spans survive fragment projection;
- no Serial-column span exists;
- total projected row count equals the source semantic row count, preventing silent row loss/duplication.

Status: integrated regression implemented. No P0-B production paginator rewrite was required by source triage. Current-head Windows gate pending.

## P0-C — Heading/caption keep-with-next boundary fidelity

Observed gap:

- actual table fragment layout measures the visible numbered caption text through `TableCaptionSequenceFormatter.BuildDisplayText`;
- heading `KeepWithNext` minimum-height estimation measured raw `TableContentNode.Caption`;
- a long caption near a wrapping boundary could therefore be underestimated because the visible `Tablo n: ` prefix was omitted during the keep-with-next decision.

Implemented:

- `TableCaptionSequenceFormatter.PeekNextSequenceNumber` returns the next per-identifier sequence number without mutating the counter.
- `ResolveNextSequenceNumber` reuses the same shared next-number rule and remains the only consuming path.
- `GeneratedDocumentPaginator` passes the current caption counter state into heading keep-with-next estimation.
- when the following node is a table, the estimator builds the same visible numbered caption text used by actual table fragment layout.
- actual table layout consumes the caption number only when the table node is laid out.
- blank captions and per-`SequenceIdentifier` counter semantics remain unchanged.
- WPF and Word production paths were not changed by P0-C.

Regression coverage:

- sequence peek returns the next number without consuming it;
- blank caption peek does not create or advance a counter;
- a deterministic wrap-boundary fixture finds a heading/table page boundary using the fully visible caption text;
- the automatic sequence version must produce the same page placement as the equivalent visible caption while preserving raw authored caption text and structured sequence metadata.

Status: implemented and source-reviewed. Current-head Windows gate pending.

## P0-D — Canonical product-pipeline generated DOCX fidelity fixture

Implemented one deterministic Infrastructure regression that starts from real Domain authoring objects and executes:

```text
Project / Report / Page / Section / ReportElement tree
        ↓
ReportContentBuilder
        ↓
SerialQuantityTableContentRowComposer
        ↓
OpenXmlReferenceDocumentFormatProvider
        ↓
ReferenceReportContentFormatResolver
        ↓
WordExporter
        ↓
Generated DOCX reopened with WordprocessingDocument
```

The canonical report contains:

- primary heading;
- secondary heading;
- body paragraph;
- automatic numbered caption;
- six-column 70-row table;
- explicit stable Serial/Quantity grouping role IDs;
- a two-serial product at semantic rows 21/22.

The test first validates the real `ReportContentBuilder` semantic table:

- 70 projected semantic rows;
- `SER-A` / `SER-B` remain separate rows;
- canonical merged quantity is `2` on the first row and blank on the continuation row;
- five non-serial spans exist;
- no Serial-column span exists;
- the row group keeps the two serial rows together when possible.

The same Project/Report is then exported through the real `WordExporter` and the generated package is reopened. OpenXML assertions cover:

- built-in A4 portrait page size;
- page margins plus header/footer distances;
- Heading 1 / Heading 2 paragraph styles;
- Arial 12 primary and secondary heading formatting, primary italic and both keep-next;
- reference secondary-heading indent;
- Arial 10 body text;
- real `SEQ Tablo \\* ARABIC` caption field with cached result `1`;
- `Tablo 1: ...` initial caption text;
- centered Arial 8 bold caption with keep-next;
- 100% table width and fixed layout;
- six unequal grid widths from the first built-in table profile;
- 0.5 pt borders;
- resolved left/right cell margins;
- repeated Word table header;
- resolved preferred row height;
- `w:vMerge restart/continue` on columns 0, 1, 2, 3 and 5 for the grouped product;
- no vertical merge on the Serial column;
- separate `SER-A` / `SER-B` cell text and canonical merged quantity `2`.

Word itself remains responsible for physical page breaking inside a long Word table. Engine multi-page fragment fidelity is covered independently by P0-B; the DOCX fixture does not invent explicit Word page fragments.

Status: canonical product-pipeline fixture implemented and source-reviewed. Current-head Windows gate pending.

## Captured non-Sprint-18 Windows output — not closure evidence

The user supplied successful Windows `restore/build/test` output on 2026-07-11, but the command prompt path was:

```text
C:\Users\PC_4461\Desktop\KKL-Word-Studio-3-main>
```

The output also showed Sprint 17 baseline counts (`Application 185`, `Engine 58`). It therefore validates the merged `main` baseline, not the Sprint 18 branch. It must not be used to mark P0-A/P0-B/P0-C/P0-D GREEN.

## Closure gates

1. Exact current-head Windows on `sprint18/document-authoring-fidelity`:
   - `git rev-parse HEAD`
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - no deleted/skipped/weakened tests.
2. Table-format UX smoke:
   - automatic option names the effective profile;
   - explicit profile selection changes Preview and Word consistently;
   - returning to automatic does not persist a resolved key.
3. Three-page Preview smoke with enough real rows:
   - repeated headers;
   - caption first fragment only;
   - grouped serial rows survive page boundaries.
4. Generated Word smoke:
   - automatic caption numbering;
   - real SEQ fields;
   - repeated header and true vertical merges.
5. Final PR evidence records the exact tested head and visual results.

## Non-goals

- Office COM/Interop.
- WebView-based preview.
- PDF implementation.
- moving pagination into UI/Rendering.
- changing Sprint 17 Serial/Quantity safe-grouping rules without a new explicit product decision.
