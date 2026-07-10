namespace KKL.WordStudio.UI.Preview;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.UI.ViewModels;

/// <summary>
/// Fragment-local read-only table renderer. Pagination and span clipping are
/// owned by Engine; this control only maps resolved table formatting and
/// payload-local spans to WPF Grid geometry. Border thickness comes from the
/// resolved format; the document border visual uses the Word-equivalent black.
/// </summary>
public sealed class PreviewTableGridControl : Grid
{
    private const double MillimetersToDips = 96.0 / 25.4;
    private const double PointsToDips = 96.0 / 72.0;
    private static readonly Brush CellBorderBrush = Brushes.Black;

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows),
        typeof(IReadOnlyList<PreviewTablePageRowViewModel>),
        typeof(PreviewTableGridControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnGridInputChanged));

    public static readonly DependencyProperty CellSpansProperty = DependencyProperty.Register(
        nameof(CellSpans),
        typeof(IReadOnlyList<TableCellSpan>),
        typeof(PreviewTableGridControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnGridInputChanged));

    public static readonly DependencyProperty ColumnCountProperty = DependencyProperty.Register(
        nameof(ColumnCount),
        typeof(int),
        typeof(PreviewTableGridControl),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnGridInputChanged));

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format),
        typeof(ResolvedTableFormat),
        typeof(PreviewTableGridControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnGridInputChanged));

    public IReadOnlyList<PreviewTablePageRowViewModel>? Rows
    {
        get => (IReadOnlyList<PreviewTablePageRowViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public IReadOnlyList<TableCellSpan>? CellSpans
    {
        get => (IReadOnlyList<TableCellSpan>?)GetValue(CellSpansProperty);
        set => SetValue(CellSpansProperty, value);
    }

    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    public ResolvedTableFormat? Format
    {
        get => (ResolvedTableFormat?)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    private static void OnGridInputChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _) =>
        ((PreviewTableGridControl)dependencyObject).RebuildGrid();

    private void RebuildGrid()
    {
        Children.Clear();
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();

        var rows = Rows ?? Array.Empty<PreviewTablePageRowViewModel>();
        var spans = CellSpans ?? Array.Empty<TableCellSpan>();
        var format = Format ?? DefaultFormatProfiles.Table;
        var columnCount = ResolveColumnCount(rows, spans);

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var columnFormat = ResolveColumnFormat(format, columnIndex);
            var widthWeight = columnFormat.WidthWeight > 0d ? columnFormat.WidthWeight : 1d;
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widthWeight, GridUnitType.Star) });
        }

        var preferredRowHeight = ToDips(Math.Max(0d, format.PreferredRowHeightMillimeters));
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = preferredRowHeight });

        var spanAnchors = new Dictionary<(int Row, int Column), int>();
        var coveredCells = new HashSet<(int Row, int Column)>();
        var occupiedSpanCells = new HashSet<(int Row, int Column)>();
        foreach (var span in spans)
        {
            if (!IsValidLocalSpan(span, rows.Count, columnCount))
                continue;

            var anchor = (span.RowIndex, span.ColumnIndex);
            var spanCells = Enumerable.Range(span.RowIndex, span.RowSpan)
                .Select(rowIndex => (Row: rowIndex, Column: span.ColumnIndex))
                .ToList();
            if (spanAnchors.ContainsKey(anchor) || spanCells.Any(occupiedSpanCells.Contains))
                continue;

            spanAnchors.Add(anchor, span.RowSpan);
            foreach (var spanCell in spanCells)
                occupiedSpanCells.Add(spanCell);
            foreach (var coveredRowIndex in Enumerable.Range(span.RowIndex + 1, span.RowSpan - 1))
                coveredCells.Add((coveredRowIndex, span.ColumnIndex));
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                if (coveredCells.Contains((rowIndex, columnIndex)))
                    continue;

                var cell = CreateCell(ReadCell(rows[rowIndex], columnIndex), format, columnIndex);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, columnIndex);
                if (spanAnchors.TryGetValue((rowIndex, columnIndex), out var rowSpan))
                    Grid.SetRowSpan(cell, rowSpan);

                Children.Add(cell);
            }
        }
    }

    private int ResolveColumnCount(
        IReadOnlyList<PreviewTablePageRowViewModel> rows,
        IReadOnlyList<TableCellSpan> spans)
    {
        var rowColumnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Cells.Count);
        var spanColumnCount = spans.Count == 0 ? 0 : spans.Max(span => span.ColumnIndex + 1);
        return Math.Max(1, Math.Max(ColumnCount, Math.Max(rowColumnCount, spanColumnCount)));
    }

    private static bool IsValidLocalSpan(TableCellSpan span, int rowCount, int columnCount) =>
        span.RowIndex >= 0
        && span.ColumnIndex >= 0
        && span.RowSpan >= 2
        && span.RowIndex < rowCount
        && (long)span.RowIndex + span.RowSpan <= rowCount
        && span.ColumnIndex < columnCount;

    private static string ReadCell(PreviewTablePageRowViewModel row, int columnIndex) =>
        columnIndex < row.Cells.Count ? row.Cells[columnIndex] ?? string.Empty : string.Empty;

    private static Border CreateCell(
        string text,
        ResolvedTableFormat format,
        int columnIndex)
    {
        var column = ResolveColumnFormat(format, columnIndex);
        var borderThickness = Math.Max(0d, format.BorderSizePoints) * PointsToDips;
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = Math.Max(1d, column.BodyFontSizePoints * PointsToDips),
            FontWeight = column.BodyBold ? FontWeights.Bold : FontWeights.Normal,
            Margin = new Thickness(
                ToDips(Math.Max(0d, format.CellMarginLeftMillimeters)),
                ToDips(Math.Max(0d, format.CellMarginTopMillimeters)),
                ToDips(Math.Max(0d, format.CellMarginRightMillimeters)),
                ToDips(Math.Max(0d, format.CellMarginBottomMillimeters))),
            TextAlignment = ProjectAlignment(column.BodyAlignment),
            TextWrapping = column.NoWrap ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextTrimming = column.NoWrap ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            VerticalAlignment = ProjectVerticalAlignment(column.VerticalAlignment),
            IsHitTestVisible = false
        };

        var fontFamily = CreateFontFamily(column.BodyFontFamilyName);
        if (fontFamily is not null)
            textBlock.FontFamily = fontFamily;

        return new Border
        {
            BorderBrush = CellBorderBrush,
            BorderThickness = new Thickness(0d, 0d, borderThickness, borderThickness),
            SnapsToDevicePixels = true,
            Child = textBlock
        };
    }

    private static ResolvedTableColumnFormat ResolveColumnFormat(
        ResolvedTableFormat format,
        int columnIndex) =>
        columnIndex >= 0 && columnIndex < format.Columns.Count
            ? format.Columns[columnIndex]
            : FallbackColumnFormat;

    private static TextAlignment ProjectAlignment(ParagraphAlignment alignment) => alignment switch
    {
        ParagraphAlignment.Center => TextAlignment.Center,
        ParagraphAlignment.Right => TextAlignment.Right,
        ParagraphAlignment.Justify => TextAlignment.Justify,
        _ => TextAlignment.Left
    };

    private static VerticalAlignment ProjectVerticalAlignment(VerticalContentAlignment alignment) => alignment switch
    {
        VerticalContentAlignment.Center => VerticalAlignment.Center,
        VerticalContentAlignment.Bottom => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Top
    };

    private static FontFamily? CreateFontFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        try
        {
            return new FontFamily(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static double ToDips(double millimeters) => millimeters * MillimetersToDips;

    private static ResolvedTableColumnFormat FallbackColumnFormat { get; } = new()
    {
        WidthWeight = 1d,
        HeaderAlignment = ParagraphAlignment.Left,
        BodyAlignment = ParagraphAlignment.Left,
        HeaderFontFamilyName = "Segoe UI",
        HeaderFontSizePoints = 10d,
        HeaderBold = true,
        BodyFontFamilyName = "Segoe UI",
        BodyFontSizePoints = 10d,
        BodyBold = false,
        VerticalAlignment = VerticalContentAlignment.Top,
        NoWrap = false
    };
}
