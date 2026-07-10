namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows.Data;

/// <summary>True when the bound enum's string form equals ConverterParameter — used to drive the Contents/Properties segmented RadioButtons' IsChecked from a single DockPage enum property.</summary>
public sealed class EnumEqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Enum.Parse(targetType, parameter!.ToString()!) : Binding.DoNothing;
}
