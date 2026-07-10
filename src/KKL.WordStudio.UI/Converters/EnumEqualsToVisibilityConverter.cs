namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

/// <summary>Visible when the bound enum's string form equals ConverterParameter — used to switch between Heading/Table/None sections in PropertiesView without a DataTemplateSelector.</summary>
public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
