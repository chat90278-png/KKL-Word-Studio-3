# Sprint 24 Tranche 09 — Real DOCX / Large Table Matrix

## Stacked baseline

- Base branch: `sprint24/08-project-lifecycle-cleanup`
- Current base head: `615441de02cb81941e71ad92c4a26ebe37d47959`
- Branch: `sprint24/09-real-docx-large-table-matrix`
- The current exact head is recorded in PR #23 and must be confirmed with `git rev-parse HEAD`.
- This tranche adds the real DOCX regression matrix plus one authoritative Word-writer schema correction exposed by that matrix.

PR #22 must be validated and merged first. This branch can be used for one combined Windows run because it contains both Tranche 08 and Tranche 09.

## Added end-to-end DOCX matrix

All scenarios use the authoritative production pipeline:

`ReportContentBuilder -> WordExporter -> OpenXML DOCX`

No fake exporter, second Word writer or duplicated pagination implementation is introduced.

### 100 rows / six columns

- Creates a real static report table with 100 semantic rows and six columns.
- Exports through the real Word exporter.
- Reopens the generated DOCX.
- Verifies one header plus exactly 100 data rows.
- Verifies six cells on every row.
- Verifies stable row keys appear exactly once and in order.

### Native Word pagination properties

- Creates and exports a real 100-row table.
- Verifies the first row carries `TableHeader`.
- Verifies every header/data row carries `CantSplit`.
- Verifies the header and leading rows carry the shared KeepNext start chain.

### Consecutive tables

- Exports two consecutive captioned tables.
- Verifies both tables are present.
- Verifies the second caption appears directly in the between-table flow.
- Verifies no explicit page break or `PageBreakBefore` is inserted between them.

### Long cell text / physical file

- Places a long repeated text value in a real table cell.
- Writes the exporter stream to a physical temporary `.docx` file.
- Reopens that file through `WordprocessingDocument`.
- Verifies marker and content survival.
- Runs `OpenXmlValidator` and requires zero validation errors.

## OpenXML schema correction

The first Windows run after the namespace compile fix passed 149 of 150 Infrastructure tests and exposed one real schema error in `/word/document.xml`.

The invalid node was:

```xml
<w:tblBorders>
  <w:top />
  <w:bottom />
  <w:left />
  <w:right />
  <w:insideH />
  <w:insideV />
</w:tblBorders>
```

`CT_TblBorders` is sequence-sensitive. The authoritative `WordTableWriter` now emits:

```text
top, left, bottom, right, insideH, insideV
```

No table values, widths, pagination rules, row properties or exporter ownership changed. The strict physical-DOCX validator test remains in place to prevent recurrence.

## Expected integrated test inventory

```text
Domain           20
Application     299
Engine           68
Architecture    128
Infrastructure  150
-------------------
Total           665 / 665
```

## Combined Windows gate

The schema correction is committed, but no post-fix Windows result has been supplied yet. Run from `sprint24/09-real-docx-large-table-matrix` to validate both stacked tranches:

```bat
git fetch origin
git checkout sprint24/09-real-docx-large-table-matrix
git reset --hard origin/sprint24/09-real-docx-large-table-matrix

git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

## Manual Word smoke

1. Create/export a report with a 50–100 row table.
2. Open the resulting file in Microsoft Word.
3. Confirm every row appears exactly once and in source order.
4. Confirm header rows repeat on continuation pages.
5. Confirm individual table rows do not split across pages.
6. Confirm caption/header/first data rows do not leave a stranded caption.
7. Confirm two consecutive tables do not produce a blank page.
8. Confirm a long text cell remains readable and does not corrupt the DOCX.
9. Confirm table borders render normally on all four outside edges and inside grid lines.
10. Confirm normal Excel transfer, Preview, diagnostics and Word preflight still work after Tranche 08 cleanup.

No GREEN/ready/merge claim is valid until the exact-head Windows Release gate and manual smoke are supplied.
