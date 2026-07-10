# KKL Word Studio — Sprint 12 Completion / UI Wiring Gate

Baseline: `KKL.WordStudio-Sprint12.zip`. This task closes the five concrete
Sprint 12 completion gaps (A–E) narrowly, without starting Sprint 13/14 and
without pagination, A4 preview, front-matter rendering, or Excel cleanup. The
accepted Application-layer direction is unchanged: Contents is a projection of
`Section.Root.Children`, `IReportStructureService` owns real mutations, no
`ParentId`/second outline tree, heading level derived from `HeadingStylePresets`
plus document order. No report-structure undo/redo was introduced.

## 1. Contents command wiring (gap A)

`ContentsView.xaml` now binds every structure command. The selected-node action
area (compact `WrapPanel`, not a ribbon) exposes: Yeniden Adlandır, Sil, Yukarı,
Aşağı, Girintiyi Artır, Girintiyi Azalt — bound to `BeginRenameCommand`,
`DeleteSelectedCommand`, `MoveUpCommand`, `MoveDownCommand`, `IndentCommand`,
`OutdentCommand`. The TreeView has key bindings: **F2 → BeginRenameCommand**,
**Delete → DeleteSelectedCommand**.

## 2. Inline rename + keyboard (gap A)

The node template now contains a real inline rename `TextBox` shown only for the
selected node while `IsRenaming` is true (MultiDataTrigger on `IsSelected` +
`DataContext.IsRenaming`); the label collapses meanwhile. It binds two-way to the
existing single `RenameText` state (no per-node rename model). Enter →
`CommitRenameCommand`, Escape → `CancelRenameCommand`, focus-loss → commit via
`RenameBox_LostFocus`, **unless** the user cancelled: `CancelRename` sets a
`_renameCancelled` flag that `CommitRename` honours, so a trailing focus-loss
after Escape does not re-commit. Heading/Alt Heading rename changes semantic
text; table rename changes `TableElement.Name` only, preserving Caption, Binding,
Sources, columns and SourceField identities (locked by
`RenameTable_ChangesNameWithoutChangingCaptionOrBinding`).

## 3. Delete confirmation (gap B)

`DeleteSelected` now calls the existing `IDialogService.ShowConfirmation` with a
short Turkish prompt ("Seçili öğe silinsin mi?") before mutating. User cancels →
no mutation; confirms → the real element is deleted via the service. No
`MessageBox` in Application, no deletion product logic in code-behind. Deleting a
table leaves `Project.DataSources`/`WorkingData` intact; deleting a heading does
not cascade; value-copied captions survive; a sensible neighbour is selected;
Preview/Contents/Properties refresh. Coordination tests
`DeleteConfirmation_Cancel_DoesNotMutate` and
`DeleteConfirmation_Confirm_DeletesRealElement` model the confirm/cancel gate at
the service boundary (the real ViewModel is WPF-only).

## 4. Drag identity is the real element Id (gap C)

`ContentsView.xaml.cs` now carries **`DataObject(typeof(Guid), ElementId)`** as
the drag payload and reads the `Guid` back on drop — no `ContentsNodeViewModel`
is dragged or persisted as product state. The drop target is still resolved from
the target row's `DataContext`; code-behind only starts `DragDrop`, resolves the
target, computes Before/Into/After from pointer position, and calls
`MoveByDragDrop(sourceElementId, targetElementId, mode)`. No structure logic in
code-behind.

## 5. Same-scope Move Up/Down (gap D)

The block/scope helper was refined (not patched with UI special-cases). A new
`ComputeScope(children, index)` returns the `[Start, End)` range of sibling
blocks an element may move among:
- Heading 1: the whole section (siblings = other Heading 1 blocks).
- Heading 2: from just after its enclosing Heading 1 to that Heading 1's end
  (siblings = other Heading 2 blocks in the same Heading 1).
- Content/table: the run owned by its nearest enclosing heading, bounded by the
  next heading of any level.

`MoveBlock` now rejects a move that would cross the scope boundary
("Öğe zaten bu kapsamda en üstte/en altta."), so a first/last child at its scope
boundary fails without mutation and there is **no implicit promotion/outdent** —
promotion stays explicit via Indent/Outdent or drag Into. The buggy
`ComputeBlockEndingAt` (which could discover an enclosing Heading 1 and escape
scope) was replaced by scope-bounded `ComputeSiblingBlockEndingAt`. Adjacent
sibling swaps still work (Heading 2 ↔ Heading 2 in-scope, table ↔ table in-scope,
Heading 1 ↔ Heading 1). All six required D-tests plus two extras pass, and the
original Sprint 12 logical-subtree tests still pass.

