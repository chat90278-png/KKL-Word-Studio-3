namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

/// <summary>
/// Visible when the bound string is non-empty; Collapsed otherwise. Used by the
/// Contents dock's non-modal structure-failure feedback line so it only takes
/// space when there is a message to show.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
