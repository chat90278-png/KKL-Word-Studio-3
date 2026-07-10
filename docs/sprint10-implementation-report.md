# Sprint 10 Implementation Report

## 1. Source Collection / Model Decision

`TableElement` now has an ordered persisted `Sources` collection of `TableSourceBinding` items. Each source pins `DataSourceName`, `WorksheetName`, a cloned `DataRange`, and persisted `TableColumn.Id -> provider SourceField` mappings.

`TableColumn` now has a stable persisted `Guid Id`. `Header` remains display text and `SourceField` remains the report column's logical/source identity; neither is repurposed as a per-source mapping key.

No generic dataset/document engine was introduced.

## 2. Legacy Binding Compatibility

`TableElement.Binding` was preserved.

- `Sources.Count == 0`: existing single-source `Binding` behavior remains authoritative.
- Adding a second source to a legacy-bound table snapshots the current bound worksheet and configured range as source #1, then appends the new source as source #2.
- Existing rebind/replace choices clear `Sources` and retain their existing single-binding meaning.
- Old `.kws` projects without `Sources` or `TableColumn.Id` data remain compatible with the existing JSON populate path; missing column IDs receive the model initializer value.

## 3. Field Normalization

Additional-source matching is Application-layer behavior in `ExcelReportTransferService`.

Resolution order:

1. mapped logical field name against `TableColumn.SourceField`;
2. exact case-insensitive logical/provider/header identity;
3. if any table column remains unresolved, return `RequiresSourceFieldMapping` without adding a table source.

No positional fallback is used for a new additional source. A compact Turkish `KAYNAK ALAN EŞLEME` surface lets the user choose source fields only when automatic normalization is incomplete. The persisted source item stores provider field keys by stable `TableColumn.Id`.

Legacy-source snapshot conversion retains a positional fallback only for backward compatibility with old tables that predate `TableColumn.SourceField`.

## 4. Composition + Preview / Word Consistency

`IDataProvider.GetRowsAsync` gained an optional `DataRange` override while existing calls remain compatible.

`ReportContentBuilder` remains authoritative:

- when `Sources` is empty, it executes the existing single-binding path;
- for multi-source tables, it reads each persisted source in list order;
- original Excel reads use the pinned source range override;
- worksheet `WorkingData` keeps provider precedence when present;
- each row is normalized through that source's persisted `TableColumn.Id -> SourceField` mappings;
- normalized rows are appended into one `TableContentNode`.

Preview renders that node and Word writes the same node. There is no preview-only or Word-only multi-source composition state.

`MultiSourceRows_AreIdenticalInReportContentAndWord` asserts the semantic values and verifies both values are present in generated DOCX body content in the same order.

## 5. Source-Order Persistence / UI

The existing `Word'e Aktar` decision surface now includes `Kaynak Olarak Ekle` for an already configured table.

Properties now shows `VERİ KAYNAKLARI` with source name, worksheet, pinned range, and source status. `Yukarı`, `Aşağı`, and `Kaldır` operations are coordinated by `ITableSourceCompositionService`.

The `Sources` list order is persisted and is the row append order. Reordering triggers shared report-content refresh immediately. Removing a source removes only the table reference; project `DataSource` objects are not deleted.

## 6. Missing-Source Behavior

Source usability is evaluated per table source.

- If the original XLSX/XLSM path is missing but the pinned worksheet has `WorkingData`, the source is usable and WorkingData remains authoritative.
- If neither WorkingData nor a readable original source is available, the source entry remains persisted.
- Properties reports a Turkish missing-source state.
- `ReportContentBuilder` puts an explicit Turkish `SourceError` on the composed table node instead of silently skipping the input.
- Preview surfaces the error.
- `WordExporter` returns the existing friendly failure `Result` path and does not emit a silently incomplete multi-source Word document.

No relink system was added.

## 7. Files Changed

### Added

- `src/KKL.WordStudio.Application/DataSources/TableSourceCompositionService.cs`
- `src/KKL.WordStudio.Domain/DataBinding/TableSourceBinding.cs`
- `src/KKL.WordStudio.UI/ViewModels/SourceFieldMatchRowViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/TableSourceRowViewModel.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint10TransferSourceMappingTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint10MultiSourceTests.cs`
- `docs/sprint10-implementation-report.md`

### Modified