## 6. Failure feedback (gap E)

`ContentsViewModel` now surfaces the service's short Turkish `Result.Error`
through a non-modal `StatusMessage` property, shown in the Contents dock via a new
`StringToVisibilityConverter` (only visible when non-empty). Successful mutations
clear it. Harmless invalid moves never raise a modal dialog; only the destructive
delete uses a confirmation dialog. Examples surfaced: "Öğe zaten bu kapsamda en
üstte.", "Alt başlık daha fazla girintilenemez.", "Öğeler yalnızca aynı bölüm
içinde taşınabilir."

## 7. Files changed

- `src/KKL.WordStudio.Application/Structure/ReportStructureService.cs` —
  scope-aware Move Up/Down: new `ComputeScope`, `ComputeSiblingBlockEndingAt`;
  boundary-reject messages; removed scope-escaping helper.
- `src/KKL.WordStudio.UI/ViewModels/ContentsViewModel.cs` — inject
  `IDialogService`; delete confirmation; rename cancel-flag semantics;
  `StatusMessage` failure feedback.
- `src/KKL.WordStudio.UI/Views/ContentsView.xaml` — action buttons, inline
  rename TextBox, F2/Delete key bindings, status line, drag handlers.
- `src/KKL.WordStudio.UI/Views/ContentsView.xaml.cs` — Guid drag payload;
  `RenameBox_LostFocus` commit.
- `src/KKL.WordStudio.UI/Converters/StringToVisibilityConverter.cs` (new) +
  registration in `App.xaml`.
- `tests/KKL.WordStudio.Application.Tests/Sprint12ScopedMoveTests.cs` (new).

No Domain/Infrastructure/existing-test files were modified.

## 8. Tests

New: 8 scoped-move tests (6 required + `MoveDown_Heading1_StillMovesAmongHeading1Blocks`,
`MoveUp_ContentInsideHeading2_StaysInHeading2Scope`) and 2 delete-confirmation
coordination tests — 10 total. Existing inventory preserved: 130 → **140**; no
test deleted, skipped, renamed away, or weakened.

## 9. Actual verification

No .NET SDK shipped and NuGet.org was blocked, so a normal
`dotnet restore`/`build`/`test` could not run. The .NET 8 SDK (8.0.128) was
installed from the Ubuntu archive and the Application layer + tests were compiled
with Roslyn (`csc`) against on-disk reference assemblies and the installed
`Microsoft.Extensions.*` assemblies, bypassing the blocked restore.

Observed, actual output:
- **Build (Application + Domain + Shared):** clean — **0 errors, 0 warnings**,
  under `-warnaserror`.
- **Sprint 12 structure + scoped-move tests:** **26 passed, 0 failed**
  (18 original + 8 scoped-move).
- **Broader Application suite** (73 tests incl. Sprint 7/10/11/12, content
  builder, workspace, working-data, plugin): **73 passed, 0 failed.**
- XAML (`ContentsView.xaml`, `App.xaml`) verified well-formed; UI `.cs` files
  brace/paren-balanced with required usings present.

Two pre-existing files (`Sprint8CaptionAndDropTests`,
`Sprint10TransferSourceMappingTests`) use richer xunit `Assert` features the
minimal local shim does not model; they are unmodified and pass on Windows per
the supplied ground truth.

## 10. Remaining gaps

- The WPF **UI project is Windows-only** and cannot compile on Linux, so the
  `ContentsViewModel` wiring, `ContentsView` XAML/code-behind, and the new
  converter were validated by inspection, XAML well-formedness, and brace/
  member checks — not by a UI compile. A full multi-project `dotnet build`/
  `dotnet test` on Windows/.NET 8 (UI + Infrastructure.Tests with OpenXML)
  remains **pending**; Windows/.NET 8 is the final runtime truth. Expected:
  build green, 140 tests passing.
- Focusing the rename `TextBox` on open and selecting-all is left to WPF
  behaviour/light code-behind polish; the commit/cancel/focus-loss contract is
  wired.
- Button enable/disable gating currently relies on command no-ops plus non-modal
  status feedback; richer `CanExecute` gating per node kind can follow without
  touching the verified structure logic.
