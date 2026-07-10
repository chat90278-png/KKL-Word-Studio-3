# KKL Word Studio — Sprint 13 Implementation Report

## 1. Context-menu / keyboard interaction model

The permanent `Satır Ekle`, `Satır Sil`, `Sütun Ekle`, and `Sütun Sil` toolbar buttons were removed. The Excel grid now exposes real row headers and routes compact right-click menus for row headers, column headers, and cells. Row operations are `Üste Satır Ekle`, `Alta Satır Ekle`, `Satırı Sil`, and `Satırı Temizle`; column operations are `Sola Sütun Ekle`, `Sağa Sütun Ekle`, `Sütunu Sil`, `Sütunu Gizle`, and `Tüm Sütunları Göster`; cell operations reuse the existing copy/paste/clear paths.

`Ctrl+Z` / `Ctrl+Y` execute the existing worksheet-scoped `UndoCommand` / `RedoCommand`. `Ctrl+OemPlus`, numpad `Add`, `Ctrl+OemMinus`, and numpad `Subtract` are header-context aware. Plain-cell context does not emulate Excel shift-cells and reports `Önce bir satır veya sütun başlığı seçin.` Runtime interaction context remains WPF-only and is not persisted.

## 2. Filtered-row + hidden-column identity safety

`WorkingDataInteractionResolver` maps filtered display rows through `WorkingDataViewState.VisibleRowToWorkingRow` before insert, delete, clear, paste, or edit. Context operations no longer treat a filtered DataGrid row index as a WorkingData row index.

Column mutations resolve the DataGrid binding path to `WorkingDataColumn.SourceField`, `OriginalSourceColumn`, or stable `Id`; `DisplayIndex` is not used as product column identity. Hidden columns therefore do not shift delete/insert targets. The technical `#` DataTable column is cancelled during DataGrid auto-generation and rendered only through `DataGridRowHeader`. Existing referenced-table deletion guards remain in `WorksheetWorkingDataService.DeleteColumns`. Each logical context mutation still enters the existing `Mutate` funnel once, producing one undo step on success.

## 3. Automatic range detection heuristic

Added Application-layer `IExcelDataRangeDetector` / `ExcelDataRangeDetector`. The deterministic heuristic ignores leading fully blank rows, profiles occupied columns, scores adjacent candidate rows by non-blank density, column overlap, likely-header evidence, and continuation, then derives active start/end columns from repeated occupancy in the detected preview block.

A probable label row followed by a substantially overlapping data row is preferred as a header when header-label evidence or a clear scalar-shape change exists. Ambiguous all-text data remains a no-header candidate but is intentionally low confidence. Low confidence keeps the best candidate and drives the review status instead of claiming perfect table inference.

The read-only OpenXML reader now accepts the candidate `DataStartRow`, `StartColumn`, and `EndColumn` for the full-sheet contiguous end-row scan. Sparse OpenXML cell references are preserved in preview projection, so a real B:G source block is no longer compressed to A:F. The workbook is still opened with `SpreadsheetDocument.Open(filePath, false)`.

## 4. Open / sheet-switch detection flow

`ExcelRangeLoadPolicy` centralizes the worksheet load decision:

1. `Worksheet.WorkingData` exists → WorkingData is authoritative; no source re-detection.
2. `Worksheet.SelectedRange` exists → persisted range is applied; no automatic overwrite.
3. Otherwise → current preview is automatically detected and the full-sheet reader resolves `DataEndRow`.

The same path runs after sheet switches. Auto-detection configures only the current source-range candidate and does not create WorkingData. A new auto-detect path resets transient range display state first so a failed detection cannot leak the previous sheet's range. Missing-source handling does not clear persisted range or WorkingData.

## 5. Manual range editor

The permanent raw start-row field, `Başlık` checkbox, and `Bitişi Algıla` button were removed from the normal strip. The default strip now shows `RangeReference · state` plus one compact `Veri Aralığını Düzenle` action.

The range editor exposes header presence/row, data start/end rows, Excel-letter start/end columns, `Yeniden Algıla`, `Uygula`, and `İptal`. Manual apply validates row/column order, marks `WasAutoDetected = false`, updates the current range and `Worksheet.SelectedRange` when a worksheet model exists, and refreshes transfer/status state. Persisted ranges render as `Yapılandırıldı`; only the live automatic candidate renders as `Otomatik algılandı`. Low-confidence candidates render `Veri aralığını doğrulayın`.

## 6. Real DataRange propagation into transfer / WorkingData / multi-source

`ExcelWorkspaceViewModel` no longer creates transfer or WorkingData ranges with `StartColumn = 1` and `EndColumn = preview.ColumnCount`. `BuildCurrentRange` carries the configured/detected `DataStartRow`, `DataEndRow`, `HeaderRowIndex`, `StartColumn`, and `EndColumn` into:

