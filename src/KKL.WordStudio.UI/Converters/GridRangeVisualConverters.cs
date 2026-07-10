namespace KKL.WordStudio.UI.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

/// <summary>
/// Highlights a DataGridRow with the warm header-row background when its
/// row number matches the configured header row. Inputs: [0] the row's own
/// "#" cell value, [1] ExcelWorkspaceViewModel.HeaderRowNumber (int?).
/// The "#" column is a string-typed DataColumn (DataTable.Columns.Add(string)
/// defaults to string), so the row number is parsed rather than cast.
/// </summary>
public sealed class HeaderRowBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || !TryGetRowNumber(values[0], out var rowNumber) || values[1] is not int headerRow)
            return Brushes.Transparent;

        return rowNumber == headerRow ? new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xDD)) : Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static bool TryGetRowNumber(object? value, out int rowNumber)
    {
        if (value is int i) { rowNumber = i; return true; }
        return int.TryParse(value?.ToString(), out rowNumber);
    }
}

/// <summary>
/// Draws a strong accent top border on the data-start row and an accent
/// bottom border on the data-end row, so "where the real data begins/ends"
/// is visible directly in the grid. Inputs: [0] row number, [1] data start
/// row (int), [2] data end row (int?).
/// </summary>
public sealed class RangeBoundaryThicknessConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3 || !HeaderRowBackgroundConverter.TryGetRowNumber(values[0], out var rowNumber) || values[1] is not int dataStart)
            return new Thickness(0);

        var isStart = rowNumber == dataStart;
        var isEnd = values[2] is int dataEnd && rowNumber == dataEnd;

        return new Thickness(0, isStart ? 2 : 0, 0, isEnd ? 2 : 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
