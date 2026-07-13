# Sprint 22 — Responsive Hızlı Rapor Tranche

## Status

- Baseline GREEN head: `2d2892cc25e80985220f52839de4e1ed73c0f63f`
- Responsive batch head tested on Windows: `c02d41774186225ff92dbf3bb1dd1b507658582e`
- Release build and all 494 tests passed on that head.
- UI smoke did not pass: the process remained attached after `dotnet run`, but the main window was not visible.
- Startup visibility and diagnostics hardening now continues on top of that evidence.

## Implemented

- Target-level progress before and after every deterministic batch item.
- Visible completed/total progress and current workbook/sheet text.
- Cooperative cancel action.
- Cancellation takes effect inside the current target when its existing safe checkpoints observe the token, or before the next target.
- Completed target results are retained after cancellation.
- No result is fabricated for an incomplete target.
- Successful targets are deselected; failed, skipped, cancelled and not-yet-started targets remain available for retry.
- Transfer, select-all and clear-selection commands are disabled while a batch is active.
- A second batch invocation is rejected while `IsBusy` is true.
- Per-target exceptions still remain isolated and later targets continue unless the user cancels.

## Ownership and safety

`QuickAssemblyBatchOrchestrator` remains an ordering/accounting component. It does not read Excel, create report tables, build Preview content, paginate, or write Word files.

Every real transfer still delegates through:

`QuickAssemblyViewModel`
→ `ExcelWorkspaceViewModel.TransferQuickAssemblyTargetAsync`
→ existing `IExcelReportTransferService`

Cancellation does not roll back already completed table creation. It stops future work and preserves completed results, which is the safe boundary supported by the existing transfer pipeline.

## Windows evidence for responsive head

- `dotnet restore`: SUCCESS
- `dotnet build -c Release`: SUCCESS
- Build warnings: 0
- Build errors: 0
- Domain: 18/18
- Application: 209/209
- Engine: 60/60
- Architecture: 80/80
- Infrastructure: 127/127
- Total: 494/494
- Failed: 0
- Skipped: 0
- UI startup: FAILED because no visible application window appeared.

The responsive head is therefore not marked fully GREEN despite the successful build/test gate.

## Coverage

Application tests cover:

- deterministic start/completion progress order;
- cancellation between targets;
- completed-result retention;
- no execution of later targets after cancellation;
- cancellation inside a target;
- no fabricated result for an incomplete target.

Architecture tests guard:

- visible progress and cancel controls;
- `IsBusy` duplicate-command suppression;
- existing transfer delegation;
- absence of a second Excel reader, OpenXML writer, report builder or Word path.

## Next exact-head Windows gate

The startup-hardening head must pass:

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

Expected test totals:

- Domain: 18
- Application: 209
- Engine: 60
- Architecture: 82
- Infrastructure: 127
- Total: 496

## UI smoke after startup recovery

1. Verify the main window becomes visible and appears in the Windows taskbar.
2. Load at least three worksheets.
3. Select all in `Hızlı Rapor` and start transfer.
4. Verify current workbook/sheet text and progress bar update.
5. Verify transfer/select-all/clear cannot run again while active.
6. Click `İptal` while a target is active.
7. Verify completed tables remain correct and their targets are deselected.
8. Verify remaining targets stay selected and can be retried.
9. Verify Preview and Word output for completed tables remain unchanged.
