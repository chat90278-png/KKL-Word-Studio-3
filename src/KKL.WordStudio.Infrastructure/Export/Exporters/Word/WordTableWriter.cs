namespace KKL.WordStudio.Infrastructure.Export.Exporters.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;

/// <summary>Writes editable Word tables from the shared resolved table format and complete semantic spans.</summary>
internal static class WordTableWriter
{
    private const double TwipsPerMillimeter = 1440.0 / 25.4;
    private const int WordPercentageFullScale = 5000;
    private const int GridNormalizationTwips = 10000;

    public static Table BuildTable(TableContentNode tableNode)
    {
        ArgumentNullException.ThrowIfNull(tableNode);

        var table = new Table();
        var columnCount = Math.Max(
            tableNode.ColumnHeaders.Count,
            tableNode.Rows.Count == 0 ? 0 : tableNode.Rows.Max(row => row.Count));
        var mergeLookup = VerticalMergeLookup.Create(tableNode.CellSpans, tableNode.Rows.Count, columnCount);
        var layout = TableLayoutContext.Create(tableNode.Format, columnCount);

        table.AppendChild(BuildTableProperties(tableNode.Format));

        if (columnCount > 0)
        {
            var grid = new TableGrid();
            foreach (var width in layout.GridWidthsTwips)
                grid.AppendChild(new GridColumn { Width = width.ToString() });
            table.AppendChild(grid);
        }

        if (tableNode.ColumnHeaders.Count > 0)
        {
            table.AppendChild(BuildRow(
                tableNode.ColumnHeaders,
                isHeader: true,
                repeatAsHeader: tableNode.Format.RepeatHeader,
                columnCount,
                rowIndex: null,
                mergeLookup,
                layout,
                tableNode.Format.PreferredRowHeightMillimeters));
        }

        for (var rowIndex = 0; rowIndex < tableNode.Rows.Count; rowIndex++)
        {
            table.AppendChild(BuildRow(
                tableNode.Rows[rowIndex],
                isHeader: false,
                repeatAsHeader: false,
                columnCount,
                rowIndex,
                mergeLookup,
                layout,
                tableNode.Format.PreferredRowHeightMillimeters));
        }

        return table;
    }

