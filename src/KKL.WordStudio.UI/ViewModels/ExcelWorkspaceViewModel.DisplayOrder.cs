namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Visitors;

public sealed partial class ExcelWorkspaceViewModel
{
    /// <summary>
    /// Synchronizes transfer order with the DataGrid's actual left-to-right
    /// DisplayIndex order. This is intentionally runtime UI projection state;
    /// source Excel coordinates and persisted worksheet mappings stay unchanged.
    /// </summary>
    public void SetColumnDisplayOrder(IReadOnlyList<string> columnIdentities)
    {
        EnsureColumnTransferOptions();

        for (var displayIndex = 0; displayIndex < columnIdentities.Count; displayIndex++)
        {
            var identity = columnIdentities[displayIndex];
            var option = ColumnMappings.FirstOrDefault(candidate =>
                string.Equals(candidate.ProviderField, identity, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.SourceColumn, identity, StringComparison.OrdinalIgnoreCase));
            if (option is not null)
                option.SourceOrder = displayIndex;
        }
    }

    partial void OnIsTransferPlacementOpenChanged(bool value)
    {
        if (!value || _workspace.ActiveReport is not { } report)
            return;

        var root = ReportElementFlattener.Flatten(report)
            .OfType<TextElement>()
            .FirstOrDefault(text => string.Equals(text.Name, "Document Root", StringComparison.Ordinal));
        var rootText = ReportHeadingNumberingService.StripVisibleNumber(root?.Content.Text);
        PlacementParentText = $"1. {(string.IsNullOrWhiteSpace(rootText)
            ? ExcelTransferPlacementCoordinator.DefaultRootHeadingText
            : rootText)}";

        if (!CreateNewTable || _workspace.SelectedReportElementId is not { } selectedId)
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
        StatusText = "Seçili başlık parent olarak kullanılacak. Yeni başlık ve alt başlık satırlarını kaldırırsanız tablo doğrudan bu başlığın altına eklenecek.";
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
