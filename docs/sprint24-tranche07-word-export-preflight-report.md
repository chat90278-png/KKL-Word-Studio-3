# Sprint 24 Tranche 07 — Word Export Preflight

## Baseline

- Base: `main@f7095c0ca7f883ec3805f5f7fb0a6817be655d9a`
- Branch: `sprint24/07-word-export-preflight`
- Tranche 06 structured diagnostics is the only readiness input.
- Closed/unmerged PR #18 is not reused or cherry-picked.

## Goal

Make the global `Word Dosyası Oluştur` action consume the latest structured diagnostic snapshot before opening a file dialog or invoking the existing exporter.

## Readiness policy

`WordExportPreflightPolicy` is Application-owned and consumes `PreviewDiagnosticGroup` values only.

- No diagnostic groups: `Ready`.
- Warning and/or Information groups without Error: `ReadyWithFindings`.
- Any Error group: `Blocked`.

The policy counts both semantic problem groups and raw occurrences. It does not inspect localized messages, re-read Excel, rebuild report content, or invoke the exporter.

## Shell behavior

### Blocking Error

- Word export stops before `SaveWordFile`.
- The report pane is revealed.
- The existing Context Dock opens its existing `Warnings` page.
- `DockViewModel` publishes a blocking-error request and the existing `WarningCenterViewModel` switches to its Error filter, so a prior Warning/Information filter cannot hide the blocker.
- Status text reports blocking group and open-error counts.
- The exporter is not resolved or invoked.
- No new popup, Control Center, or duplicate warning surface is introduced.

### Warning / Information only

- The existing Save dialog and existing DOCX exporter continue normally.
- A successful export status retains the non-blocking problem-group and open-finding counts.
- Warnings remain actionable through the existing Warning Center cards and source navigation.

### Clean report

- Existing Word export behavior and success wording remain unchanged.

## Architecture boundaries

- Existing `PreviewDiagnosticsStore` remains the runtime bridge.
- Existing `PreviewDiagnosticSummaryService` remains the single grouping implementation.
- Existing `IReportExporterRegistry` and DOCX exporter remain authoritative.
- `MainViewModel` keeps its existing constructor contract; it depends only on its existing `DockViewModel` for Warning Center reveal routing.
- No second validator, Excel reader, report-content builder, renderer, paginator, or Word writer.
- No Domain or persistence change.
- No rejected PR #18 UI is reintroduced.

## Regression coverage

Application tests verify:

1. an empty snapshot is ready;
2. Warning and Information findings remain visible but non-blocking;
3. any Error group blocks and preserves group/finding counts.

Architecture coverage verifies:

- evaluation happens before the Save dialog and exporter;
- blocked export reveals the report pane and existing Warnings page;
- blocking reveal forces the existing Error filter;
- MainViewModel constructor ownership remains unchanged;
- policy consumes structured groups/severity without message parsing;
- MainViewModel does not introduce a MessageBox or reclassify diagnostics.

## Expected test inventory

- Domain: `20`
- Application: `299`
- Engine: `68`
- Architecture: `127`
- Infrastructure: `146`

Expected total:

```text
660 / 660
```

## Required Windows gate

```bat
git fetch origin
git checkout sprint24/07-word-export-preflight
git reset --hard origin/sprint24/07-word-export-preflight

git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

- exact branch head reported by PR;
- empty `git status --short`;
- build `0 warnings / 0 errors`;
- tests `660/660`.

## Required manual smoke

1. Clean report: click `Word Dosyası Oluştur`; Save dialog opens and DOCX export succeeds with the normal success status.
2. Warning-only report: use the warning scenario workbook; Save dialog still opens, DOCX exports, and success status includes current non-blocking group/finding counts.
3. Blocking source error: make an existing Excel source unavailable, let Preview publish `Kaynak veriye erişilemedi`, then click `Word Dosyası Oluştur`.
4. Before clicking export, select the Warning or Information filter; confirm blocked export still switches the existing Warning Center to Error.
5. Confirm no Save dialog opens, no DOCX is created, the report pane opens, and the existing Uyarılar page is visible with Error cards.
6. Restore the source and refresh; confirm the Error disappears and Word export is permitted again.
7. Confirm warning-card navigation and the accepted Warning Center layout remain unchanged.

## Gate status

- Static source review: complete.
- Exact-head Windows Release build/test: pending.
- Manual clean/warning/error export smoke: pending.
- PR must remain draft until both gates are supplied.
