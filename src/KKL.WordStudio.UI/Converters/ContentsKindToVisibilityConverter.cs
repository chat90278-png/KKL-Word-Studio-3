namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using KKL.WordStudio.UI.ViewModels;

/// <summary>Shows the binding-status dot and status line only for Table nodes — headings have neither.</summary>
public sealed class IsTableToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ContentsNodeKind.Table ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