- direct `Word'e Aktar` requests,
- lazy `EnsureWorkingDataAsync`,
- project data-source creation from the active worksheet,
- the existing Sprint 10 transfer pipeline, which pins the received range in `TableSourceBinding.DataRange` for additional sources.

Header text projection and Excel column-mapping generation are aligned to `StartColumn..EndColumn`. Source identities remain the real Excel letters (`B`, `C`, ...), while `TableColumn.Header` / `SourceField`, WorkingData stable `Id`, worksheet mappings, WorkingData-first provider precedence, ordered multi-source semantics, and legacy binding fallback remain unchanged.

## 7. Files changed

- `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `src/KKL.WordStudio.Application/Excel/ExcelDataRangeCandidate.cs`
- `src/KKL.WordStudio.Application/Excel/ExcelDataRangeDetector.cs`
- `src/KKL.WordStudio.Application/Excel/ExcelRangeLoadPolicy.cs`
- `src/KKL.WordStudio.Application/Excel/ExcelRangeProjection.cs`
- `src/KKL.WordStudio.Application/Excel/IExcelDataRangeDetector.cs`
- `src/KKL.WordStudio.Application/Excel/IExcelWorkbookReader.cs`
- `src/KKL.WordStudio.Application/WorkingData/WorkingDataInteractionResolver.cs`
- `src/KKL.WordStudio.Infrastructure/Excel/OpenXmlExcelWorkbookReader.cs`
- `src/KKL.WordStudio.UI/ViewModels/ExcelWorkspaceViewModel.cs`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml`
- `src/KKL.WordStudio.UI/Views/ExcelWorkspaceView.xaml.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint13ExcelInteractionAndAutoRangeTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/OpenXmlExcelWorkbookReaderTests.cs`

## 8. Tests

The Sprint 12 baseline contains 144 `[Fact]` / `[Theory]` methods. None were removed or renamed. Sprint 13 adds 17 focused methods, for 161 source test methods total:

- `AutoRange_IgnoresLeadingBlankRows`
- `AutoRange_DetectsHeaderAndDataStart`
- `AutoRange_SupportsNoHeaderDataset`
- `AutoRange_DetectsNonAStartAndEndColumns`
- `AutoRange_LowConfidence_RequiresReviewStatus`
- `SheetLoad_AutoDetectsWhenNoPersistedRange`
- `PersistedRange_IsNotOverwrittenByAutoDetection`
- `WorkingDataWorksheet_IsNotRedetectedFromSource`
- `Transfer_UsesConfiguredStartAndEndColumns`
- `EnsureWorkingData_UsesConfiguredColumnBounds`
- `HeaderTexts_AreAlignedToConfiguredColumnBounds`
- `FilteredRowContextInsert_UsesUnderlyingWorkingRow`
- `FilteredRowContextDelete_UsesUnderlyingWorkingRows`
- `HiddenColumnContextDelete_ResolvesStableWorkingColumn`
- `ContextColumnDelete_PreservesReferencedTableGuard`
- `ContextMutation_IsOneUndoStep`
- `AutoDetection_DoesNotModifyOriginalWorkbook`

These tests were added but could not be executed in this environment because the .NET CLI is unavailable.

## 9. Actual verification

- `dotnet --info`: unavailable (`dotnet: command not found`). Therefore `dotnet restore`, `dotnet build`, and `dotnet test` were not run and no green build/test claim is made.
- Baseline test-method source count: 144.
- Sprint 13 test-method source count: 161; all 17 requested focused test names are present.
- All XAML files parsed successfully as XML.
- `ExcelWorkspaceView.xaml` event/EventSetter handlers were statically matched to code-behind methods; no missing handler was found.
- C# brace-parity scan found no mismatch in `src` or `tests`.
- Static scan found no remaining forced transfer/WorkingData `StartColumn = 1` plus preview-width end-column pattern and no `DisplayIndex - 1` product-identity pattern in the affected layers. `DisplayIndex` remains only in clipboard layout ordering.
- Delivery tree contains no `bin` or `obj` directories.

Windows/.NET 8 WPF verification remains the final runtime truth.

## 10. Remaining gaps

The remaining blocker is actual Windows/.NET 8 restore/build/test and WPF gesture verification because this environment has no `dotnet`. In particular, row/column header focus and multi-selection behavior should be exercised on the real Windows DataGrid even though the routing and identity paths are source-checked.

The range detector is deliberately conservative and deterministic, not a perfect Excel table inference engine; ambiguous candidates remain reviewable. A4 pagination and native DOCX preview were not started, per Sprint 13 scope. The next major direction remains the paginated A4 document surface.
