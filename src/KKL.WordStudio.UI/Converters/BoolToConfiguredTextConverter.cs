namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows.Data;

public sealed class BoolToConfiguredTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Yapılandırıldı" : "Yapılandırılmadı";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
