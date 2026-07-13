# Sprint 23 Tranche 01 — Excel Range Intelligence Report

## Baseline

- Base branch: `main`
- Base SHA: `d49166dd9cbf237411759c0a207ef64f2b531719`
- Working branch: `sprint23/01-excel-range-intelligence`

## Scope implemented

### Semantic header recognition

- Added one canonical Turkish/English header matcher.
- Recognised roles:
  - No / Number
  - Part Name English
  - Part Name Turkish
  - Part Number
  - NSN
  - Serial Number
  - Quantity
- Matching is case-insensitive and ignores punctuation, spacing and Turkish diacritics.
- Unknown columns remain unknown and available for manual selection.
- The existing `ExcelDataRangeDetector` now scores semantic header matches together with row-shape evidence.
- Detected semantic roles are returned on the range candidate for the next transfer-selection tranche.

### Data-end detection

- Removed the single-blank-row stop rule.
- One to four blank rows inside a dataset are tolerated.
- Five consecutive blank rows form a deliberate end boundary.
- Formula cells count as meaningful even when no cached display value exists.
- Style-only/format-only cells do not extend the data range.

### Source-grid row coverage

- Removed the default 100-row preview cap.
- The existing OpenXML reader remains the only workbook reader.
- Explicit bounded preview reads remain available for tests/specialised callers.
- Formatting-only empty tails are not projected into the grid.
- Five blank visual rows are appended after the final meaningful source row so the user can see the data has ended.

### Manual range persistence

- Applying a manual range persists `SelectedRange` on the project-owned workbook/worksheet configuration.
- Persistence works even before WorkingData is created.
- Different worksheets in the same workbook retain independent ranges.
- Existing WorkingData is not discarded when the range metadata is updated.
- An unaccepted automatic candidate is not persisted as a manual range.

## Architecture constraints preserved

- No second Excel reader.
- No second data-range detector.
- No source workbook write.
- No forced WorkingData creation for range-only configuration.
- No Preview/Word renderer changes in this tranche.

## Test delta

Baseline after Tranche 00: `525`

Expected net additions:

- Application: `+21`
- Architecture: `+3`
- Infrastructure: `+3`
- Expected total: `552`

Expected project totals:

- Domain: `18`
- Application: `234`
- Engine: `60`
- Architecture: `101`
- Infrastructure: `139`
- Total: `552`

## Windows gate — pending

Run against the exact final branch head:

```bat
git fetch origin
git checkout sprint23/01-excel-range-intelligence
git reset --hard origin/sprint23/01-excel-range-intelligence
git rev-parse HEAD
git status --short

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

- 0 warnings;
- 0 errors;
- `552/552` tests;
- application startup smoke GREEN;
- source grid passes 100 rows;
- one internal blank row no longer truncates the detected range;
- manual range survives source/worksheet switching.

## Explicitly not included

The new `Word'e Aktar` placement-confirmation popup is not part of Tranche 01. It depends on the fixed transfer schema and report numbering/structure services and is scheduled across Tranche 02 and Tranche 03.
