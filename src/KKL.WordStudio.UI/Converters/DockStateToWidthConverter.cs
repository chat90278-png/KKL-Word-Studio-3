namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows.Data;
using KKL.WordStudio.UI.ViewModels;

/// <summary>Maps DockState to a pixel width. Mirrors DockViewModel.NormalWidth/CollapsedWidth/ExpandedWidth — kept as a converter (rather than binding to those properties directly) because GridSplitter-driven Grid.Column.Width needs a GridLength, not a raw double.</summary>
public sealed class DockStateToWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DockState.Collapsed => new System.Windows.GridLength(46),
        DockState.Expanded => new System.Windows.GridLength(440),
        _ => new System.Windows.GridLength(350)
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
