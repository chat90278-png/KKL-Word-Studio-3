# Sprint 24 Tranche 01 — Excel-first Session Lifecycle

## Baseline

- Base: `main@7c919b2761957b55661c0ed361669c0c7296b082`
- Branch: `sprint24/01-session-lifecycle`
- Previous Windows gate: `597/597`, build `0 warnings / 0 errors`, startup GREEN

## Implemented scope

### One process-lifetime workspace session

- `MainViewModel` still creates one in-memory `Project` aggregate because the established report transfer/export pipeline is project-based.
- The aggregate is now explicitly treated as an internal session container, not a user-managed native project file.
- Active project and report are installed into the existing singleton `IWorkspace` once during shell startup.
- Word export continues to use the current `IReportExporterRegistry` and active report.

### Removed hidden native-project commands

Removed generated shell commands and backing state for:

- New Project
- Open Project
- Save Project
- Save Project As
- last native project path

There is no longer a hidden command surface that can diverge from the visible Excel-first UI.

### Removed native-project file dialogs from UI

`IFileDialogService` now exposes only dialogs used by the active product flow:

- Open Excel
- Open Word pre-document
- Save Word output

The WPF implementation no longer contains `.kws`, `Proje Aç`, or `Projeyi Kaydet` dialog definitions.

The lower-level `IProjectService` persistence contract is intentionally retained for now. It still provides the authoritative default aggregate factory and remains isolated behind Application/Infrastructure; deleting the repository format is not required for this UI lifecycle cleanup.

### Clean title and messages

- The title bar no longer displays the internal `CurrentProject.Name` value (`Yeni Proje`).
- Quick Report no longer tells the user to create/open a project when workspace state is unavailable.
- Export failure wording now refers to the report workspace rather than project lifecycle actions.

### Quick Report pipeline continuity

- Quick Report still calls the injected `_transferService.Transfer` path.
- Normal `Word'e Aktar` still routes through `ExcelTransferPlacementCoordinator` and the same underlying transfer service.
- No second Excel reader, transfer engine, report tree, preview renderer, or exporter was introduced.

## Test delta

Added Architecture tests: `+4`

Expected total:

```text
601 / 601
```

## Windows gate

```bat
dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Manual smoke:

1. Application title area shows only `KKL Word Studio`; `Yeni Proje` is absent.
2. Excel loading, range editing, zoom, search and report-pane toggle still work.
3. Normal `Word'e Aktar` still creates/updates report tables.
4. Hızlı Rapor still creates selected sheets as separate report tables.
5. Word output dialog and export still work.
6. No New/Open/Save/Save As project command appears anywhere in the shell.
