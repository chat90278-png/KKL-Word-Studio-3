namespace KKL.WordStudio.UI.ViewModels;

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;

/// <summary>
/// Testable, presentation-only mapping from the frozen Application layout
/// contract to WPF page/block ViewModels. It never derives pagination or
/// report structure.
/// </summary>
public static class PreviewPageProjection
{
    public const double MillimetersToDips = 96.0 / 25.4;
    private const double PointsToDips = 96.0 / 72.0;

    public static PreviewPageViewModel Project(DocumentPageLayout page) => new()
    {
        PageNumber = page.PageNumber,
        Origin = page.Origin,
        Width = ToDips(page.PageLayout.WidthMillimeters),
        Height = ToDips(page.PageLayout.HeightMillimeters),
        Blocks = page.Blocks.Select(ProjectBlock).ToList()
    };

    public static PreviewPageBlockViewModel ProjectBlock(PositionedPageBlock block)
    {
        var geometry = new BlockGeometry(block);

        return block.Payload switch
        {
            TextPageBlockPayload text => new PreviewTextPageBlockViewModel
            {
                ElementId = block.ElementId,
                Region = block.Region,
                Kind = block.Kind,
                X = geometry.X,
                Y = geometry.Y,
                Width = geometry.Width,
                Height = geometry.Height,
                FragmentIndex = block.FragmentIndex,
                IsContinuation = block.IsContinuation,
                IsEditableReportElement = block.IsEditableReportElement,
                Runs = text.Runs.Select(ProjectRun).ToList(),
                SemanticKind = text.SemanticKind,
                Alignment = ProjectAlignment(ResolveTextAlignment(text)),
                FontFamily = CreateFontFamily(text.Format.FontFamilyName),
                FontSize = Math.Max(1.0, text.Format.FontSizePoints * PointsToDips),
                FontWeight = text.Format.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = text.Format.Italic ? FontStyles.Italic : FontStyles.Normal,
                TextDecorations = text.Format.Underline ? System.Windows.TextDecorations.Underline : null,
                Foreground = CreateBrush(text.Format.ForegroundColor),
                LineHeight = CalculateLineHeight(text),
                FirstLineIndent = text.SemanticKind is not null && block.FragmentIndex == 0
                    ? ToDips(Math.Max(0d, text.Format.FirstLineIndentMillimeters))
                    : 0d,
                PlainText = string.Concat(text.Runs.Select(run => run.Text))
            },

            TablePageBlockPayload table => new PreviewTablePageBlockViewModel
            {
                ElementId = block.ElementId,
                Region = block.Region,
                Kind = block.Kind,
                X = geometry.X,
                Y = geometry.Y,
                Width = geometry.Width,
                Height = geometry.Height,
                FragmentIndex = block.FragmentIndex,
                IsContinuation = block.IsContinuation,
                IsEditableReportElement = block.IsEditableReportElement,
                Name = table.Name,
                Caption = table.Caption,
                CaptionFormat = table.CaptionFormat,
                CaptionRuns = [ProjectCaptionRun(ResolveCaptionDisplayText(table), table.CaptionFormat)],
                CaptionAlignment = ProjectAlignment(table.CaptionFormat?.Alignment ?? ParagraphAlignment.Left),
                CaptionFontFamily = CreateFontFamily(table.CaptionFormat?.FontFamilyName),
                CaptionFontSize = ResolveCaptionFontSize(table.CaptionFormat),
                CaptionFontWeight = table.CaptionFormat is null
                    ? FontWeights.SemiBold
                    : table.CaptionFormat.Bold ? FontWeights.Bold : FontWeights.Normal,
                CaptionFontStyle = table.CaptionFormat?.Italic == true ? FontStyles.Italic : FontStyles.Normal,
                CaptionTextDecorations = table.CaptionFormat?.Underline == true ? System.Windows.TextDecorations.Underline : null,
                CaptionForeground = CreateBrush(table.CaptionFormat?.ForegroundColor) ?? Brushes.Black,
                CaptionLineHeight = ResolveCaptionLineHeight(table.CaptionFormat),
                CaptionFirstLineIndent = table.CaptionFormat is null
                    ? 0d
                    : ToDips(Math.Max(0d, table.CaptionFormat.FirstLineIndentMillimeters)),
                CaptionAreaMargin = table.CaptionFormat is null
                    ? new Thickness(1d, 0d, 1d, 3d)
                    : new Thickness(1d, 0d, 1d, 0d),
                Columns = table.ColumnHeaders.Select((header, index) => ProjectTableColumn(
                    block,
                    table.Format,
                    header,
                    index)).ToList(),
                Rows = table.Rows.Select(row => new PreviewTablePageRowViewModel { Cells = row }).ToList(),
                CellSpans = table.CellSpans,
                Format = table.Format,
                TableBorderThickness = UniformPointThickness(table.Format.BorderSizePoints),
                StartRowIndex = table.StartRowIndex,
                HasHeader = table.HasHeader,
                IsHeaderRepeated = table.IsHeaderRepeated,
                SourceError = table.SourceError
            },

            TocPageBlockPayload toc => new PreviewTocPageBlockViewModel
            {
                ElementId = block.ElementId,
                Region = block.Region,
                Kind = block.Kind,
                X = geometry.X,
                Y = geometry.Y,
                Width = geometry.Width,
                Height = geometry.Height,
                FragmentIndex = block.FragmentIndex,
                IsContinuation = block.IsContinuation,
                IsEditableReportElement = block.IsEditableReportElement,
                Entries = toc.Entries.Select(entry => new PreviewTocEntryViewModel
                {
                    ElementId = entry.ElementId,
                    Text = entry.Text,
                    Level = entry.Level,
                    PageNumber = entry.PageNumber
                }).ToList()
            },

            ImagePageBlockPayload image => new PreviewImagePageBlockViewModel
            {
                ElementId = block.ElementId,
                Region = block.Region,
                Kind = block.Kind,
                X = geometry.X,
                Y = geometry.Y,
                Width = geometry.Width,
                Height = geometry.Height,
                FragmentIndex = block.FragmentIndex,
                IsContinuation = block.IsContinuation,
                IsEditableReportElement = block.IsEditableReportElement,
                Name = image.Name,
                ImageSource = DecodeImage(image.ImageBytes)
            },

            PageNumberPageBlockPayload pageNumber => new PreviewPageNumberBlockViewModel
            {
                ElementId = block.ElementId,
                Region = block.Region,
                Kind = block.Kind,
                X = geometry.X,
                Y = geometry.Y,
                Width = geometry.Width,
                Height = geometry.Height,
                FragmentIndex = block.FragmentIndex,
                IsContinuation = block.IsContinuation,
                IsEditableReportElement = block.IsEditableReportElement,
                PageNumber = pageNumber.PageNumber
            },

            UnsupportedPageBlockPayload unsupported => new PreviewUnsupportedPageBlockViewModel
            {
                ElementId = block.ElementId,
                Region = block.Region,
                Kind = block.Kind,
                X = geometry.X,
                Y = geometry.Y,
                Width = geometry.Width,
                Height = geometry.Height,
                FragmentIndex = block.FragmentIndex,
                IsContinuation = block.IsContinuation,
                IsEditableReportElement = block.IsEditableReportElement,
                Description = unsupported.Description
            },

            _ => throw new NotSupportedException($"Desteklenmeyen sayfa bloğu payload türü: {block.Payload.GetType().Name}")
        };
    }

