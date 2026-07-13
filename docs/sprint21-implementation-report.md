# Sprint 21 — Implementation Report

## Status

- Baseline: `main` at `a622bffa71194fc0bbae64cf9664aeb7e3b0becd`
- Branch: `sprint21/multi-source-quick-assembly`
- PR: #5 (draft)
- The earlier multi-source quick-assembly head passed Windows restore/build/test and UI smoke.
- The current head adds bounded caption chrome and selectable report columns; it requires one final exact-head Windows/UI gate before merge.

## Implemented quick-assembly flow

The loaded-source area includes `Hızlı Rapor`. The panel reads the existing in-memory `ExcelWorkspaceViewModel.OpenWorkbooks` list and provides:

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

## Selectable report columns

The existing `Sütunları Eşle` surface now also controls which source columns become report columns:

- every mapping row has an `Aktar` checkbox;
- all columns start selected, preserving the existing behavior;
- `Tümünü seç` and `Hiçbirini seçme` support large schemas;
- at least one column must be selected before applying;
- mapping names/types remain editable;
- the selection is keyed by workbook path + worksheet and stays in the running application session only;
- original Excel columns and WorkingData are not deleted or mutated by this selection.

A transfer decorator applies the optional selection before delegating to the existing `ExcelReportTransferService`:

- no explicit selection is a transparent all-column pass-through;
- source-range transfers project checked non-contiguous Excel columns in Excel order;
- WorkingData transfers resolve checks by stable `SourceField` or `OriginalSourceColumn` identity;
- normal `Word'e Aktar` and `Hızlı Rapor` both use the same selection because they resolve the same `IExcelReportTransferService`;
- no second Excel reader, report engine or Word path was added.

## Empty-caption hint containment fix

The `+ Tablo başlığı` helper previously used a WPF `Popup`, which owns a detached window surface and could remain visually above another panel/window.

The helper now uses a bounded `Adorner` attached to the actual `TableBlockHost`:

- no `Popup`, `PlacementMode` or popup ToolTip remains;
- the adorner is clipped to the table block bounds;
- it is removed when the table host unloads, the Preview unloads or the owner window deactivates;
- its help text uses `AutomationProperties.SetHelpText`;
- empty caption editing and authored-caption direct editing still call the existing caption editor.

## Safety

- Deterministic workbook and worksheet order.
- Case-insensitive duplicate target rejection.
- Stale target removal when sources change.
- Continue after an individual target failure.
- Created targets are deselected after completion.
- Failed/skipped targets remain selected for retry.
- Original XLSX/XLSM files are not modified.
- Column inclusion and quick-assembly state are not persisted into Domain.
- No Engine, Rendering or Word production files changed.

## Coverage

Application tests cover quick-assembly ordering/deduplication/error accounting plus selected non-contiguous source columns, WorkingData original-column identity, all-column backward compatibility and empty-selection rejection.

Architecture guards cover the quick-assembly UI/DI seam, existing transfer delegation, new-table-only safety, bounded caption hint, checkbox/bulk-selection controls, transfer decorator ownership and absence of column-selection persistence in Domain.

## Pending final gate

Run on the current branch head:

```bat
git checkout sprint21/multi-source-quick-assembly
git pull
git rev-parse HEAD

dotnet restore
dotnet build
dotnet test
dotnet run --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

UI smoke:

1. Open `Sütunları Eşle`; verify an `Aktar` checkbox exists on every row.
2. Click `Hiçbirini seçme`, then select a few non-contiguous columns and apply.
3. Run normal `Word'e Aktar`; only checked columns must appear in Preview/Word.
4. Run `Hızlı Rapor` for the same sheet; it must use the same checked set.
5. Reopen the mapper; all source columns must still exist and the applied checks must be restored.
6. Verify the original Excel grid/source file is unchanged.
7. Verify `+ Tablo başlığı` remains inside the Preview/table and never floats above another window.
