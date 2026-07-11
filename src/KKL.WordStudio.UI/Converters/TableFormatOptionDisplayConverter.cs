namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows.Data;
using KKL.WordStudio.UI.ViewModels;

public sealed class TableFormatOptionDisplayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var option = values.ElementAtOrDefault(0) as TableFormatOptionViewModel;
        var viewModel = values.ElementAtOrDefault(1) as PropertiesViewModel;
        if (option is null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(option.Key))
            return option.DisplayName;

        return string.IsNullOrWhiteSpace(viewModel?.AutomaticTableFormatDisplayName)
            ? "Otomatik"
            : $"Otomatik — {viewModel.AutomaticTableFormatDisplayName}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
