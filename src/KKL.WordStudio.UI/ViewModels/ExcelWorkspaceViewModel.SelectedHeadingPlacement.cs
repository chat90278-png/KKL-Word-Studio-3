namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Visitors;

public sealed partial class ExcelWorkspaceViewModel
{
    partial void OnIsTransferPlacementOpenChanged(bool value)
    {
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
        StatusText = "Seçili başlık parent olarak kullanılacak. Yeni başlık satırlarını kaldırırsanız tablo doğrudan bu başlığın altına eklenir.";
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
