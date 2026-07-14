namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Visitors;

public sealed partial class ExcelWorkspaceViewModel
{
    [ObservableProperty]
    private bool _hasSelectedHeadingTransferParent;

    [ObservableProperty]
    private string _selectedHeadingTransferParentText = string.Empty;

    partial void OnIsTransferPlacementOpenChanged(bool value)
    {
        HasSelectedHeadingTransferParent = false;
        SelectedHeadingTransferParentText = string.Empty;

        if (!value || !CreateNewTable)
            return;

        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } selectedId)
            return;

        if (ReportElementFlattener.FindById(report, selectedId) is not TextElement selectedHeading)
            return;

        var isRoot = string.Equals(selectedHeading.Name, "Document Root", StringComparison.Ordinal);
        var isHeading = HeadingStylePresets.IsHeading(selectedHeading.Style);
        var isAltHeading = HeadingStylePresets.IsAltHeading(selectedHeading.Style);
        if (!isRoot && !isHeading && !isAltHeading)
            return;

        _placementAnchorElementId = selectedHeading.Id;
        PlacementParentText = NormalizePlacementParentText(selectedHeading, isRoot);
        SelectedHeadingTransferParentText = PlacementParentText;
        HasSelectedHeadingTransferParent = true;
    }

    [RelayCommand]
    private void PlaceDirectlyUnderSelectedHeading()
    {
        if (!HasSelectedHeadingTransferParent)
            return;

        IncludePlacementHeading = false;
        IncludePlacementAltHeading = false;
        StatusText = $"Yeni tablo {SelectedHeadingTransferParentText} başlığının altına doğrudan eklenecek.";
    }

    private static string NormalizePlacementParentText(TextElement heading, bool isRoot)
    {
        var current = heading.Content.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(current))
        {
            return isRoot
                ? $"1. {ExcelTransferPlacementCoordinator.DefaultRootHeadingText}"
                : "Seçili başlık";
        }

        if (char.IsDigit(current[0]))
            return current;

        return isRoot ? $"1. {current}" : current;
    }
}