    public static double ToDips(double millimeters) => millimeters * MillimetersToDips;

    private static string ResolveCaptionDisplayText(TablePageBlockPayload table) =>
        TableCaptionSequenceFormatter.BuildDisplayText(
            table.Caption ?? string.Empty,
            table.CaptionSequence,
            table.CaptionSequenceNumber);

    private static PreviewTablePageColumnViewModel ProjectTableColumn(
        PositionedPageBlock block,
        ResolvedTableFormat format,
        string header,
        int index)
    {
        var column = ResolveColumnFormat(format, index);
        var borderThickness = Math.Max(0d, format.BorderSizePoints) * PointsToDips;
        return new PreviewTablePageColumnViewModel
        {
            TableElementId = block.IsEditableReportElement ? block.ElementId : null,
            Index = index,
            IsEditable = block.IsEditableReportElement && block.ElementId.HasValue,
            WidthWeight = column.WidthWeight > 0d ? column.WidthWeight : 1d,
            HeaderAlignment = ProjectAlignment(column.HeaderAlignment),
            HeaderFontFamily = CreateFontFamily(column.HeaderFontFamilyName),
            HeaderFontSize = Math.Max(1d, column.HeaderFontSizePoints * PointsToDips),
            HeaderFontWeight = column.HeaderBold ? FontWeights.Bold : FontWeights.Normal,
            VerticalAlignment = ProjectVerticalAlignment(column.VerticalAlignment),
            TextWrapping = column.NoWrap ? System.Windows.TextWrapping.NoWrap : System.Windows.TextWrapping.Wrap,
            CellMargin = new Thickness(
                ToDips(Math.Max(0d, format.CellMarginLeftMillimeters)),
                ToDips(Math.Max(0d, format.CellMarginTopMillimeters)),
                ToDips(Math.Max(0d, format.CellMarginRightMillimeters)),
                ToDips(Math.Max(0d, format.CellMarginBottomMillimeters))),
            PreferredRowHeight = ToDips(Math.Max(0d, format.PreferredRowHeightMillimeters)),
            CellBorderThickness = new Thickness(0d, 0d, borderThickness, borderThickness),
            Header = header
        };
    }

