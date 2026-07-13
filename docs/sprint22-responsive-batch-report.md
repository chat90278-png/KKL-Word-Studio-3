# Sprint 22 — Responsive Hızlı Rapor Tranche

## Status

- Baseline GREEN head: `2d2892cc25e80985220f52839de4e1ed73c0f63f`
- Responsive batch implementation landed after that baseline.
- The exact branch head returned by `git rev-parse HEAD` requires Windows Release build/test and UI smoke before GREEN.

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

## Exact-head Windows gate

```bat
git checkout sprint22/release-readiness-big-data
git pull
git rev-parse HEAD

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

The tested SHA must be copied from `git rev-parse HEAD`; do not infer GREEN from an earlier implementation commit. The `dotnet run` command only proves launch; the Hızlı Rapor cancellation behavior still requires the manual UI smoke below.

Expected test totals if no other changes land:

- Domain: 18
- Application: 209
- Engine: 60
- Architecture: 80
- Infrastructure: 127
- Total: 494

## UI smoke

1. Load at least three worksheets.
2. Select all in `Hızlı Rapor` and start transfer.
3. Verify current workbook/sheet text and progress bar update.
4. Verify transfer/select-all/clear cannot run again while active.
5. Click `İptal` while a target is active.
6. Verify completed tables remain correct and their targets are deselected.
7. Verify remaining targets stay selected and can be retried.
8. Verify Preview and Word output for completed tables remain unchanged.
