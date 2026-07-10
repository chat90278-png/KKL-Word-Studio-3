# Sprint 9 Stabilization Report

## Baseline

Baseline: `KKL.WordStudio-Sprint9.zip` only.

No Sprint 8 or older workspace was used for implementation.

## Windows Ground Truth Received

Before stabilization:

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED — 0 warnings, 3 errors
- `Sprint9WorkingDataTests.cs`: CS0104 `TableColumn` ambiguity at two report-column constructions
- `ExcelWorkspaceViewModel.cs`: CS0103 `File` not found around `IsSourceMissing`
- Domain Tests: 16 passed, 0 failed
- Application Tests: 32 passed, 0 failed
- Infrastructure tests could not compile because of the `TableColumn` ambiguity
- UI build could not complete because of the `File` compile error
- NETSDK1057 is treated only as the supplied preview SDK notice, not as the compile failure

## Compile Fixes Applied

1. `tests/KKL.WordStudio.Infrastructure.Tests/Sprint9WorkingDataTests.cs`
   - Added `DomainTableColumn = KKL.WordStudio.Domain.Elements.TableColumn` alias.
   - Changed only the two report-table column constructions in `CreateBoundReport` to `DomainTableColumn`.
   - Kept `DocumentFormat.OpenXml.Spreadsheet` imports used by workbook fixtures.
   - Preserved the Sprint 9 Preview/Word consistency test and existing `TableColumn` model.

2. `src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs`
   - Changed the `IsSourceMissing` file check to `System.IO.File.Exists(SelectedWorkbook.FilePath)`.
   - Missing-source behavior and working-data semantics were not changed.

No product feature, Sprint 10 work, shell redesign, working-data refactor, provider refactor, mapping redesign, or report-content pipeline change was added.

## Preservation

The following source semantics were left unchanged:

- original XLSX/XLSM read-only behavior;
- project-owned `Worksheet.WorkingData` persistence;
- working-data-first / original-Excel-fallback provider precedence;
- worksheet isolation;
- `Binding.WorksheetName`;
- worksheet-owned `Worksheet.ColumnMappings`;
- `TableColumn.SourceField` separate from display `Header`;
- shared `IDataProvider` / `ReportContentDocument` Preview and Word data path;
- Sprint 8 front-matter and table-caption behavior.

Static test inventory comparison found all 79 baseline test methods still present and no baseline test method removed.

## Verification

Actual commands attempted in the stabilization workspace:

### `dotnet restore`

Result: NOT EXECUTED SUCCESSFULLY

Output:

`dotnet: command not found`

Exit code: `127`

### `dotnet build`

Result: NOT EXECUTED SUCCESSFULLY

Output:

`dotnet: command not found`

Exit code: `127`

### `dotnet test`

Result: NOT EXECUTED SUCCESSFULLY

Output:

`dotnet: command not found`

Exit code: `127`

This environment cannot prove a green build or test result. Windows/.NET 8 re-verification remains required and is the final runtime truth.

Available static verification performed:

- baseline-vs-stabilized source diff before report creation: exactly 2 modified source files;
- 79 baseline test methods found and 79 retained;
- 0 missing baseline test methods;
- 21 XML/XAML/project files parsed with 0 XML parse errors;
- no remaining `new TableColumn` construction in the reported ambiguous Sprint 9 test location;
- no unqualified `File.Exists` use remains in `ExcelWorkspaceViewModel`;
- no `bin/` or `obj/` directories are present.

## Files Changed

### Added

- `docs/sprint9-stabilization-report.md`

### Modified

- `src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint9WorkingDataTests.cs`

### Removed

None.

## Remaining Blocker

No additional source-level blocker was identified by the available static checks after the two supplied Windows compile fixes.

The remaining blocker is verification: this environment has no `dotnet` CLI, so the stabilized candidate still requires real Windows/.NET 8 `dotnet restore`, `dotnet build`, and `dotnet test` output before green status can be claimed.
