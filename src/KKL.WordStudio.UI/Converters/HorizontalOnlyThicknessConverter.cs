namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

/// <summary>Keeps a Thickness's Left/Right but replaces Top/Bottom with a small fixed value — used so the Header/Footer preview bands align horizontally with the Body's real page margins while keeping their own compact vertical padding.</summary>
public sealed class HorizontalOnlyThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Thickness t ? new Thickness(t.Left, 8, t.Right, 8) : new Thickness(0, 8, 0, 8);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
