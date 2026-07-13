# Sprint 22 — Tranche 3: Excel Read-Path and Large-Preview Hardening

## Baseline

- Base branch: `main`
- Base commit: `52cdd246cfa349862b8f11c138a1ae31d499f54b`
- Working branch: `sprint22/tranche3-excel-read-hardening`
- Previous Sprint 22 tranche is Windows GREEN: Release build 0 warnings / 0 errors, 496/496 tests, visible UI launch and Hızlı Rapor progress/cancel smoke.

## Proven risks from source review and Windows smoke

`OpenXmlExcelWorkbookReader.ReadWorkingDataAsync` originally:

1. scanned the complete sheet once through `ResolveMaxColumn` when `EndColumn` was absent;
2. enumerated all indexed rows into a full `Dictionary<int, Row>` to locate one header row;
3. enumerated the sheet again to create WorkingData rows;
4. exposed a `Task` API while the OpenXML traversal was still synchronous;
5. observed cancellation only in the final data-row loop.

A real roughly 10,000-row workbook then produced a correct 1,455-page Preview, but Windows smoke showed UI stalls. The first overlay attempt was incomplete because it covered only Preview projection and did not guarantee a WPF render before heavy work.

## Tranche objective

- Remove repeated complete-sheet scans from WorkingData creation.
- Move all OpenXML read paths off the WPF UI thread.
- Provide one in-window loading shield for Excel, transfer and Preview operations.
- Keep existing Excel, transfer, Preview, layout and Word ownership boundaries intact.

## Implemented order

1. Added focused behavior and cancellation tests.
2. Replaced full-sheet max-column scan and full row lookup with one relevant-row traversal.
3. Used the explicit configured end column as the streaming fast path.
4. When the end column is not configured, discovered it during the same relevant-row traversal and buffered only rows that become WorkingData.
5. Added cancellation checks before file access and during row/projection loops.
6. Converted workbook-open, sheet-preview and AutoRange OpenXML work to real background tasks.
7. Added one shell-level long-operation state and full-window interaction shield.
8. Added transparent decorators around the existing OpenXML reader and transfer service.
9. Forced one WPF DataBind/Render turn before heavy work when the main window is visible.
10. Kept dispatcher pumping below Input priority to avoid re-entry.
11. Kept Preview projection cancellable and published pages to the UI in batches of 25.
12. Preserved source-file read-only integrity and all existing engine seams.

## Acceptance criteria

- Existing 496 tests remain GREEN.
- New tests cover implicit end-column discovery, sparse cells, header extraction, source hash integrity and pre-cancelled OpenXML paths.
- No `ResolveMaxColumn` pre-scan remains in WorkingData creation.
- No full `rowLookup` dictionary remains.
- Data rows and columns are neither truncated nor reordered.
- Source XLSX/XLSM bytes remain unchanged.
- Cancellation is rethrown as `OperationCanceledException`, not converted into a generic failure result.
- No second OpenXML reader, transfer engine, Preview renderer, layout engine or Word writer is introduced.
- Loading overlay appears for Excel open, sheet preview, AutoRange, WorkingData, `Word'e Aktar` and Preview rebuild.
- Underlying controls cannot be clicked while the shield is active.
- 1,000+ page Preview publication yields regularly to the WPF dispatcher.
- Startup DI does not pump a dispatcher before the main window is visible.

## Validation gate

This head is not GREEN until exact-head Windows evidence confirms:

- Release build: 0 warnings / 0 errors;
- Domain: 18;
- Application: 209;
- Engine: 60;
- Architecture: 88;
- Infrastructure: 134;
- Total: 509;
- visible overlay during Excel and transfer flows;
- successful completion and cancellation recovery with the roughly 10,000-row / 1,455-page scenario;
- unchanged XLSX hash.
