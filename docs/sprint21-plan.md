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

Add a compact multi-source selection surface reachable from the existing loaded-source area.

Each loaded workbook is shown with its worksheets:

```text
☑ sero2.xlsx
   ☑ Sayfa1
   ☐ Sayfa2

☑ envanter.xlsx
   ☑ Araçlar
   ☑ Parçalar
```

Acceptance:

- selection state belongs to UI/session state, not Domain persistence;
- newly opened workbooks/sheets appear without recreating a parallel source store;
- workbook-level selection selects/deselects its sheets;
- sheet-level selection remains independently editable;
- unloaded/closed workbooks are removed from the session selection;
- duplicate `(source path, worksheet)` targets cannot appear twice;
- source and worksheet order follows the existing loaded-workbook order and workbook sheet order;
- the currently active workbook/sheet workflow continues to work unchanged.

## P0-B — Deterministic batch transfer orchestration

Add `Seçilenleri Rapora Aktar` for selected sheet targets.

The batch orchestrator must reuse the existing single-sheet transfer path and its real services:

- existing workbook/sheet activation;
- range detection/configuration;
- WorkingData snapshot;
- table creation/binding;
- Serial/Quantity grouping detection;
- Preview refresh;
- automatic caption sequencing.

Do not create a second table import/composition engine.

Acceptance:

- selected targets execute in visible deterministic order;
- one sheet creates at most one new table during a batch invocation;
- the same target cannot be submitted twice in one batch;
- a failure on one target is recorded and remaining targets continue;
- existing customized tables are never silently overwritten;
- if the existing transfer path requires a user choice for an already configured table, batch mode defaults to creating a new table rather than silently replacing it;
- original XLSX/XLSM files remain unchanged;
- one final workspace/Preview refresh is preferred after the batch, unless existing contracts require per-item refresh for correctness.

## P0-C — Compact per-target authoring options

For each selected sheet, provide lightweight session-only options:

- optional raw table caption;
- action: `Yeni tablo` initially;
- order controls or drag ordering only if deterministic source order is insufficient after P0-B;
- skip toggle by deselecting the target.

Do not add a complex wizard or persistent template memory.

Explicit existing-table update is deferred until it can reuse the current safe transfer-choice contracts without weakening overwrite protection.

## P0-D — Batch result summary and diagnostics integration

After execution show a compact result:

```text
3 tablo oluşturuldu
1 kaynak atlandı
2 uyarı bulundu
```

Acceptance:

- success/failure/skip counts are explicit;
- per-target failure message includes workbook and worksheet;
- generated table warnings continue through the Sprint 20 Diagnostics Center;
- clicking a diagnostic keeps existing Preview/Excel cross-navigation behavior;
- no persistent batch history is stored.

## Regression coverage

Add focused tests/guards for:

- session selection uniqueness and deterministic order;
- workbook-level select/deselect behavior;
- stale selection removal when loaded workbooks change;
- duplicate target rejection;
- continue-on-error batch result accounting;
- batch orchestration delegates to the existing single-target transfer seam;
- no Domain persistence fields are introduced for temporary sheet selection;
- current single-sheet `Word'e Aktar` remains available and unchanged during the transition.

## Closure gate

1. Exact current-head Windows:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - 0 warnings / 0 errors;
   - no deleted, skipped or weakened tests.
2. UI smoke:
   - load at least two Excel workbooks;
   - open the quick assembly surface;
   - select three worksheets across the workbooks;
   - assign at least one caption;
   - run batch transfer.
3. Result smoke:
   - three distinct report tables appear in deterministic order;
   - captions number once per semantic table;
   - Preview and Word preserve existing table/grouping semantics;
   - one intentionally invalid source does not block valid targets;
   - related warnings appear in the Sprint 20 warning center.

## Non-goals

- persistent import presets or user memory;
- project migration/versioning work;
- background import scheduling;
- cloud sync;
- direct modification of source Excel files;
- a second report/table composition pipeline;
- silent overwrite of customized report tables.
