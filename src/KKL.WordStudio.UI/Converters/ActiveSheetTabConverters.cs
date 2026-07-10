namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

/// <summary>Shared "is this tab the active one" check reused by the three sheet-tab appearance converters below.</summary>
internal static class ActiveSheetTabHelper
{
    public static bool IsActive(object[] values) =>
        values.Length == 2 && values[0] is string tabName && values[1] is string selected && tabName == selected;
}

public sealed class ActiveSheetBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        ActiveSheetTabHelper.IsActive(values) ? Brushes.White : Brushes.Transparent;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ActiveSheetForegroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        ActiveSheetTabHelper.IsActive(values) ? new SolidColorBrush(Color.FromRgb(0x17, 0x4E, 0xA6)) : new SolidColorBrush(Color.FromRgb(0x66, 0x75, 0x8A));

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ActiveSheetFontWeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        ActiveSheetTabHelper.IsActive(values) ? FontWeights.SemiBold : FontWeights.Normal;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
