namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows.Data;

/// <summary>True when both bound strings are equal — used to give the active sheet tab a distinct look without a separate "IsActive" property on each sheet name string.</summary>
public sealed class StringEqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && Equals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
