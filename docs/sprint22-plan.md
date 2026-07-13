# Sprint 22 — Release Readiness & Large-File Hardening

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `7f80cb83658e3f8acccf81cff6a23ea72ddaccfc`
- Working branch: `sprint22/release-readiness-big-data`
- Sprint 21 is the only accepted code baseline.
- Windows `dotnet restore/build/test` output remains execution truth.
- Do not mark any Sprint 22 head GREEN without exact Windows output and visible UI smoke for that head.

## Product goal

Keep the existing Excel-to-Word accelerator stable and usable with large workbooks, many worksheets, wide ranges, many report tables, and long Preview/Word documents.

This sprint hardens existing pipelines. It must not create a second Excel reader, transfer engine, Preview path, layout engine, or Word writer.

## First source-review findings

The first pass identified measurement targets; these are findings to verify, not optimization assumptions:

1. `OpenXmlExcelWorkbookReader` exposes async methods but performs synchronous OpenXML traversal before returning `Task.FromResult`.
2. `OpenWorkbookAsync` and `GetSheetPreviewAsync` currently do not observe cancellation during traversal.
3. `ReadWorkingDataAsync` materializes a full row lookup to obtain the header and then traverses sheet rows again for data.
4. When no explicit end column is available, `ResolveMaxColumn` scans the complete sheet before working-data extraction scans it again.
5. Preview is bounded by row count, but each row still expands to the maximum discovered column and wide sheets can create large temporary collections.
6. `ExcelWorkspaceViewModel.LoadPreviewAsync` awaits these APIs from the UI flow, so synchronous reader work can still block the dispatcher.

No production optimization is approved until the baseline tranche records elapsed time, allocation deltas, shape correctness, and source-file integrity.

## P0-A — Big-data baseline and measurement

### Tranche 1

Implemented and Windows-validated:

- reusable long-operation measurement contracts for elapsed time, managed-heap before/after, total allocated-byte delta, completion state, and error text;
- measurements on success, failure, and cancellation without swallowing the original result or exception;
- deterministic OpenXML workbook fixtures for thousands of rows, very wide worksheets, many worksheets, sparse/dirty rows, and normal six-column data;
- reader-focused tests that verify exact sheet/row/column boundaries, deterministic ordering, and unchanged source-file hashes.

Validated head: `2d2892cc25e80985220f52839de4e1ed73c0f63f`

Windows evidence:

- Release build: 0 warnings / 0 errors
- Tests: 489/489
- Failed / skipped: 0 / 0
- UI smoke: GREEN

### Later measurement targets

- workbook metadata load;
- sheet Preview load;
- AutoRange detection;
- WorkingData creation;
- single-sheet transfer;
- Hızlı Rapor batch transfer;
- ReportContentBuilder;
- deterministic pagination;
- Preview projection;
- Word export.

## P0-B — Responsive long operations

### Tranche 2 — Hızlı Rapor

Implemented:

- target-level progress and current target text;
- completed/total progress;
- cooperative cancellation at safe target/checkpoint boundaries;
- completed-result retention;
- no fabricated result for incomplete targets;
- duplicate command suppression;
- selection commands disabled while active;
- existing transfer delegation and per-target error isolation preserved.

Windows evidence for responsive head:

- Release build: 0 warnings / 0 errors
- Tests: 494/494
- Failed / skipped: 0 / 0
- UI smoke: FAILED because the process stayed attached but no main window was visible.

Therefore the responsive head is not fully GREEN.

### Tranche 2B — startup visibility and diagnostics

Implemented after the UI-startup failure:

- stable per-user logs under `%LOCALAPPDATA%\KKL Word Studio\Logs`;
- startup stage markers for host build, window resolution, show and first render;
- WPF dispatcher, AppDomain and unobserved-task failure capture;
- fatal startup message with diagnostic path;
- explicit `Application.MainWindow` assignment;
- minimized/off-screen main-window recovery and activation;
- architecture guards for diagnostics and visibility recovery.

Status: exact-head Windows build/test and visible-window smoke pending. Expected total: 496 tests.

## P0-C — Controlled load and error isolation

Candidate fixes must be justified by Tranche 1/2 evidence. Likely investigation points:

- avoid repeated complete worksheet scans;
- avoid building a full row dictionary when only one header row is needed;
- keep preview row caps and DataGrid virtualization intact;
- debounce/coalesce redundant Preview refresh requests;
- continue after one source/table failure in batch operations;
- preserve all rows and selected columns without silent truncation.

## P0-D — Source session management

Implement session-only controls and rules:

- close one loaded source;
- clear all loaded sources;
- activate the existing source when the same path is added again;
- treat path comparison case-insensitively on Windows;
- handle moved, deleted, locked, or inaccessible files with explicit status;
- never require persistent source memory.

## P0-E — Word export safety

Use the existing Word export pipeline and add safe file handling:

- detect output already open/locked;
- handle same-name collision explicitly;
- validate folder and access rights;
- write through a temporary file and publish atomically where supported;
- remove failed temporary output;
- preserve Preview/Word fidelity and existing caption/grouping behavior.

## P0-F — Acceptance pack

Representative scenarios:

1. normal six-column file;
2. very wide file;
3. many-sheet workbook;
4. sparse/dirty rows;
5. serial/quantity grouped file;
6. long report;
7. multi-source Hızlı Rapor;
8. report-column selection.

For every applicable scenario verify:

- row/column counts;
- deterministic source/sheet/table order;
- source XLSX/XLSM hash unchanged;
- Preview correctness;
- Word correctness;
- warning-center navigation;
- cancellation/failure behavior;
- no broken partial output.

## P0-G — Release package

- Release build;
- explicit version number;
- executable/icon verification;
- self-contained publish or installer decision;
- writable and discoverable log folder;
- clean Windows-machine smoke test.

## Windows command shape

```bat
git checkout sprint22/release-readiness-big-data
git pull
git rev-parse HEAD

taskkill /IM KKL.WordStudio.exe /F 2>nul

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

The measurement report must include machine/OS/SDK details, scenario dimensions, elapsed duration, allocation delta, result counts, and source hash checks.

## Non-goals

- database;
- cloud sync;
- complex project history;
- template marketplace;
- PDF engine;
- Office COM/Interop;
- background scheduling;
- persistent user-preference memory.

## Implementation order

1. Measure first.
2. Fix the largest proven bottleneck.
3. Add progress/cancellation only around measured long operations.
4. Preserve existing pipelines and ownership boundaries.
5. Run the Windows gate after each meaningful tranche.
6. Keep the PR in draft until the exact-head Windows and UI acceptance evidence is attached.
