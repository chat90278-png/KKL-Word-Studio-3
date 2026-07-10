namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using KKL.WordStudio.UI.ViewModels;

/// <summary>Maps a table's BindingStatus to the small status-dot color used in the Contents outline. Color is never the only signal — the dot is always paired with StatusText (see ADR/Variant 2.5 task: "color must not be the only signal").</summary>
public sealed class BindingStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TableBindingStatus.Bound => new SolidColorBrush(Color.FromRgb(0x2F, 0x85, 0x5A)),
        TableBindingStatus.SourceMissing => new SolidColorBrush(Color.FromRgb(0xC5, 0x30, 0x30)),
        TableBindingStatus.NotConfigured => new SolidColorBrush(Color.FromRgb(0xB7, 0x79, 0x1F)),
        _ => new SolidColorBrush(Color.FromRgb(0x9A, 0xA5, 0xB3))
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
