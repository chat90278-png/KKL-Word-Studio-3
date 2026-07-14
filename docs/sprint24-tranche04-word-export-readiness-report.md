# Sprint 24 Tranche 04 — Stable Control Catalog and Word Readiness

## Baseline

- Base: `main@493ccc0ddbf3a032b90c310fa3743cb475fb17d9`
- Branch: `sprint24/04-word-export-readiness`
- Previous Windows gate: build `0/0`, tests `629/629`, startup GREEN.
- Existing Preview diagnostics, grouped Control center and DOCX exporter remain authoritative.

## Stable diagnostic catalog

Every new Preview diagnostic carries a stable code, severity and optional affected-column/row metadata. Grouping no longer depends on user-facing message wording for catalogued findings.

### Blocking errors

- `SRC_FILE_MISSING`
- `SRC_SHEET_MISSING`
- `SRC_RANGE_INVALID`
- `COLUMN_REQUIRED_MISSING`
- `SRC_ACCESS_ERROR`

These prevent Word creation, open the report pane and switch to `Kontrol` before any save dialog or exporter is invoked.

### Confirmable warnings

- `QUANTITY_INVALID`
- `SERIAL_DUPLICATE`
- `MERGE_CONFLICT`
- `ROWS_NOT_MERGED`
- `EMPTY_CELLS`
- `TABLE_DATA_WARNING`
- `TABLE_TOO_WIDE`
- `LAYOUT_WARNING`

Warnings do not block Word creation. The user receives one Continue / Review / Cancel decision showing warning-type and affected-record totals.

### Information

- `TABLE_SPLIT`

Information findings require no confirmation and never block export.

## Control center

- The dock tab is named `Kontrol` because it contains Error, Warning and Information findings.
- The header explicitly states `Word'e hazır` or `Word'e hazır değil`.
- Filters are labeled `Tümü`, `Hata`, `Uyarı`, `Bilgi` with grouped counts.
- Cards display severity, stable code, affected table/column, occurrence count, distinct-key count and examples.
- Raw technical messages remain in runtime diagnostics; user cards show a readable grouped explanation.
- `İlk Kayda Git` opens the report element and first Excel occurrence.
- `Sonraki` cycles through distinct keys and wraps to the beginning.
- Fixing source data and refreshing Preview naturally removes resolved findings; no manual resolved flag is persisted.

## Word decision flow

### Error

- export is blocked;
- save dialog is not opened;
- report pane opens and `Kontrol` becomes active;
- group and occurrence totals are shown.

### Warning

A three-way decision is shown:

- Continue: use the existing Word save/export pipeline;
- Review: open `Kontrol` and stop before the save dialog;
- Cancel: stop without changing the report.

### Information / clean

Export continues directly through the existing DOCX exporter.

## Architecture

- `PreviewDiagnosticCatalog` classifies the existing raw warnings; it is not a second validator.
- `PreviewDiagnosticFactory` remains the only raw diagnostic producer.
- `PreviewDiagnosticSummaryService` groups catalogued findings by code + element + affected column.
- Legacy/unclassified findings retain normalized-message grouping for backward compatibility.
- `PreviewDiagnosticsStore.Groups` remains shared by the Control center and readiness gate.
- `ReportReadinessAssessment` is a pure Application projection.
- Domain, persistence, Preview layout, Excel reader and Word exporter are unchanged.

## Test delta

Existing Tranche 04 tests: Application `+3`, Architecture `+1`.

Additional catalog/Control coverage:

- Application `+5`;
- Architecture `+1`.

Expected total:

```text
635 / 635
```

## Windows gate

```bat
dotnet clean
dotnet restore
dotnet build
dotnet test
dotnet run --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

```text
0 warnings
0 errors
635 / 635 tests
```

## Manual smoke

1. Create the current invalid-quantity example and confirm one `QUANTITY_INVALID` card replaces hundreds of cards.
2. Confirm the card says affected records, `Adet` column and distinct-key count.
3. Use `İlk Kayda Git`, then `Sonraki`; verify Excel selection moves through sample keys and wraps.
4. Confirm the tab and collapsed tooltip say `Kontrol`.
5. Click `Word Dosyası Oluştur` with warnings only:
   - Continue produces Word;
   - Review opens `Kontrol` without opening the save dialog;
   - Cancel stops.
6. Use a broken source and confirm a red Error card blocks export before the save dialog.
7. Use an information-only or clean report and confirm export proceeds without readiness prompts.
