# KKL Word Studio — Sprint 12 Implementation Report
## Report Structure UX

Baseline: `KKL.WordStudio-Sprint11-Stabilized.zip` (Windows/.NET 8 green, 112 tests).
Core flow preserved: Excel'i aç → veriyi hazırla → Word'e aktar → raporu yapılandır → Word oluştur.

Scope held to organizing the existing outline: rename, delete, move up/down,
indent/outdent, drag & drop, and moving tables into heading scope by document
order — with Preview/Contents/Properties/Word kept in sync. No numbering, no
page-break/pagination, no free-form Word-clone structure.

---

## 1. Real structure-mutation model

Contents remains a pure UI projection of the real flat document sequence
(`Section.Root.Children`). This sprint adds **no** `ParentId`, no persisted
`ContentsNodeViewModel`, no second outline tree, and no duplicated report order
in UI state. Every structure mutation changes the real ordered `ReportElement`
sequence; hierarchy stays derived from element order plus
`HeadingStylePresets` heading style, exactly as the Preview/Word content builder
already interprets it.

All product logic lives in a new Application service,
`IReportStructureService` / `ReportStructureService`, which owns: locating an
element and its real owning `Container`/`Section`; rename; delete; move up/down;
indent/outdent; and drag-drop move. It returns `Result` with short Turkish
messages. No structure logic lives in `ContentsView.xaml.cs`.

## 2. Logical subtree / block rules

Movement and drag-drop operate on **logical blocks** computed from document
order and derived heading level:
- Table / content element: block = that element only.
- Heading 2: block = the Heading 2 plus following tables/content until the next
  Heading 1 or Heading 2.
- Heading 1: block = the Heading 1 plus all following Heading 2 / table content
  until the next Heading 1.

Moving a heading moves its whole derived subtree as one contiguous block;
projected children are never left behind. Move Up/Down swaps the selected block
with the adjacent logical sibling block; an invalid move (already at top/bottom)
returns a friendly failure and mutates nothing. Header/footer sections are never
located or moved through this service.

## 3. Rename / delete

Rename: Heading / Alt Heading rename rewrites the `TextElement.Content` literal
via the same semantic content path Preview and Word read, so the new text shows
everywhere. Table rename changes `TableElement.Name` only — `Caption`, `Columns`
(`Header`/`SourceField`), `Sources` and `Binding` are untouched. Source
Excel/workbook/worksheet objects are never renamed from Contents. UI supports F2
/ a compact "Yeniden Adlandır" command; commit on Enter/focus-loss, cancel on
Escape (via `IsRenaming`/`RenameText` state).

Delete: removes only the selected real element from its owning
`Container.Children`. It never cascades into visually nested headings/tables,
never deletes `Project.DataSources`, and never deletes Excel sources or
`WorkingData` for a (multi-source) table. Because caption was a value copy,
deleting a heading does not erase an existing table caption. After delete,
selection moves to a sensible neighbour (previous sibling, else next), and
Preview/Contents/Properties refresh. A short Turkish confirmation gates the
destructive action in the UI.

## 4. Indent / outdent

Heading level stays Style-based through `HeadingStylePresets` — no new
`HeadingElement`, no duplicate `HeadingLevel`. Indent: Heading 1 → Heading 2
(Heading 2 cannot indent further). Outdent: Heading 2 → Heading 1 (Heading 1
cannot outdent further). Because the level is expressed through the same style
preset, Contents classification, TOC, Preview, and Word heading semantics stay
in agreement. Tables gain no fake indent level; "under a heading" for a table is
purely document order within the derived heading scope.

## 5. Drag & drop semantics

The real element `Id` is the drag identity; no `ContentsNodeViewModel` is ever
dragged or persisted as product state. `ContentsView.xaml.cs` only starts the
WPF `DragDrop`, identifies the target node, and computes Before/Into/After from
the pointer's vertical position within the target row, then calls
`ContentsViewModel.MoveByDragDrop(sourceId, targetId, mode)`, which delegates to
the service.

- **Before / After**: move the selected logical block before / after the target
  logical block.
- **Into** (valid only when the target is Heading 1 or Heading 2): move the
  block to the **end of the target heading's derived scope**, so by real
  document order it becomes nested under that heading. A table dropped Into a
  heading becomes visually nested under it. A Heading 2 dropped Into a Heading 1
  stays Heading 2. A Heading 1 dropped Into a Heading 1 is explicitly demoted to
  Heading 2 as part of the move (so classification/TOC/Word stay in agreement).

Cycles/self-drops are rejected: a block cannot be dropped onto itself or inside
its own logical subtree, and the document order is never partially mutated on a
rejected move. Deterministic, no animated ghost required.

## Multi-page / section rule

Structure operations stay inside a single owning non-header/footer
`Section.Root`. A drag whose source and target are in different Sections is
rejected with a friendly "yalnızca aynı bölüm içinde taşınabilir" message;
elements are never silently moved between Pages or Sections. A dedicated
page-break/layout sprint can add cross-section semantics later.

## 6. Preview / Contents / Properties / Word consistency

