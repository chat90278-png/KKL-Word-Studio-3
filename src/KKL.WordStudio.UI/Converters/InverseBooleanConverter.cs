namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows.Data;

/// <summary>Plain bool negation — used for the "Heading 2" radio button, checked exactly when IsMainHeading is false.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}
