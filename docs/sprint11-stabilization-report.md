# KKL Word Studio — Sprint 11 Windows Stabilization Report

## Objective

Fix the single real Windows/.NET 8 compile blocker in the Sprint 11 baseline
(`KKL.WordStudio-Sprint11.zip`). No features, no Sprint 12 work, no Sprint 11
architecture refactoring. Exactly one line changes.

## Windows Ground Truth (as supplied)

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED — 0 warnings, 1 error
- `dotnet test` (pre-fix, on the projects that do build):
  Domain 16, Application 52, Infrastructure 44 — **112 passed, 0 failed**

### Reported error

```
src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs  (~line 304)
CS7036: BuildWorkingDataTable(WorksheetWorkingData, WorkingDataViewState)
requires the 'viewState' argument, but LoadPreviewAsync called:
    PreviewTable = BuildWorkingDataTable(workingData);
```

## Root Cause

Sprint 11 changed the private helper to
`BuildWorkingDataTable(WorksheetWorkingData workingData, WorkingDataViewState viewState)`
so the working-data grid honours per-worksheet filter and column-visibility
projection. `RefreshWorkingDataView` was updated to the two-argument form, but
one earlier call site inside `LoadPreviewAsync` — the working-data-present
branch — was missed and still used the old one-argument signature. This is a
pure compile-time argument mismatch, not a logic defect.

## Fix Applied (smallest correct change)

In `LoadPreviewAsync`, inside `if (worksheet?.WorkingData is { } workingData)`
(where `worksheet` is already non-null), the call now uses the existing
worksheet-scoped view state, matching the pattern already used by
`RefreshWorkingDataView`:

```csharp
ApplyWorksheetRange(worksheet);
_currentPreview = BuildWorkingDataPreview(sheetName, workingData);
var viewState = ViewStateFor(worksheet);                       // added
PreviewTable = BuildWorkingDataTable(workingData, viewState);  // was: (workingData)
LoadPersistedMappings(worksheet);
```

### Why `ViewStateFor(worksheet)` and not a fresh instance

Find / filter / column-visibility state is worksheet-scoped runtime state owned
by the ViewModel via `ViewStateFor(worksheet)`. Reusing it preserves the
existing per-worksheet projection across a preview reload. Constructing a new
`WorkingDataViewState()` here would silently reset an active row filter and any
hidden columns whenever the preview reloaded — a behavioural regression. Using
the owned state avoids that.

### Explicitly not done (per the stabilization constraints)

- No `new WorkingDataViewState()` for this call.
- No shared/global view state.
- Did not remove the `viewState` parameter.
- Did not add a legacy one-argument overload to hide the missed call site.
- Did not disable or change filter / column-visibility projection.
- Did not change `BuildWorkingDataTable` semantics.

## Change Summary

| File | Change |
|------|--------|
| `src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs` | 1 call site: resolve `ViewStateFor(worksheet)` and pass it to `BuildWorkingDataTable` |

One file, effectively one corrected call (plus the local it uses). No other
project, and no test, was touched. Both `BuildWorkingDataTable` call sites now
pass the required `viewState`; a project-wide search confirms no remaining
one-argument call and no references outside this ViewModel.

## Verification Honesty

The sandbox is Linux; the affected project (`KKL.WordStudio.UI`) is WPF and is
Windows-only, so it cannot be compiled here, and NuGet.org was unreachable for a
normal restore. The fix was therefore verified by inspection and static checks:

- The only reported error was the one-argument call at the cited line; it is now
  the two-argument form.
- `viewState` is in scope at the call — assigned on the immediately preceding
  line from `ViewStateFor(worksheet)`; `worksheet` is non-null inside the
  `is { } workingData` branch.
- The two-argument signature matches the helper definition
  (`BuildWorkingDataTable(WorksheetWorkingData, WorkingDataViewState)`).
- No stray one-argument call sites remain anywhere in `src`/`tests`.
- Braces and parentheses in the edited file remain balanced.

Because the change is confined to one UI call site and does not touch
Application/Infrastructure/Domain code or any test, the 112 passing tests are
unaffected. **Full `dotnet build` / `dotnet test` on Windows/.NET 8 remains the
final source of truth; Windows verification is pending.** Expected result: build
green (0 errors), 112 tests still passing. NETSDK1057 is only the preview-SDK
notice and is not a build failure.

## Invariants Preserved (untouched by this change)

- All 112 existing tests
- WorksheetWorkingData project ownership
- Worksheet-scoped undo/redo history; history not persisted to .kws
- One mutation = one undo step; paste auto-grow as one undo step
- Find non-mutating; Replace All as one undo step
- WorkingDataViewState runtime-only; row filter does not change report rows;
  column visibility does not change report/Word columns; filtered display row
  maps to the underlying WorkingData row
- Shared ReportContentDocument Preview/Word path
- Sprint 10 multi-source composition
- Original XLSX/XLSM immutability
- Sprint 8 caption / front-matter behavior

## Scope Confirmation

No Sprint 12 work. No report reorder/indent, numbering, source refresh/conflict
merge, relink, autosave, formulas, pagination, PDF, shell redesign, generic
Engine, or Office COM/Interop.
