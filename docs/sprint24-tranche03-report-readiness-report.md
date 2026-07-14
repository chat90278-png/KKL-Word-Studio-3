# Sprint 24 Tranche 03 — Report Readiness and Consolidated Diagnostics

## Baseline

- Base: `main@5b907216f19d1bf1ca691efa0ceb09874196ac34`
- Branch: `sprint24/03-report-readiness`
- Previous Windows gate: build `0/0`, tests `617/617`, Quick Report smoke GREEN

## Product behavior

The former `Uyarılar` page is now the report `Kontrol` center.

### Diagnostic consolidation

Preview remains the only diagnostic producer. Repeated row-level findings are grouped by:

- severity;
- report element id;
- diagnostic title/family;
- report element name.

The first human-readable message and representative navigation key are retained. Source candidates are deduplicated. Every group carries an `OccurrenceCount`, so hundreds of row findings no longer create hundreds of cards.

Example:

```text
Before: 806 warning cards
After:  4 warning groups · 806 findings
```

The exact group count depends on the current report, affected tables and diagnostic families.

### Severity-aware control center

Each card now shows:

- `Hata`, `Uyarı` or `Bilgi` severity;
- occurrence count such as `247 kez`;
- affected report element;
- representative key when available;
- source workbook, worksheet and range;
- direct Preview/Excel navigation.

Grouped findings preserve a representative key and label it as `Örnek anahtar`, allowing the existing Excel navigation path to open a concrete example row.

The Context Dock badge shows the number of diagnostic groups, not the raw repeated finding total. Error groups use a red badge; non-error findings use amber.

### Word export readiness gate

`Word Dosyası Oluştur` reads the current consolidated Preview diagnostics:

- **Error present:** export is blocked; the report pane opens and switches to `Kontrol`; the user receives a critical-error message.
- **Warnings only:** export is allowed after one explicit confirmation.
- **Information only / no findings:** export proceeds normally.

Errors, warning groups and occurrence totals are counted separately. The exporter, Preview renderer and report engine are unchanged.

## Architecture

- `PreviewDiagnosticFactory` remains the only producer.
- `PreviewDiagnosticConsolidator` is a projection-only Application helper.
- `PreviewDiagnosticsStore` publishes consolidated runtime state.
- `ReportReadinessAssessment` computes severity counts from the same diagnostics stream.
- `MainViewModel` consumes the assessment before invoking the existing DOCX exporter.
- No second validator, renderer, report tree or exporter was introduced.
- Diagnostics remain runtime-only and are never persisted into Domain/Project.

## Regression coverage

New Application tests: `+4`

- key-specific row messages consolidate by semantic issue family;
- different severity/element groups remain separate and error-first;
- errors block readiness;
- warnings require confirmation but are not hard blockers.

New Architecture tests: `+1`

- control center reuses Preview diagnostics and gates Word export by severity.

The Sprint 20 diagnostics architecture guard was evolved from the old `Uyarılar` label to the accepted `Kontrol` label without weakening navigation or ownership checks.

Expected Windows total:

```text
622 / 622
```

## Windows gate

```bat
dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

```text
0 warnings
0 errors
622 / 622 tests
```

## Manual smoke

1. Open a report that previously showed a very large warning count.
2. Confirm the `Kontrol` badge shows grouped issue count rather than raw row count.
3. Open `Kontrol`; confirm the header shows error/warning/information groups and total findings.
4. Confirm repeated findings show an occurrence badge.
5. Click a grouped card; confirm Preview selects the affected table and Excel opens a representative key/source.
6. With errors present, click `Word Dosyası Oluştur`; confirm export is blocked and `Kontrol` opens.
7. With warnings but no errors, confirm the continuation dialog appears and export proceeds only after approval.
8. With no errors/warnings, confirm Word export remains unchanged.
