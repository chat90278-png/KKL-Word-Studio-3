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

Warnings do not block Word creation. The user receives one `Devam Et / Kontrol Et / İptal` decision.

### Information

- `TABLE_SPLIT`
- `ROWS_SKIPPED`
- `DEFAULT_TITLE_USED`

Information findings require no confirmation and never block export.

## Counting semantics

The Control center now separates three meanings:

- **Bulgu sayısı:** raw diagnostic occurrences, such as two conflicting fields;
- **Benzersiz kayıt anahtarı:** distinct Excel records represented by the group;
- **Gezilebilir hedef:** keys that navigation can actually visit.

Two conflicts on the same record are therefore shown as:

```text
2 bulgu
1 benzersiz kayıt anahtarı
1 kayıt anahtarında 2 çelişkili değer bulundu.
```

Because there is only one target, the card shows `Kayda Git`. `İlk Kayda Git` and `Sonraki` appear only when there are multiple distinct targets.

## Control center

- The dock tab is named `Kontrol` because it contains Error, Warning and Information findings.
- The header explicitly states `Word'e hazır` or `Word'e hazır değil`.
- Filters visibly read `Tümü`, `Hata`, `Uyarı`, `Bilgi`; each has its own count badge.
- Cards display severity, stable code, affected table/column, finding count, distinct-key count and examples.
- Raw technical messages remain in runtime diagnostics; user cards show a readable grouped explanation.
- `Kayda Git` is used for one target.
- `İlk Kayda Git` and wrapping `Sonraki` are used for multiple targets.
- `Tümünü Göster` distinguishes findings, affected rows and unique keys.
- Fixing source data and refreshing Preview naturally removes resolved findings; no manual resolved flag is persisted.

## Word decision flow

### Error

- export is blocked;
- save dialog is not opened;
- report pane opens and `Kontrol` becomes active;
- the first related report element is selected.

### Warning

A three-way decision is shown:

- `Devam Et`: use the existing Word save/export pipeline;
- `Kontrol Et`: open `Kontrol` and stop before the save dialog;
- `İptal`: stop without changing the report.

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

## Expected Windows gate

The final wording/navigation correction changes no test-method count. Expected total remains:

```text
635 / 635
```

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

1. Confirm the filter row reads `Tümü 2 / Hata 0 / Uyarı 2 / Bilgi 0`, not four unlabeled numbers.
2. For one unique key with multiple findings, confirm the card says `bulgu`, shows one unique record key and only `Kayda Git`.
3. For multiple unique keys, confirm `İlk Kayda Git` and wrapping `Sonraki` appear.
4. Confirm `Tümünü Göster` distinguishes findings, affected rows and unique keys.
5. Confirm Error blocks Word export and Warning opens the explicit three-button decision window.
