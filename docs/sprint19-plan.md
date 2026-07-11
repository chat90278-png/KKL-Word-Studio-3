# Sprint 19 — Fast Source Workspace and Continuous Grid Navigation

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `cc518145de6be653d7c78b29bc7bc8249f3d2b04`
- Working branch: `sprint19/source-tabs-grid-focus`
- Windows restore/build/test output remains execution truth.

## Product direction

KKL Word Studio remains a fast Excel-to-Word accelerator. Sprint 19 removes project-management chrome that does not contribute to the immediate workflow and strengthens keyboard continuity in the Excel working surface.

## P0-A — Remove Project Explorer and expose loaded sources directly

Remove the Project Explorer feature and its shell remnants:

- top command-bar button;
- dimmed scrim and slide-out overlay;
- `IsProjectExplorerOpen` / toggle command;
- `ProjectExplorerView`, ViewModel, generic node type and DI registrations;
- MainWindow constructor dependencies and visual event handler.

Replace it with a compact always-available loaded-source selector in the top command bar:

- binds directly to `ExcelWorkspaceViewModel.OpenWorkbooks`;
- two-way selection through `SelectedWorkbook`;
- shows every currently loaded workbook with sheet count;
- exposes a compact add-source button that reuses `OpenExcelFileCommand`;
- selecting a workbook activates its current worksheet and refreshes the Excel workspace through the existing ViewModel path;
- existing sheet tabs remain the worksheet selector for the active workbook.

No new persistent project memory, database, source-history store or recovery framework is introduced.

## P0-B — Preserve DataGrid keyboard flow after working-data operations

Observed issue:

- toolbar Copy/Paste/Clear buttons take keyboard focus away from `WorkingDataGrid`;
- Paste/Clear rebuild `PreviewTable`, which resets `DataGrid.CurrentCell`;
- the user must click a cell again before arrow-key navigation resumes.

Acceptance:

- remember the active row and stable column identity before clipboard/mutation commands;
- after Copy, return keyboard focus to the existing grid selection;
- after Paste/Clear/Undo/Redo and other table-refreshing toolbar actions, rebuild `CurrentCell` against the refreshed ItemsSource;
- select and scroll the restored cell into view;
- return keyboard focus to the DataGrid at input dispatcher priority;
- Ctrl+V follows the same restore path;
- normal arrow-key navigation is left to the native WPF DataGrid;
- no mutation logic moves into code-behind; only focus/selection restoration lives there.

## Regression guards

Add source-level architecture guards verifying:

- no Project Explorer shell/view/DI remnants remain;
- loaded-source selector binds to `OpenWorkbooks` and `SelectedWorkbook`;
- source add reuses `OpenExcelFileCommand`;
- grid focus restoration uses stable column identity, `CurrentCell`, dispatcher input priority and `Keyboard.Focus`;
- clipboard/mutation actions arm the restore path.

## Closure gate

1. Exact current-head Windows:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - 0 warnings / 0 errors and no skipped/weakened tests.
2. Source selector smoke:
   - load two Excel files;
   - both appear in `Yüklenen Kaynaklar`;
   - switch between them without opening an overlay;
   - sheet tabs follow the selected source.
3. Grid keyboard smoke:
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
