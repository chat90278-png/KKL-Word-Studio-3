# Sprint 19 — Fast Source Workspace and Continuous Grid Navigation

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `cc518145de6be653d7c78b29bc7bc8249f3d2b04`
- Working branch: `sprint19/source-tabs-grid-focus`
- Windows restore/build/test output remains execution truth.
- Do not mark the current branch GREEN from source review alone.

## Product direction

KKL Word Studio remains a fast Excel-to-Word accelerator. Sprint 19 removes project-management chrome that does not contribute to the immediate workflow and strengthens keyboard continuity in the Excel working surface.

No persistent recent-source history, database, cloud sync, recovery engine or complex preference memory is introduced.

## P0-A — Remove Project Explorer and expose loaded sources directly

Removed:

- the `Proje Gezgini` top-bar command;
- dimmed scrim and slide-out overlay;
- `IsProjectExplorerOpen` and toggle command;
- `ProjectExplorerView`, code-behind, ViewModel and generic node type;
- Project Explorer DI registrations and MainWindow dependency.

Added:

- a compact `Yüklenen Kaynaklar` selector in the top command bar;
- direct binding to `ExcelWorkspaceViewModel.OpenWorkbooks`;
- two-way selection through `SelectedWorkbook`;
- workbook name and worksheet-count display;
- a compact add button reusing `OpenExcelFileCommand`;
- existing sheet tabs remain the worksheet selector for the active workbook.

The selector is session-only and does not persist a recent-file history.

## P0-B — Preserve DataGrid keyboard flow after working-data operations

Implemented in a UI-only partial class:

- remember active row and stable bound-column identity;
- after Copy, return focus without collapsing the copied selection;
- after Paste/Clear/Undo/Redo/Reset and Delete, restore `CurrentCell`, selection, scroll position and keyboard focus;
- restore again after asynchronous `PreviewTable` replacement;
- Ctrl+V, Ctrl+Z and Ctrl+Y use the same path;
- normal arrow-key navigation remains native WPF DataGrid behavior;
- all data mutation remains in the existing ViewModel/Application services.

## First Windows gate

The first exact branch run reported:

- restore: success;
- build: success, 0 warnings / 0 errors;
- Domain 18/18;
- Application 191/191;
- Engine 60/60;
- Infrastructure 122/122;
- Architecture 63/64 with one failure.

The architecture failure was a stale Sprint 16 guard that attempted to read the intentionally removed `ProjectExplorerView.xaml`. The guard was preserved and updated to verify that `LoadedSourcesView` remains separate from the `KAYNAK VERİ` Excel workspace surface.

The same run exposed a startup-only lifecycle bug. `ExcelWorkspaceView.KeyboardFlow.cs` originally subscribed to `_viewModel` from `OnInitialized`; WPF can invoke that override during `InitializeComponent`, before the constructor assigns the injected ViewModel. The hook setup now occurs in `Loaded`, while `OnInitialized` only registers the lifecycle events. The new architecture guard verifies that `_viewModel` is not dereferenced from `OnInitialized`.

Status: fixes applied; exact current-head Windows gate and application smoke pending.

## Regression guards

Architecture tests verify:

- no Project Explorer shell/view/DI remnants remain;
- loaded-source selector binds to `OpenWorkbooks` and `SelectedWorkbook`;
- source add reuses `OpenExcelFileCommand`;
- loaded-source selector and `KAYNAK VERİ` remain separate surfaces;
- grid focus restoration uses stable column identity, `CurrentCell`, dispatcher input priority and `Keyboard.Focus`;
- clipboard/mutation actions arm the restore path;
- ViewModel-dependent keyboard hooks are attached in `Loaded`, not during `InitializeComponent`.

## Closure gate

1. Exact current-head Windows:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - 0 warnings / 0 errors and no skipped/weakened tests.
2. Startup smoke:
   - `dotnet run --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj` opens the main window.
3. Source selector smoke:
   - load two Excel files;
   - both appear in `Yüklenen Kaynaklar`;
   - switch between them without opening an overlay;
   - sheet tabs follow the selected source.
4. Grid keyboard smoke:
   - select a cell/block;
   - Copy from toolbar and continue with arrow keys;
   - Paste to another location and continue with arrow keys without clicking the grid;
   - Ctrl+V has the same behavior;
   - Clear/Undo/Redo do not strand focus on toolbar controls.

## Non-goals

- persistent recent-file history;
- database or cloud sync;
- complex project recovery;
- template library;
- changing Excel mutation semantics or source-file immutability;
- changing Preview/Word document behavior.
