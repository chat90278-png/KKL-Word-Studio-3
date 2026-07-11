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

Current state at the Sprint 18 baseline:

- `TableElement.ReferenceTableFormatKey` persists an optional explicit profile key.
- the Properties panel already exposes `TableFormatOptions`.
- null key displayed only `Varsayılan`, while the production resolver actually selected the first compatible profile by table column count and then fell back to the first profile.
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

A deterministic A4 70-row fixture now exercises at least three generated pages with:

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

## P0-C — Heading/caption keep-with-next boundary QA

Source triage identified a real measurement gap before implementation:

- actual table fragment layout measures the visible numbered caption text through `TableCaptionSequenceFormatter.BuildDisplayText`;
- heading `KeepWithNext` minimum-height estimation currently passes raw `TableContentNode.Caption` to `DeterministicTablePaginator.EstimateMinimumFragmentHeight`;
- therefore a long caption near a wrapping boundary can be underestimated because the visible `Tablo n: ` prefix is omitted during the keep-with-next decision.

Planned correction:

- preserve the existing Engine-owned keep-with-next decision path;
- peek the next semantic caption sequence number without consuming the counter;
- measure the same visible numbered caption text used by actual fragment layout;
- keep blank-caption and per-identifier counter behavior unchanged;
- add a focused boundary regression before changing broader pagination behavior.

Acceptance:

- resolved `KeepWithNext` semantics remain honored by Engine.
- Preview and generated Word do not leave an isolated heading/caption at the bottom of a page when the following content can be moved as a unit.
- long caption minimum-fragment measurement uses the same visible numbered caption text rendered in Preview.
- Word keeps real caption SEQ fields.

Status: root cause source-triaged; implementation intentionally deferred until the first P0-A/P0-B Windows gate so failure attribution remains narrow.

## P0-D — Canonical generated-DOCX fidelity fixture

Add one deterministic generated-document fixture/test path covering:

- A4 page geometry and margins;
- primary/secondary headings;
- body text;
- automatic numbered table caption;
- six-column unequal widths;
- fixed table layout;
- borders and cell margins;
- repeated header;
- serial/quantity vertical merge semantics;
- continuation across multiple pages where applicable.

The fixture is generated by the product pipeline. Do not replace it with a hand-authored DOCX that bypasses `ReportContentBuilder` or the Word writers.

## Closure gates

1. Exact current-head Windows:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - no deleted/skipped/weakened tests.
2. Table-format UX smoke:
   - automatic option names the effective profile;
   - explicit profile selection changes Preview and Word consistently;
   - returning to automatic does not persist a resolved key.
3. Three-page Preview smoke:
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
