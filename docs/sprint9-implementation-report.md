# Sprint 9 Implementation Report

## Architecture / Model Decision

Sprint 9 adds one UI-independent, serializable worksheet model: `WorksheetWorkingData` on `Worksheet`.

- `WorkingDataColumn` keeps ordered position plus stable `Id` / `SourceField` identity and optional original Excel column alias.
- `WorkingDataRow` keeps ordered cell values and optional original Excel row number.
- `Worksheet.WorkingData == null` means the original read-only workbook remains authoritative.
- Working data is created lazily by `IWorksheetWorkingDataService` on the first edit, clear, row/column mutation, or paste.
- `Worksheet.ColumnMappings` remains the primary mapping owner; the pre-Sprint-8 `ExcelDataSource.ColumnMappings` path remains compatibility fallback only.
- No persistent WPF `DataTable` model and no generic dataset engine were introduced.

The known Windows build candidates supplied for this continuation were fixed narrowly:

- `WorksheetMappingIndependenceTests.cs`: added the existing OpenXML root namespace for `SpreadsheetDocumentType` and aliased the Domain `DataField` type.
- `PreviewView.xaml`: removed invalid `BorderDashArray` from the existing front-matter placeholder border; no custom control or shell redesign.
- `ExcelDataProvider.cs`: worksheet/range lookup now uses guarded non-null locals before mapping access.
- `Sprint8PersistenceAndCompositionTests.cs`: page-break assertion now uses analyzer-supported `Assert.Contains(..., predicate)` with the same behavioral check.

The previous Sprint 8 `ReportEditingService` stabilization fixes already present in the Sprint 9 baseline were not broadened or reimplemented.

## Implemented Features

- Editable Excel Workspace cell commit into project-owned working data.
- Clear selected cells.
- Insert/delete ordered rows.
- Insert/delete ordered columns with stable `SourceField` identity.
- Explicit Turkish rejection when a report table still references a column selected for deletion.
- Copy selected cells as normal tab/newline text.
- `Ctrl+C`, `Ctrl+V`, and `Delete` grid gestures.
- Rectangular tab/newline clipboard paste from the active cell; overflow is rejected with a short Turkish status instead of auto-growing the dataset.
- Worksheet-level `Değiştirildi` / `Kaynak veri` / `Kaynak Excel bulunamadı` state.
- `Kaynak Veriye Dön` reset with discard confirmation.
- Existing `Word'e Aktar` flow now carries ordered working-data column identity when a worksheet has been edited.

WPF code-behind only routes grid gestures/clipboard and cancels the temporary `DataTable` cell commit. Product mutation is performed by the ViewModel through `IWorksheetWorkingDataService`.

## Provider + Preview / Word Consistency

`ExcelDataProvider` precedence is now:

1. selected worksheet `WorkingData`, when present;
2. original XLSX/XLSM through the existing read-only OpenXML path.

Working-data rows expose stable `SourceField`, original Excel column alias when available, and the worksheet mapping target name when mapped. This keeps existing raw-column and mapped `TableColumn.SourceField` bindings usable without moving mapping ownership away from the worksheet.

Every successful working-data mutation calls the shared report-content refresh. Preview and Word therefore continue through the existing `IDataProvider -> ReportContentBuilder -> ReportContentDocument` pipeline; no preview-only edited state or duplicate UI binding engine was added. Report Preview table cells remain read-only.

`EditedWorkingData_IsReflectedInReportContentDocument` was added as the hard consistency test source: it edits working data, resolves it through a real `ExcelDataProvider` / `DataProviderRegistry` / `ReportContentBuilder`, asserts the edited value in `TableContentNode`, exports through `WordExporter`, reopens the DOCX with OpenXML, and asserts the same value in Word body content.

This test is present but is **not claimed as passing locally**, because the local environment has no `dotnet` CLI.

## Persistence / Reset / Missing Source

`Worksheet.WorkingData` is ordinary project JSON state inside the existing `.kws` persistence flow. Its optional property preserves Sprint 8 project compatibility; projects with no working data continue to deserialize with original-source fallback semantics. Existing `PreferredObjectCreationHandling.Populate`, polymorphism, `DataRange.RangeReference` ignore behavior, and front-matter ZIP asset handling were not changed.

Reset behavior:

- asks for confirmation when current worksheet working data will be discarded;
- sets only the current worksheet `WorkingData` to `null`;
- rereads the original workbook preview;
- refreshes shared report content;
- never writes the source workbook.

Missing-source behavior:

- persisted working data remains provider-authoritative and usable even when the original workbook path no longer exists;
- the workspace can reconstruct the workbook/sheet selector from persisted project metadata when working data exists;
- Turkish status shows `Kaynak Excel bulunamadı`;
- reset/reload is disabled;
- working data is never silently deleted;
- full relink remains out of scope.

