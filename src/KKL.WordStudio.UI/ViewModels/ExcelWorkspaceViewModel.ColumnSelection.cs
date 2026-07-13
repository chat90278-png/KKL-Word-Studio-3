namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Transfer;

public sealed partial class ExcelWorkspaceViewModel
{
    private readonly IColumnTransferSelectionSession _columnTransferSelections =
        ColumnTransferSelectionSession.Shared;

    [RelayCommand]
    private void OpenColumnSelectionMappingDrawer()
    {
        var previous = ColumnMappings
            .GroupBy(row => row.SourceColumn, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        // Rebuild the complete current-sheet list so columns excluded during a
        // previous transfer remain available for re-selection. Preserve any
        // user-authored names/types already present in this session.
        GenerateColumnMappings();
        foreach (var row in ColumnMappings)
        {
            if (!previous.TryGetValue(row.SourceColumn, out var oldRow))
                continue;

            row.FieldName = oldRow.FieldName;
            row.DataType = oldRow.DataType;
            row.IsIncluded = oldRow.IsIncluded;
        }

        if (SelectedWorkbook?.SelectedSheetName is { } sheetName)
        {
            var appliedSelection = _columnTransferSelections.GetSelection(
                SelectedWorkbook.FilePath,
                sheetName);
            if (appliedSelection is not null)
            {
                var selected = appliedSelection.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var row in ColumnMappings)
                    row.IsIncluded = selected.Contains(row.SourceColumn);
            }
        }

        IsMappingDrawerOpen = true;
    }

    [RelayCommand]
    private void SelectAllMappingColumns()
    {
        foreach (var row in ColumnMappings)
            row.IsIncluded = true;
    }

    [RelayCommand]
    private void ClearMappingColumnSelection()
    {
        foreach (var row in ColumnMappings)
            row.IsIncluded = false;
    }

    [RelayCommand]
    private void ApplyColumnSelectionMapping()
    {
        if (SelectedWorkbook?.SelectedSheetName is not { } sheetName)
        {
            StatusText = "Önce bir Excel dosyası ve sayfası açın.";
            return;
        }

        var selectedColumns = ColumnMappings
            .Where(row => row.IsIncluded)
            .Select(row => row.SourceColumn)
            .ToList();

        if (selectedColumns.Count == 0)
        {
            StatusText = "Rapora aktarılacak en az bir sütun seçin.";
            return;
        }

        _columnTransferSelections.SetSelection(
            SelectedWorkbook.FilePath,
            sheetName,
            selectedColumns);

        ApplyMapping();
        StatusText = $"{selectedColumns.Count} sütun rapora aktarılacak — eşleme uygulandı";
    }
}
