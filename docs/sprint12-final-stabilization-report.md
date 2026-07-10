# KKL Word Studio — Sprint 12 Final Stabilization Report

Baseline: `KKL.WordStudio-Sprint12-Completed.zip`. Two source-review defects
fixed narrowly. No Sprint 13 work; no pagination/A4/front-matter/Excel cleanup.
The Sprint 12 structure architecture is unchanged: `Section.Root.Children` is the
source of truth, Contents is a projection, `IReportStructureService` owns
mutations, no `ParentId`/second outline tree, `HeadingStylePresets` owns heading
level.

## 1. Inline rename visibility fix

File: `src/KKL.WordStudio.UI/Views/ContentsView.xaml`.

The rename `TextBox` previously carried a **local** `Visibility="Collapsed"` plus
a Style `MultiDataTrigger` trying to set `Visibility="Visible"`. In WPF a local
property value outranks a Style trigger setter, so the editor could never appear.

Fix (the preferred one): the local `Visibility` attribute was removed; the
default `Collapsed` is now a **Style `Setter`**, and the existing
`MultiDataTrigger` (node `IsSelected` == true AND `DataContext.IsRenaming` ==
true) overrides it to `Visible`. Setter-vs-trigger precedence lets the trigger
win, so the inline editor shows for the selected node while renaming.

Preserved: selected-node-only editor, shared `RenameText` state (no second
per-node rename model), Enter → `CommitRenameCommand`, Escape →
`CancelRenameCommand`, focus-loss → commit unless cancelled (the
`_renameCancelled` flag), heading semantic rename, table `Name`-only rename.

Verification (targeted XAML contract check, run against the file): the XAML is
well-formed; the `RenameBox` `TextBox` has **no** local `Visibility` attribute;
its Style contains both a `Visibility=Collapsed` Setter and a
`Visibility=Visible` trigger setter.

## 2. Level-aware previous-sibling-block fix (Move Up)

File: `src/KKL.WordStudio.Application/Structure/ReportStructureService.cs`.

The old `ComputeSiblingBlockEndingAt` located the previous block by walking
backward through **Content only**. When the previous Heading 1 block ended with
content owned by a Heading 2, the walk stopped at that Heading 2 and returned the
wrong (Heading 2) block, so `MoveUp` on a Heading 1 could produce an invalid,
hierarchy-breaking order.

Fix (structural): that helper was replaced by a **level-aware previous-sibling
locator** `TryFindPreviousSiblingBlock`, driven by the selected block's derived
level and its computed scope:
- **Heading 1**: previous sibling starts at the previous Heading 1 in the
  section; the whole previous H1 block (its H2/content subtree included) moves.
- **Heading 2**: previous sibling starts at the previous Heading 2 within the
  same enclosing Heading 1 scope; the whole H2 block moves.
- **Content/table**: each content element is its own block, so the previous
  sibling is the immediately preceding content element within the same
  nearest-heading scope (swaps only the adjacent table, never jumps to the run
  start).

A helper `IsSiblingBlockStart` classifies a same-level block start. When no
previous same-scope sibling exists, `MoveUp` returns failure without mutating.
No UI special cases were added; Move Down was already correct (it uses the next
block within scope) and is unchanged.

Verified against the prompt's exact shape (H1 A / H2 A.1 / Table A.1 / H1 B /
Table B): `MoveUp(H1 B)` now yields `B, Table B, A, A.1, Table A.1`.

## 3. Files changed

- `src/KKL.WordStudio.Application/Structure/ReportStructureService.cs` —
  replaced the content-only sibling walk with `TryFindPreviousSiblingBlock` +
  `IsSiblingBlockStart` (level- and scope-aware); `MoveBlock` up-branch now uses
  it and fails cleanly when no previous sibling exists.
- `src/KKL.WordStudio.UI/Views/ContentsView.xaml` — rename `TextBox` visibility
  moved from a local value to a Style Setter overridden by the trigger.
- `tests/KKL.WordStudio.Application.Tests/Sprint12ScopedMoveTests.cs` — 4 new
  MoveUp tests.

No Domain/Infrastructure/existing-test files were modified.

## 4. Tests added / preserved

Added (all passing): `MoveUp_Heading1AfterNestedHeading2_SwapsWholeHeading1Blocks`,
`MoveUp_Heading1AfterDirectContent_SwapsWholeHeading1Blocks`,
`MoveUp_Heading2AfterSiblingWithTable_SwapsWholeHeading2Blocks`,
`MoveUp_TableWithinHeadingScope_SwapsOnlyAdjacentTable`.

Preserved and passing: `MoveHeading1_MovesEntireLogicalSubtree`,
`MoveHeading2_MovesHeadingAndFollowingTablesAsBlock`, and all
`Sprint12ScopedMoveTests` (boundary rejections, sibling swaps, delete
confirmation coordination). Inventory: 140 → **144** total test methods; none
deleted, skipped, renamed away, or weakened.

## 5. Actual verification

No .NET SDK shipped and NuGet.org was blocked, so a normal
`dotnet restore`/`build`/`test` could not run. The .NET 8 SDK (8.0.128) was
installed from the Ubuntu archive and the Application layer + tests were compiled
with Roslyn (`csc`) against on-disk reference assemblies and the installed
`Microsoft.Extensions.*` assemblies, bypassing the blocked restore.

Observed, actual output:
- **Build (Application + Domain + Shared):** clean — **0 errors, 0 warnings**,
  under `-warnaserror`.
- **Sprint 12 structure + scoped-move tests:** **32 passed, 0 failed** (incl. the
  4 new MoveUp tests and the two preserved logical-subtree tests).
- **Broader Application suite** (77 tests: Sprint 7/10/11/12, content builder,
  workspace, working-data, plugin): **77 passed, 0 failed.**
- XAML fix verified by parsing `ContentsView.xaml`: well-formed, no local
  `Visibility` on `RenameBox`, Style has both the Collapsed Setter and the
  Visible trigger.

Two pre-existing files (`Sprint8CaptionAndDropTests`,
`Sprint10TransferSourceMappingTests`) use richer xunit `Assert` features the
minimal local shim does not model; they are unmodified and pass on Windows per
the supplied ground truth.

## 6. Remaining blocker

The WPF **UI project is Windows-only** and cannot compile on Linux, so the XAML
fix was validated by parse/attribute contract checks rather than a UI build. A
full multi-project `dotnet build`/`dotnet test` on Windows/.NET 8 (UI +
Infrastructure.Tests with OpenXML) remains **pending**; Windows/.NET 8 is the
final runtime truth. Expected: build green, 144 tests passing. NETSDK1057 is only
the preview-SDK notice and is not a build failure.