All working-data source reads use `SpreadsheetDocument.Open(..., false)`.

## Files Changed

Added:

- `src/KKL.WordStudio.Application/WorkingData/WorksheetWorkingDataService.cs`
- `src/KKL.WordStudio.Domain/DataSources/WorksheetWorkingData.cs`
- `tests/KKL.WordStudio.Application.Tests/WorksheetWorkingDataServiceTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint9WorkingDataTests.cs`

Modified:

- `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `src/KKL.WordStudio.Application/Excel/IExcelWorkbookReader.cs`
- `src/KKL.WordStudio.Application/Transfer/ExcelReportTransferService.cs`
- `src/KKL.WordStudio.Application/Transfer/ExcelTransferRequest.cs`
- `src/KKL.WordStudio.Domain/DataSources/Worksheet.cs`
- `src/KKL.WordStudio.Infrastructure/Excel/ExcelDataProvider.cs`
- `src/KKL.WordStudio.Infrastructure/Excel/OpenXmlExcelWorkbookReader.cs`
- `src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint8PersistenceAndCompositionTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/WorksheetMappingIndependenceTests.cs`
- `docs/sprint9-implementation-report.md`

Removed: none.

## Tests Added / Preserved

Added focused test sources:

- `WorkingData_IsCreatedWithoutModifyingOriginalWorkbook`
- `WorkingData_CellEdit_IsUsedByExcelDataProvider`
- `WorkingData_IsIsolatedPerWorksheet`
- `WorkingData_RoundTripsThroughProjectPersistence`
- `WorkingData_RowInsertDelete_PreservesOrder`
- `WorkingData_ColumnInsertDelete_PreservesStableFieldIdentity`
- `WorkingData_Reset_FallsBackToOriginalWorkbook`
- `WorkingData_MissingSource_RemainsUsable`
- `EditedWorkingData_IsReflectedInReportContentDocument`
- `ClipboardMatrixPaste_AppliesRectangularValues`

Static source inventory comparison against the supplied Sprint 9 baseline:

- baseline test methods: 69
- current test methods: 79
- added test methods: 10
- missing baseline test methods: 0
- `WorksheetMappings_AreIndependentPerWorksheet`: preserved

No existing test was deleted or weakened.

## Verification

Known Windows/.NET 8 result supplied at continuation start:

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED
- `WorksheetMappingIndependenceTests.cs`: CS0104 `DataField` ambiguity (2)
- `WorksheetMappingIndependenceTests.cs`: CS0103 `SpreadsheetDocumentType`
- `PreviewView.xaml`: MC3072 `BorderDashArray`
- `ExcelDataProvider.cs`: CS8602 warning
- `Sprint8PersistenceAndCompositionTests.cs`: xUnit2030 warning
- NETSDK1057: preview SDK notice, not the compile failure

The listed source/analyzer candidates were targeted with the exact narrow fixes described above.

Actual local command results after implementation:

- `dotnet restore`: command invoked; exit `127`; `dotnet: command not found`
- `dotnet build`: command invoked; exit `127`; `dotnet: command not found`
- `dotnet test`: command invoked; exit `127`; `dotnet: command not found`

Therefore build/test success is **not claimed**. Windows/.NET 8 verification remains pending and is the final runtime truth.

Supplemental static verification actually performed:

- 21 XAML/XML/project/props files parsed: 0 XML parse errors.
- 171 C# files passed lexical delimiter-balance scan: 0 reported issues.
- XAML event-handler cross-check: 0 missing handlers.
- Invalid `BorderDashArray` pattern: 0 remaining hits.
- Old xUnit2030 `Assert.NotEmpty(...Where(...))` pattern: 0 remaining hits.
- relevant Excel/working-data/UI source scan found 0 `SpreadsheetDocument.Open(..., true)` calls.
- Sprint 9 baseline test inventory preserved: 69/69 methods present.
- no `bin/` or `obj/` directories are included in the working tree.

## Remaining Gaps

- Windows/.NET 8 build and test execution is still required because local `dotnet` is unavailable.
- Paste rejects rectangles that exceed the current working-data bounds; it does not auto-insert rows/columns.
- Inserted columns use the default `Yeni Sütun` header; dedicated column-header rename UX is not included.
- Working-data editing is scoped to the configured dataset, not arbitrary workbook/header/formula editing.
- Missing-source full relink is not implemented.
- Undo/redo is not implemented.
- Formula evaluation is not implemented.
- Large-dataset editing/virtualization performance has not been benchmarked.
