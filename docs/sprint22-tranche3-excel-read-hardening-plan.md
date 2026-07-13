# Sprint 22 — Tranche 3: Excel Read-Path Hardening

## Baseline

- Base branch: `main`
- Base commit: `52cdd246cfa349862b8f11c138a1ae31d499f54b`
- Working branch: `sprint22/tranche3-excel-read-hardening`
- Previous Sprint 22 tranche is Windows GREEN: Release build 0 warnings / 0 errors, 496/496 tests, visible UI launch and Hızlı Rapor progress/cancel smoke.

## Proven risks from source review

`OpenXmlExcelWorkbookReader.ReadWorkingDataAsync` currently:

1. scans the complete sheet once through `ResolveMaxColumn` when `EndColumn` is absent;
2. enumerates all indexed rows into a full `Dictionary<int, Row>` to locate one header row;
3. enumerates the sheet again to create WorkingData rows;
4. exposes a `Task` API while the OpenXML traversal is still synchronous;
5. observes cancellation only in the final data-row loop.

## Tranche objective

Remove repeated complete-sheet scans from WorkingData creation while preserving exact row/column shape, headers, sparse cells, deterministic order and source-file integrity.

## Implementation order

1. Add focused behavior and cancellation tests.
2. Replace full-sheet max-column scan and full row lookup with one relevant-row traversal.
3. Use the explicit configured end column as the streaming fast path.
4. When the end column is not configured, discover it during the same relevant-row traversal and buffer only rows that become WorkingData.
5. Observe cancellation before file access and during every row traversal.
6. Preserve the existing reader interface and all Excel/transfer/Preview ownership boundaries.
7. Run exact-head Windows restore/build/test and UI smoke.

## Acceptance criteria

- Existing 496 tests remain GREEN.
- New tests cover implicit end-column discovery, sparse cells, header extraction, pre-cancelled calls and source hash integrity.
- No `ResolveMaxColumn` pre-scan remains in WorkingData creation.
- No full `rowLookup` dictionary remains.
- Data rows and columns are neither truncated nor reordered.
- Source XLSX/XLSM bytes remain unchanged.
- Cancellation is rethrown as `OperationCanceledException`, not converted into a generic failure result.
- No second Excel reader or data-transfer path is introduced.

## Non-goals for this tranche

- changing AutoRange semantics;
- changing Preview row limits;
- changing report transfer behavior;
- changing Word/Preview rendering;
- adding hard performance thresholds before representative Windows measurements are captured.