After every successful structure mutation the ViewModel keeps the moved/renamed
element selected (`SetSelectedReportElement` with the same real Id), rebuilds
Contents, and calls `NotifyReportContentChanged()` so the shared
`ReportContentDocument` Preview/Word path re-resolves. Properties continues to
point at the same real element (shared selection). Selection-only changes still
flow through the existing narrow selection state and do not trigger Excel/
provider reloads, preserving the Sprint 7 `ReportContentChanged`/
`WorkspaceChanged` behaviour. No report-structure undo/redo was introduced;
Sprint 11 Undo/Redo remains working-data-only.

## Files changed

New (Application):
- `Structure/ReportStructureService.cs` — `IReportStructureService` +
  implementation (locate/rename/delete/move/indent/outdent/drag-drop, block and
  scope computation over the real sequence).

Modified:
- `DependencyInjection/ApplicationServiceCollectionExtensions.cs` — register
  `IReportStructureService`.
- `UI/ViewModels/ContentsViewModel.cs` — inject the service; rename (begin/
  commit/cancel), delete, move up/down, indent/outdent commands; `MoveByDragDrop`
  entry point; post-mutation selection + rebuild + notify; neighbour-selection
  on delete.
- `UI/Views/ContentsView.xaml.cs` — drag gesture routing only (compute source/
  target/mode, call the ViewModel).
- `UI/Views/ContentsView.xaml` — wire `AllowDrop` and the drag/drop handlers.

No Domain, Infrastructure, or existing test/source files were modified.

New tests (18) in `Sprint12ReportStructureTests.cs`.

## Tests

All 14 prompt-required methods are present, plus 4 edge-case tests (self-drop,
move-up-at-top, indent-Heading2-rejected, Heading1-into-Heading1-demotion) — 18
total. Existing inventory preserved: 112 → **130** total test methods; no test
deleted, skipped, renamed away, or weakened.

Required tests: RenameHeading_UpdatesSemanticContent,
RenameTable_ChangesNameWithoutChangingCaptionOrBinding,
DeleteTable_DoesNotDeleteProjectDataSources,
DeleteHeading_DoesNotCascadeDeleteProjectedChildren,
MoveHeading1_MovesEntireLogicalSubtree,
MoveHeading2_MovesHeadingAndFollowingTablesAsBlock, MoveTable_ChangesDocumentOrder,
IndentHeading1_ChangesItToHeading2, OutdentHeading2_ChangesItToHeading1,
DragTableIntoHeading_MovesTableIntoDerivedHeadingScope,
DragHeadingIntoOwnSubtree_IsRejectedWithoutMutation,
CrossSectionMove_IsRejectedWithoutMutation,
StructureMutation_PreservesSelectedElementIdentity,
ReorderedStructure_IsReflectedInReportContentAndWordOrder.

## Actual verification

No .NET SDK shipped in the sandbox and NuGet.org was blocked, so a normal
`dotnet restore`/`build`/`test` could not run. To verify for real, the .NET 8
SDK (8.0.128) was installed from the Ubuntu archive and the Application layer +
tests were compiled directly with Roslyn (`csc`) against the on-disk
`Microsoft.NETCore.App` reference assemblies and the installed
`Microsoft.Extensions.*` assemblies, bypassing the blocked package restore.

Observed, actual output:
- **Build (Application + Domain + Shared):** clean — **0 errors, 0 warnings**,
  even under `-warnaserror`.
- **Sprint 12 tests:** **18 passed, 0 failed** — including the move/block
  ordering cases and `ReorderedStructure_...` run through the real
  `ReportContentBuilder`, which confirms report-content/Word order follows the
  mutated document order.
- **Broader Application suite** (Sprint 7/10/11/12 + content-builder +
  workspace + working-data + plugin, 63 tests via a reflection runner and an
  xunit-compatible Assert shim): **63 passed, 0 failed.**

Two pre-existing files (`Sprint8CaptionAndDropTests`, `Sprint10Transfer-
SourceMappingTests`) use richer xunit `Assert` features the minimal local shim
does not fully model; they were not run locally. They are unmodified and pass on
Windows per the supplied ground truth. (One transient local failure during
development was an Assert-shim array-comparison artifact, not a code/test defect;
it passed once the shim matched real xunit's element-wise comparison.)

## Remaining gaps

- The WPF **UI project is Windows-only** and cannot compile on Linux, so the
  `ContentsViewModel` commands and `ContentsView` drag-drop routing/XAML were
  validated by inspection and brace/paren/member-reference checks, not by a UI
  compile. A full multi-project `dotnet build`/`dotnet test` on Windows/.NET 8
  (UI + Infrastructure.Tests with OpenXML) remains **pending**; Windows/.NET 8
  is the final runtime truth. Expected: build green, 130 tests passing.
- Contents dock actions are exposed on the ViewModel (Yeniden Adlandır, Sil,
  Yukarı, Aşağı, Girintiyi Artır/Azalt) with validity gating in the service;
  richer WPF presentation (disabled-state styling, drag affordance visuals) can
  follow without touching the verified structure logic.
- Move Up/Down targets adjacent sibling blocks at the same projected scope; more
  elaborate cross-scope promotion on move is intentionally out of scope this
  sprint.
