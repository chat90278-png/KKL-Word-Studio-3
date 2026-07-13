# Sprint 21 — Implementation Report

## Status

- Baseline: `main` at `a622bffa71194fc0bbae64cf9664aeb7e3b0becd`
- Branch: `sprint21/multi-source-quick-assembly`
- PR: #5 (draft)
- The multi-source quick-assembly head before the final caption containment fix passed Windows restore/build/test and UI smoke.
- The final caption containment head still requires one exact-head Windows gate before merge.

## Implemented flow

The loaded-source area now includes `Hızlı Rapor`. The panel reads the existing in-memory `ExcelWorkspaceViewModel.OpenWorkbooks` list and provides:

- workbook and worksheet selection;
- optional raw table caption per worksheet;
- select-all and clear-selection actions;
- `Seçilenleri Rapora Aktar`;
- per-target result text;
- created/skipped/failed summary.

Selections, captions and results are session-only UI state. No Domain persistence fields were added.

## Transfer ownership

`QuickAssemblyBatchOrchestrator` only orders selected targets and records outcomes. It does not read Excel or create report tables.

Each target delegates to `ExcelWorkspaceViewModel.TransferQuickAssemblyTargetAsync`, which reuses the existing preview/range/WorkingData/mapping state and the existing `IExcelReportTransferService`.

Batch requests use `TargetElementId = null`, so selected customized tables are never overwritten. Success is accepted only when `ExcelTransferResult.CreatedNewTable` is true. Optional captions are applied to the new `TableElement.Caption`; existing automatic `Tablo n:` sequencing remains authoritative.

## Empty-caption hint containment fix

The `+ Tablo başlığı` helper previously used a WPF `Popup`, which owns a detached window surface and could remain visually above another panel/window.

The helper now uses a bounded `Adorner` attached to the actual `TableBlockHost`:

- no `Popup` or `PlacementMode` remains;
- the adorner is clipped to the table block bounds;
- it is removed when the table host unloads, the Preview unloads or the owner window deactivates;
- its help text uses `AutomationProperties.SetHelpText`, not a popup ToolTip;
- empty caption editing and authored-caption direct editing still call the existing caption editor.

## Safety

- Deterministic workbook and worksheet order.
- Case-insensitive duplicate target rejection.
- Stale target removal when sources change.
- Continue after an individual target failure.
- Created targets are deselected after completion.
- Failed/skipped targets remain selected for retry.
- Original XLSX/XLSM files are not modified.
- No Engine, Rendering or Word production files changed.

## Coverage

Application tests cover ordering, deduplication, selection/caption preservation, stale removal, workbook toggles, continue-on-error accounting and duplicate rejection.

Architecture guards cover the compact UI, DI registration, reuse of `OpenWorkbooks`, delegation to `_transferService.Transfer`, range/header/WorkingData reuse, new-table-only safety, absence of QuickAssembly persistence in Domain, and prevention of detached popup regressions for the caption hint.

## Pending final gate

```bat
git checkout sprint21/multi-source-quick-assembly
git pull
git rev-parse HEAD

dotnet build
dotnet test
dotnet run --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

UI smoke: hover an empty-caption table, verify `+ Tablo başlığı` stays inside the Preview/table area, switch to another window and close/reopen the app, and verify no helper remains above the console or another application.