- `src/KKL.WordStudio.Application/Abstractions/IDataProvider.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentBuilder.cs`
- `src/KKL.WordStudio.Application/Content/ReportContentNode.cs`
- `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `src/KKL.WordStudio.Application/Preview/PreviewSnapshot.cs`
- `src/KKL.WordStudio.Application/Transfer/ExcelReportTransferService.cs`
- `src/KKL.WordStudio.Application/Transfer/ExcelTransferRequest.cs`
- `src/KKL.WordStudio.Application/WorkingData/WorksheetWorkingDataService.cs`
- `src/KKL.WordStudio.Domain/Elements/TableElement.cs`
- `src/KKL.WordStudio.Infrastructure/DataProviders/InMemoryDataProvider.cs`
- `src/KKL.WordStudio.Infrastructure/Excel/ExcelDataProvider.cs`
- `src/KKL.WordStudio.Infrastructure/Export/Exporters/WordExporter.cs`
- `src/KKL.WordStudio.UI/Preview/PreviewRenderer.cs`
- `src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/PreviewViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/PropertiesViewModel.cs`
- `src/KKL.WordStudio.UI/ViewModels/TablePreviewBlockViewModel.cs`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml`
- `src/KKL.WordStudio.UI/Views/PropertiesView.xaml`
- `tests/KKL.WordStudio.Application.Tests/ReportContentBuilderTests.cs`

### Removed

None.

## 8. Tests

All 79 test methods from the supplied Sprint 9 Stabilized baseline remain present.

15 Sprint 10 test methods were added:

- `MultiSourceTable_AppendsRowsInConfiguredSourceOrder`
- `MultiSourceTable_SupportsSameWorkbookDifferentWorksheets`
- `MultiSourceTable_SupportsDifferentWorkbooks`
- `PerSourceFieldMapping_NormalizesDifferentSchemas`
- `SourceOrder_RoundTripsThroughProjectPersistence`
- `ReorderingSources_ChangesComposedRowOrder`
- `LegacySingleBinding_StillRenders`
- `WorkingData_HasPrecedenceInsideMultiSourceComposition`
- `MissingMultiSourceInput_IsSurfacedWithoutCrashing`
- `MultiSourceRows_AreIdenticalInReportContentAndWord`
- `AddingSource_DoesNotOverwriteExistingDisplayHeaders`
- `RemovingSource_DoesNotDeleteProjectDataSource`
- `MultiSourceOriginalExcel_UsesPinnedRangeOverride`
- `AdditionalSource_UnresolvedSchema_RequiresExplicitMappingWithoutMutatingTableSources`
- `AdditionalSource_ExplicitFieldMapping_PersistsProviderFieldByStableTableColumnId`

`WorksheetMappings_AreIndependentPerWorksheet` remains present.

No existing test method was deleted, skipped, renamed away, or weakened.

## 9. Actual Verification

Actual commands invoked in the Sprint 10 workspace:

- `dotnet restore` -> `dotnet: command not found`, exit `127`
- `dotnet build` -> `dotnet: command not found`, exit `127`
- `dotnet test` -> `dotnet: command not found`, exit `127`

Build/test success is **not claimed**. Windows/.NET 8 remains the final runtime truth and validation is pending there.

Supplemental static/source verification actually performed:

- baseline test inventory: 79 methods;
- current test inventory: 94 methods;
- baseline methods missing: 0;
- 21 XAML/XML/project/props files parsed with 0 XML errors;
- 177 C# files passed lexical delimiter-balance scan with 0 reported issues;
- XAML event-handler cross-check found 0 missing handlers;
- all 12 requested focused Sprint 10 test names are present;
- prohibited-scope scan found no Office Interop, WebView, or `SpreadsheetDocument.Open(..., true)` use;
- original Excel provider path still uses `SpreadsheetDocument.Open(sourcePath, false)`;
- no baseline source/test file was removed.

These static checks do not replace compilation or runtime tests.

## 10. Remaining Gaps

- Windows/.NET 8 `restore`, `build`, and `test` output is still required before green status can be claimed.
- Source relink is not implemented.
- Removing the final source intentionally leaves the table unbound; no automatic model collapse is performed.
- Field matching UI is compact and explicit; it does not provide saved mapping presets.
- Multi-source filter execution remains the pre-existing deferred filter behavior.
- True pagination is not implemented.
- Undo/redo is not implemented.
- Very large multi-source composition performance has not been benchmarked.