    private static TableProperties BuildTableProperties(ResolvedTableFormat format)
    {
        var widthPercent = Math.Clamp(format.WidthPercent, 0d, 100d);
        var width = Math.Max(1, (int)Math.Round(widthPercent * 50d));
        var borderSize = Math.Max(0u, (uint)Math.Round(format.BorderSizePoints * 8d));
        var properties = new TableProperties();
        properties.AddChild(new TableWidth
        {
            Type = TableWidthUnitValues.Pct,
            Width = width.ToString()
        }, true);
        properties.AddChild(new TableBorders(
            // CT_TblBorders is sequence-sensitive: top, left, bottom, right,
            // insideH, insideV. Word may repair a different order on open, but
            // OpenXmlValidator correctly rejects that malformed document XML.
            new TopBorder { Val = BorderValues.Single, Size = borderSize },
            new LeftBorder { Val = BorderValues.Single, Size = borderSize },
            new BottomBorder { Val = BorderValues.Single, Size = borderSize },
            new RightBorder { Val = BorderValues.Single, Size = borderSize },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = borderSize },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = borderSize }), true);
        properties.AddChild(new TableLayout
        {
            Type = format.FixedLayout ? TableLayoutValues.Fixed : TableLayoutValues.Autofit
        }, true);
        properties.AddChild(new TableCellMarginDefault(
            new TopMargin
            {
                Width = ToTwips(format.CellMarginTopMillimeters).ToString(),
                Type = TableWidthUnitValues.Dxa
            },
            new TableCellLeftMargin
            {
                Width = (short)Math.Min(
                    ToTwips(format.CellMarginLeftMillimeters),
                    (uint)short.MaxValue),
                Type = TableWidthValues.Dxa
            },
            new BottomMargin
            {
                Width = ToTwips(format.CellMarginBottomMillimeters).ToString(),
                Type = TableWidthUnitValues.Dxa
            },
            new TableCellRightMargin
            {
                Width = (short)Math.Min(
                    ToTwips(format.CellMarginRightMillimeters),
                    (uint)short.MaxValue),
                Type = TableWidthValues.Dxa
            }), true);
        return properties;
    }

    private static TableRow BuildRow(
        IReadOnlyList<string> cellValues,
        bool isHeader,
        bool repeatAsHeader,
        int columnCount,
        int? rowIndex,
        VerticalMergeLookup mergeLookup,
        TableLayoutContext layout,
        double preferredRowHeightMillimeters)
    {
        var rowProperties = new TableRowProperties();
        if (preferredRowHeightMillimeters > 0d)
        {
            rowProperties.AppendChild(new TableRowHeight
            {
                Val = ToTwips(preferredRowHeightMillimeters),
                HeightType = HeightRuleValues.AtLeast
            });
        }
        if (repeatAsHeader)
            rowProperties.AppendChild(new TableHeader());

        var row = new TableRow(rowProperties);
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var mergeState = rowIndex is int dataRowIndex
                ? mergeLookup.GetState(dataRowIndex, columnIndex)
                : null;
            var value = mergeState == MergeState.Continue
                ? string.Empty
                : columnIndex < cellValues.Count ? cellValues[columnIndex] : string.Empty;
            var columnFormat = layout.ColumnFormats[columnIndex];
            var cellProperties = new TableCellProperties();
            cellProperties.AddChild(new TableCellWidth
            {
                Type = TableWidthUnitValues.Pct,
                Width = layout.CellWidthsPct[columnIndex].ToString()
            }, true);

            if (mergeState is not null)
            {
                cellProperties.AddChild(new VerticalMerge
                {
                    Val = mergeState == MergeState.Restart
                        ? MergedCellValues.Restart
                        : MergedCellValues.Continue
                }, true);
            }

            if (columnFormat.NoWrap)
                cellProperties.AddChild(new NoWrap(), true);

            if (layout.UsesResolvedColumnProfile)
            {
                cellProperties.AddChild(new TableCellVerticalAlignment
                {
                    Val = ToVerticalAlignment(columnFormat.VerticalAlignment)
                }, true);
            }
            else if (mergeState is not null)
            {
                // Sprint 15 compatibility for direct/default table nodes: merged identity
                // cells were explicitly centered while ordinary cells had no tcVAlign.
                cellProperties.AddChild(new TableCellVerticalAlignment
                {
                    Val = TableVerticalAlignmentValues.Center
                }, true);
            }

            var paragraphProperties = new ParagraphProperties();
            paragraphProperties.AddChild(new Justification
            {
                Val = WordParagraphWriter.ToJustification(
                    isHeader ? columnFormat.HeaderAlignment : columnFormat.BodyAlignment)
            }, true);
            var runProperties = WordParagraphWriter.BuildRunProperties(
                isHeader ? columnFormat.HeaderFontFamilyName : columnFormat.BodyFontFamilyName,
                isHeader ? columnFormat.HeaderFontSizePoints : columnFormat.BodyFontSizePoints,
                isHeader ? columnFormat.HeaderBold : columnFormat.BodyBold,
                italic: false,
                underline: false,
                foregroundColor: "000000");

            var cell = new TableCell(
                cellProperties,
                new Paragraph(
                    paragraphProperties,
                    new Run(
                        runProperties,
                        new Text(value) { Space = SpaceProcessingModeValues.Preserve })));
            row.AppendChild(cell);
        }

        return row;
    }

    private static uint ToTwips(double millimeters) =>
        Math.Max(0u, (uint)Math.Round(Math.Max(0d, millimeters) * TwipsPerMillimeter));

    private static TableVerticalAlignmentValues ToVerticalAlignment(VerticalContentAlignment alignment) => alignment switch
    {
        VerticalContentAlignment.Bottom => TableVerticalAlignmentValues.Bottom,
        VerticalContentAlignment.Center => TableVerticalAlignmentValues.Center,
        _ => TableVerticalAlignmentValues.Top
    };

    private enum MergeState
    {
        Restart,
        Continue
    }

    private sealed class TableLayoutContext
    {
        private TableLayoutContext(
            IReadOnlyList<uint> gridWidthsTwips,
            IReadOnlyList<int> cellWidthsPct,
            IReadOnlyList<ResolvedTableColumnFormat> columnFormats,
            bool usesResolvedColumnProfile)
        {
            GridWidthsTwips = gridWidthsTwips;
            CellWidthsPct = cellWidthsPct;
            ColumnFormats = columnFormats;
            UsesResolvedColumnProfile = usesResolvedColumnProfile;
        }

        public IReadOnlyList<uint> GridWidthsTwips { get; }
        public IReadOnlyList<int> CellWidthsPct { get; }
        public IReadOnlyList<ResolvedTableColumnFormat> ColumnFormats { get; }
        public bool UsesResolvedColumnProfile { get; }

        public static TableLayoutContext Create(ResolvedTableFormat format, int columnCount)
        {
            if (columnCount <= 0)
            {
                return new TableLayoutContext(
                    Array.Empty<uint>(),
                    Array.Empty<int>(),
                    Array.Empty<ResolvedTableColumnFormat>(),
                    usesResolvedColumnProfile: false);
            }

            var hasValidProfile = format.Columns.Count == columnCount
                && format.Columns.All(column => double.IsFinite(column.WidthWeight) && column.WidthWeight > 0d);
            var columnFormats = hasValidProfile
                ? format.Columns.ToArray()
                : Enumerable.Range(0, columnCount).Select(_ => CreateFallbackColumnFormat()).ToArray();
            var weights = hasValidProfile
                ? columnFormats.Select(column => column.WidthWeight).ToArray()
                : Enumerable.Repeat(1d, columnCount).ToArray();
            var totalWeight = weights.Sum();

            var gridWidths = weights
                .Select(weight => Math.Max(1u, (uint)Math.Round(weight / totalWeight * GridNormalizationTwips)))
                .ToArray();
            var cellWidths = weights
                .Select(weight => Math.Max(1, (int)Math.Round(weight / totalWeight * WordPercentageFullScale)))
                .ToArray();

            return new TableLayoutContext(gridWidths, cellWidths, columnFormats, hasValidProfile);
        }

        private static ResolvedTableColumnFormat CreateFallbackColumnFormat() => new()
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

    /// <summary>
    /// Resolves complete semantic-table spans to unambiguous Word merge states.
    /// Invalid or overlapping metadata is ignored rather than emitting malformed/conflicting vMerge XML.
    /// </summary>
    private sealed class VerticalMergeLookup
    {
        private readonly IReadOnlyDictionary<(int RowIndex, int ColumnIndex), MergeState> states;

        private VerticalMergeLookup(IReadOnlyDictionary<(int RowIndex, int ColumnIndex), MergeState> states)
        {
            this.states = states;
        }

        public static VerticalMergeLookup Create(
            IReadOnlyList<TableCellSpan> spans,
            int rowCount,
            int columnCount)
        {
            var candidates = new List<SpanCandidate>();
            var occupancy = new Dictionary<(int RowIndex, int ColumnIndex), int>();

            foreach (var span in spans)
            {
                if (!IsValid(span, rowCount, columnCount))
                    continue;

                var coordinates = Enumerable.Range(span.RowIndex, span.RowSpan)
                    .Select(rowIndex => (RowIndex: rowIndex, ColumnIndex: span.ColumnIndex))
                    .ToArray();
                candidates.Add(new SpanCandidate(span, coordinates));

                foreach (var coordinate in coordinates)
                    occupancy[coordinate] = occupancy.GetValueOrDefault(coordinate) + 1;
            }

            var states = new Dictionary<(int RowIndex, int ColumnIndex), MergeState>();
            foreach (var candidate in candidates)
            {
                if (candidate.Coordinates.Any(coordinate => occupancy[coordinate] > 1))
                    continue;

                states[(candidate.Span.RowIndex, candidate.Span.ColumnIndex)] = MergeState.Restart;
                foreach (var coordinate in candidate.Coordinates.Skip(1))
                    states[coordinate] = MergeState.Continue;
            }

            return new VerticalMergeLookup(states);
        }

        public MergeState? GetState(int rowIndex, int columnIndex) =>
            states.TryGetValue((rowIndex, columnIndex), out var state) ? state : null;

        private static bool IsValid(TableCellSpan span, int rowCount, int columnCount)
        {
            if (span.RowSpan < 2 || span.RowIndex < 0 || span.ColumnIndex < 0)
                return false;
            if (span.RowIndex >= rowCount || span.ColumnIndex >= columnCount)
                return false;

            var endExclusive = (long)span.RowIndex + span.RowSpan;
            return endExclusive <= rowCount;
        }

        private sealed record SpanCandidate(
            TableCellSpan Span,
            IReadOnlyList<(int RowIndex, int ColumnIndex)> Coordinates);
    }
}
