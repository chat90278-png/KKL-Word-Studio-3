namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.Formatting;

public sealed partial class PropertiesViewModel
{
    public string AutomaticTableFormatDisplayName =>
        _selectedTable is null
            ? string.Empty
            : TableFormatProfileSelector.SelectAutomatic(_resolvedFormatProfile, _selectedTable)?.DisplayName
              ?? string.Empty;

    public string EffectiveTableFormatStatusText
    {
        get
        {
            if (_selectedTable is null)
                return string.Empty;

            var effective = TableFormatProfileSelector.Select(_resolvedFormatProfile, _selectedTable);
            if (effective is null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(_selectedTable.ReferenceTableFormatKey)
                ? $"Etkin biçim (otomatik): {effective.DisplayName}"
                : $"Etkin biçim: {effective.DisplayName}";
        }
    }

    partial void OnTableFormatStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(AutomaticTableFormatDisplayName));
        OnPropertyChanged(nameof(EffectiveTableFormatStatusText));
    }
}
