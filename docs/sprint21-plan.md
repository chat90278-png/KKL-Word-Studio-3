# Sprint 21 — Multi-Source Quick Report Assembly

## Baseline

- Repository: `chat90278-png/KKL-Word-Studio-3`
- Baseline branch: `main`
- Baseline commit: `a622bffa71194fc0bbae64cf9664aeb7e3b0becd`
- Working branch: `sprint21/multi-source-quick-assembly`
- Windows `dotnet restore/build/test` output remains execution truth.
- Do not mark a branch head GREEN from source review alone.

## Product goal

Reduce repeated Excel-to-report actions without turning KKL Word Studio into a persistent project-management system. The user should be able to choose several currently loaded workbook sheets and assemble report tables in one deterministic operation.

Selections and draft captions are session-only UI state. They are not added to the persisted Project/Domain model.

## P0-A — Session-only source/sheet selection

Implemented:

- `Hızlı Rapor` is hosted beside the existing loaded-source selector.
- The panel projects `ExcelWorkspaceViewModel.OpenWorkbooks`; no parallel source store exists.
- Workbook and worksheet checkboxes are available.
- Workbook selection selects/deselects its worksheets.
- Sheet selection remains independently editable.
- Source synchronization preserves temporary selection/caption state and removes stale targets.
- Duplicate `(source path, worksheet)` targets are deduplicated case-insensitively.
- Order follows loaded-workbook order and workbook worksheet order.

Status: implemented and source-reviewed; current-head Windows/UI gate pending.

## P0-B — Deterministic batch transfer orchestration

Implemented:

- `Seçilenleri Rapora Aktar` executes selected targets in visible deterministic order.
- `QuickAssemblyBatchOrchestrator` only orders and accounts for outcomes.
- Every real transfer delegates to `ExcelWorkspaceViewModel.TransferQuickAssemblyTargetAsync`.
- The target adapter reuses the existing preview/range/WorkingData/mapping state and `IExcelReportTransferService`.
- One failure is recorded while later targets continue.
- Duplicate targets are rejected before any transfer call.
- Requests use `TargetElementId = null`; selected customized tables are not overwritten.
- Success is accepted only when `ExcelTransferResult.CreatedNewTable` is true.
- Original Excel files remain read-only.

Status: implemented and source-reviewed; current-head Windows/UI gate pending.

## P0-C — Compact per-target authoring options

Implemented:

- Optional raw caption per worksheet.
- Initial and only action in this sprint is safe `Yeni tablo` creation.
- Deselecting a worksheet skips it.
- Created targets are deselected after completion to reduce accidental duplicate imports.
- Failed/skipped targets remain selected for retry.

No wizard, persistent template memory or existing-table overwrite option was added.

Status: implemented and source-reviewed; current-head Windows/UI gate pending.

## P0-D — Batch result summary and diagnostics integration

Implemented:

- Per-target `Oluşturuldu`, `Atlandı` or `Başarısız` text.
- Explicit created/skipped/failed aggregate summary.
- Existing Sprint 20 Diagnostics Center remains unchanged and continues to receive generated table warnings through the normal Preview path.
- No persistent batch history is stored.

Status: implemented and source-reviewed; current-head Windows/UI gate pending.

## Regression coverage

Application tests cover:

- session selection uniqueness and deterministic order;
- workbook-level select/deselect behavior;
- stale selection removal;
- selection/caption preservation;
- duplicate target rejection;
- continue-on-error accounting.

Architecture guards cover:

- the loaded-source quick-assembly surface and DI registration;
- use of the existing `OpenWorkbooks` session state;
- delegation to `_transferService.Transfer`;
- current range/header/WorkingData reuse;
- `TargetElementId = null` and `CreatedNewTable` safety;
- absence of QuickAssembly persistence in Domain;
- absence of a second Excel reader/table engine in the orchestrator.

No tests were deleted, skipped or weakened.

## Closure gate

1. Exact current-head Windows:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - 0 warnings / 0 errors;
   - no deleted, skipped or weakened tests.
2. UI smoke:
   - load at least two Excel workbooks;
   - open `Hızlı Rapor`;
   - select three worksheets across the workbooks;
   - assign at least one caption;
   - run `Seçilenleri Rapora Aktar`.
3. Result smoke:
   - three distinct report tables appear in deterministic order;
   - captions number once per semantic table;
   - created/skipped/failed summary is correct;
   - one intentionally invalid source does not block valid targets;
   - Preview/Word preserve existing table/grouping semantics;
   - related warnings still appear in the Sprint 20 warning center.

## Non-goals

- persistent import presets or user memory;
- project migration/versioning work;
- background import scheduling;
- cloud sync;
- direct modification of source Excel files;
- a second report/table composition pipeline;
- silent overwrite of customized report tables;
- updating existing tables from the batch panel.
