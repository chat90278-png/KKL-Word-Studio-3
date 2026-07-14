namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.Input;

public sealed partial class WarningCenterViewModel
{
    [RelayCommand]
    private void ShowAllOccurrences(WarningDiagnosticItemViewModel? item)
    {
        if (item is null)
            return;

        var group = item.Group;
        var parts = new List<string>
        {
            $"{item.CodeText} · {item.OccurrenceText}"
        };

        if (!string.IsNullOrWhiteSpace(group.AffectedColumn))
            parts.Add($"Sütun: {group.AffectedColumn}");

        if (group.RowNumbers.Count > 0)
            parts.Add($"Etkilenen satırlar: {string.Join(", ", group.RowNumbers.Take(25))}{(group.RowNumbers.Count > 25 ? ", …" : string.Empty)}");

        if (group.KeyValues.Count > 0)
        {
            parts.Add(item.DistinctKeyText);
            parts.Add($"Kayıt anahtarları: {string.Join(", ", group.KeyValues.Take(25))}{(group.KeyValues.Count > 25 ? ", …" : string.Empty)}");
        }

        NavigationStatusText = string.Join("  •  ", parts);
    }
}