    private static ResolvedTableColumnFormat ResolveColumnFormat(ResolvedTableFormat format, int index) =>
        index >= 0 && index < format.Columns.Count
            ? format.Columns[index]
            : FallbackColumnFormat;

    private static VerticalAlignment ProjectVerticalAlignment(VerticalContentAlignment alignment) => alignment switch
    {
        VerticalContentAlignment.Center => VerticalAlignment.Center,
        VerticalContentAlignment.Bottom => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Top
    };

    private static ParagraphAlignment ResolveTextAlignment(TextPageBlockPayload text) =>
        text.SemanticKind is null ? text.Alignment : text.Format.Alignment;

    private static double CalculateLineHeight(TextPageBlockPayload text)
    {
        if (text.SemanticKind is null)
        {
            var maxRunSizePoints = text.Runs.Count == 0
                ? text.Format.FontSizePoints
                : text.Runs.Max(run => run.FontSizePoints > 0d ? run.FontSizePoints : text.Format.FontSizePoints);
            return Math.Max(1d, maxRunSizePoints * PointsToDips * 1.25d);
        }

        var spacing = text.Format.LineSpacingMultiple > 0d ? text.Format.LineSpacingMultiple : 1d;
        return Math.Max(1d, text.Format.FontSizePoints * PointsToDips * 1.25d * spacing);
    }

    private static Thickness UniformPointThickness(double points)
    {
        var dips = Math.Max(0d, points) * PointsToDips;
        return new Thickness(dips);
    }

    private static Brush? CreateBrush(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            if (new BrushConverter().ConvertFromString(value) is not Brush brush)
                return null;

            if (brush.CanFreeze)
                brush.Freeze();
            return brush;
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
            return null;
        }
    }

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

    private static double ResolveCaptionFontSize(ResolvedTextFormat? format) =>
        format is null
            ? 12d
            : Math.Max(1d, format.FontSizePoints * PointsToDips);

    private static double ResolveCaptionLineHeight(ResolvedTextFormat? format)
    {
        if (format is null)
            return 15d;

        var spacing = format.LineSpacingMultiple > 0d ? format.LineSpacingMultiple : 1d;
        return Math.Max(1d, format.FontSizePoints * PointsToDips * 1.25d * spacing);
    }

    private static PreviewTextRunViewModel ProjectCaptionRun(string text, ResolvedTextFormat? format) => new()
    {
        Text = text,
        FontWeight = format is null
            ? FontWeights.SemiBold
            : format.Bold ? FontWeights.Bold : FontWeights.Normal,
        FontStyle = format?.Italic == true ? FontStyles.Italic : FontStyles.Normal,
        TextDecorations = format?.Underline == true ? System.Windows.TextDecorations.Underline : null,
        FontSize = ResolveCaptionFontSize(format),
        FontFamily = CreateFontFamily(format?.FontFamilyName)
    };

    private static PreviewTextRunViewModel ProjectRun(TextRunLayout run) => new()
    {
        Text = run.Text,
        FontWeight = run.Bold ? FontWeights.Bold : FontWeights.Normal,
        FontStyle = run.Italic ? FontStyles.Italic : FontStyles.Normal,
        TextDecorations = run.Underline ? System.Windows.TextDecorations.Underline : null,
        FontSize = Math.Max(1.0, run.FontSizePoints * PointsToDips),
        FontFamily = CreateFontFamily(run.FontFamilyName)
    };

    private static TextAlignment ProjectAlignment(ParagraphAlignment alignment) => alignment switch
    {
        ParagraphAlignment.Center => TextAlignment.Center,
        ParagraphAlignment.Right => TextAlignment.Right,
        ParagraphAlignment.Justify => TextAlignment.Justify,
        _ => TextAlignment.Left
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

    private static ImageSource? DecodeImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private readonly record struct BlockGeometry(double X, double Y, double Width, double Height)
    {
        public BlockGeometry(PositionedPageBlock block)
            : this(
                ToDips(block.XMillimeters),
                ToDips(block.YMillimeters),
                ToDips(block.WidthMillimeters),
                ToDips(block.HeightMillimeters))
        {
        }
    }
}
