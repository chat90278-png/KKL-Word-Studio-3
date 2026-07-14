# Sprint 24 Tranche 04 — Word Export Readiness Gate

## Baseline

- Base: `main@493ccc0ddbf3a032b90c310fa3743cb475fb17d9`
- Branch: `sprint24/04-word-export-readiness`
- Existing grouped Control center remains authoritative.
- Previous merged test inventory: `625/625` expected.

## Product behavior

`Word Dosyası Oluştur` now checks the same grouped diagnostics shown in the `Kontrol` center before opening the save dialog.

### Error groups

When one or more `Error` groups exist:

- Word export is blocked;
- the report pane opens automatically;
- the Context Dock switches to `Kontrol`;
- the user sees the number of critical groups and underlying error occurrences;
- the save-file dialog and DOCX exporter are not invoked.

### Warning groups

When warnings exist but no errors exist:

- warnings do not hard-block export;
- one confirmation dialog shows warning-group and occurrence counts;
- approving continues through the existing DOCX exporter;
- cancelling leaves the report unchanged and does not open a save dialog.

### Information-only / clean report

Information groups do not require confirmation. A report with only information findings—or no findings—exports exactly as before.

## Architecture

- `PreviewDiagnosticFactory` remains the raw diagnostic producer.
- `PreviewDiagnosticSummaryService` remains the sole grouping projection.
- `PreviewDiagnosticsStore.Groups` remains the shared Control-center state.
- `ReportReadinessAssessment` is a pure Application projection over those existing groups.
- `MainViewModel` applies the decision before the existing file-dialog/export pipeline.
- No second validator, Preview renderer, report tree, Word exporter or persistence model was introduced.

## Regression coverage

New Application tests: `+3`

- group and occurrence counts by severity;
- errors hard-block export and suppress warning confirmation;
- warnings require confirmation while information-only reports do not.

New Architecture tests: `+1`

- Word export consumes `PreviewDiagnosticsStore.Groups`, opens the existing Control center for errors and reuses the existing exporter path.

Expected Windows total:

```text
629 / 629
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
629 / 629 tests
```

## Manual smoke

1. Open a report with at least one red `Hata` group.
2. Click `Word Dosyası Oluştur`.
3. Confirm no save dialog appears, the report pane opens and `Kontrol` becomes active.
4. Use a report with warnings but no errors.
5. Confirm one continuation dialog appears with grouped warning/occurrence counts.
6. Cancel once and confirm no file is produced.
7. Approve once and confirm the existing DOCX output is produced.
8. Use an information-only or clean report and confirm export proceeds without readiness prompts.
