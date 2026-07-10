namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;

internal sealed class DeterministicTablePaginator
{
    private const double HeightTolerance = 0.0001d;
    private readonly DeterministicTextMeasurement _measurement;

    public DeterministicTablePaginator(DeterministicTextMeasurement measurement) =>
        _measurement = measurement;

    public void Layout(
        LayoutPageFlow flow,
        Guid? elementId,
        string name,
        string? caption,
        ResolvedTextFormat? captionFormat,
        TableCaptionSequenceProfile? captionSequence,
        int? captionSequenceNumber,
        IReadOnlyList<string> columnHeaders,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<TableCellSpan> cellSpans,
        IReadOnlyList<TableRowGroup> rowGroups,
        string? sourceError,
        bool isEditable,
        bool repeatHeader,
        ResolvedTableFormat format,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var rowIndex = 0;
        var fragmentIndex = 0;
        var emittedEmptyFragment = false;
        var columnCount = Math.Max(
            1,
            Math.Max(columnHeaders.Count, rows.Count == 0 ? 0 : rows.Max(row => row.Count)));
        ArgumentNullException.ThrowIfNull(format);
        var tableWidthMillimeters = ResolveTableWidth(flow.ContentWidthMillimeters, format.WidthPercent);
        var validSpans = ValidateSemanticSpans(name, rows, cellSpans, columnCount, warnings);
        var keepTogetherGroups = BuildKeepTogetherGroupLookup(name, rows, rowGroups, warnings);

        while (rowIndex < rows.Count || !emittedEmptyFragment)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (flow.RemainingBodyHeightMillimeters <= 0d)
                flow.NewPage();

            var metrics = CreateFragmentMetrics(
                fragmentIndex,
                caption,
                captionFormat,
                columnHeaders,
                sourceError,
                repeatHeader,
                format,
                tableWidthMillimeters);

            if (rowIndex < rows.Count)
            {
                var firstRowHeight = EstimateRowHeight(rows[rowIndex], columnCount, tableWidthMillimeters, format);
                var minimumHeight = metrics.OverheadHeight + firstRowHeight;
                if (!flow.IsAtBodyTop && minimumHeight > flow.RemainingBodyHeightMillimeters)
                {
                    flow.NewPage();
                    continue;
                }
            }
            else if (!flow.IsAtBodyTop && metrics.OverheadHeight > flow.RemainingBodyHeightMillimeters)
            {
                flow.NewPage();
                continue;
            }

            var startRowIndex = rowIndex;
            var rowsHeight = 0d;
            var availableForRows = Math.Max(0d, flow.RemainingBodyHeightMillimeters - metrics.OverheadHeight);
            var restartOnFreshPage = false;

            while (rowIndex < rows.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (keepTogetherGroups.TryGetValue(rowIndex, out var group))
                {
                    var groupHeight = EstimateRowsHeight(
                        rows,
                        group.StartRowIndex,
                        group.RowCount,
                        columnCount,
                        tableWidthMillimeters,
                        format);
                    var currentRemainingForRows = Math.Max(0d, availableForRows - rowsHeight);
                    var freshFragmentIndex = rowsHeight > 0d ? fragmentIndex + 1 : fragmentIndex;
                    var freshOverhead = CreateFragmentMetrics(
                        freshFragmentIndex,
                        caption,
                        captionFormat,
                        columnHeaders,
                        sourceError,
                        repeatHeader,
                        format,
                        tableWidthMillimeters).OverheadHeight;
                    var freshRowsCapacity = Math.Max(0d, flow.BodyHeightMillimeters - freshOverhead);

                    if (groupHeight <= freshRowsCapacity + HeightTolerance)
                    {
                        if (groupHeight <= currentRemainingForRows + HeightTolerance)
                        {
                            rowsHeight += groupHeight;
                            rowIndex += group.RowCount;
                            continue;
                        }

                        if (rowsHeight > 0d)
                            break;

                        if (!flow.IsAtBodyTop)
                        {
                            restartOnFreshPage = true;
                            break;
                        }
                    }
                }

                var candidateHeight = EstimateRowHeight(rows[rowIndex], columnCount, tableWidthMillimeters, format);
                if (rowsHeight + candidateHeight > availableForRows + HeightTolerance)
                    break;

                rowsHeight += candidateHeight;
                rowIndex++;
            }

            if (restartOnFreshPage)
            {
                flow.NewPage();
                continue;
            }

            if (rowIndex == startRowIndex && rowIndex < rows.Count)
            {
                var candidateRowHeight = EstimateRowHeight(rows[rowIndex], columnCount, tableWidthMillimeters, format);

                if (!flow.IsAtBodyTop)
                {
                    flow.NewPage();
                    continue;
                }

                if (candidateRowHeight > flow.BodyHeightMillimeters)
                {
                    rowsHeight = candidateRowHeight;
                    rowIndex++;
                    AddWarningOnce(
                        warnings,
                        $"'{name}' tablosunun {rowIndex - 1}. veri satırı sayfa gövde yüksekliğini aşıyor; satır tek başına yerleştirildi.");
                }
                else if (fragmentIndex == 0
                         && metrics.OverheadHeight > 0d
                         && metrics.OverheadHeight <= flow.RemainingBodyHeightMillimeters)
                {
                    flow.AddBodyBlock(
                        CreateBlock(
                            flow,
                            elementId,
                            name,
                            metrics.FragmentCaption,
                            captionFormat,
                            captionSequence,
                            captionSequenceNumber,
                            columnHeaders,
                            [],
                            [],
                            startRowIndex,
                            metrics.HasHeader,
                            metrics.IsHeaderRepeated,
                            sourceError,
                            fragmentIndex,
                            isEditable,
                            format,
                            tableWidthMillimeters,
                            Math.Min(metrics.OverheadHeight, flow.BodyHeightMillimeters)),
                        addGapAfter: false);
                    fragmentIndex++;
                    emittedEmptyFragment = true;
                    flow.NewPage();
                    continue;
                }
                else
                {
                    rowsHeight = Math.Max(1d, flow.BodyHeightMillimeters - metrics.OverheadHeight);
                    rowIndex++;
                    AddWarningOnce(
                        warnings,
                        $"'{name}' tablosunda başlık/tekrarlanan başlık ile satır birlikte gövde yüksekliğini aştı; görünür yükseklik gövdeye sınırlandı.");
                }
            }

            var projectedFragment = ProjectFragmentRowsAndSpans(
                rows,
                validSpans,
                startRowIndex,
                rowIndex - startRowIndex);
            var blockHeight = metrics.OverheadHeight + rowsHeight;
            if (projectedFragment.Rows.Count == 0)
                blockHeight = Math.Max(8d, blockHeight);

            flow.AddBodyBlock(
                CreateBlock(
                    flow,
                    elementId,
                    name,
                    metrics.FragmentCaption,
                    captionFormat,
                    captionSequence,
                    captionSequenceNumber,
                    columnHeaders,
                    projectedFragment.Rows,
                    projectedFragment.CellSpans,
                    startRowIndex,
                    metrics.HasHeader,
                    metrics.IsHeaderRepeated,
                    sourceError,
                    fragmentIndex,
                    isEditable,
                    format,
                    tableWidthMillimeters,
                    blockHeight),
                addGapAfter: rowIndex >= rows.Count);

            emittedEmptyFragment = true;
            fragmentIndex++;
            if (rowIndex < rows.Count)
                flow.NewPage();
            else
                break;
        }
    }

    public double EstimateMinimumFragmentHeight(
        string? caption,
        ResolvedTextFormat? captionFormat,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<TableRowGroup> rowGroups,
        ResolvedTableFormat format,
        double contentWidthMillimeters,
        double freshBodyHeightMillimeters)
    {
        ArgumentNullException.ThrowIfNull(format);
        var tableWidthMillimeters = ResolveTableWidth(contentWidthMillimeters, format.WidthPercent);
        var height = EstimateCaptionHeight(caption, captionFormat, tableWidthMillimeters)
            + EstimateHeaderHeight(headers, tableWidthMillimeters, format);
        if (rows.Count == 0)
            return Math.Max(6d, height);

        var columnCount = Math.Max(
            1,
            Math.Max(headers.Count, rows.Max(row => row.Count)));
        var rowContribution = EstimateRowHeight(rows[0], columnCount, tableWidthMillimeters, format);
        var firstGroup = rowGroups.FirstOrDefault(group =>
            group.KeepTogetherWhenPossible
            && group.StartRowIndex == 0
            && IsValidRowGroup(group, rows.Count));

        if (firstGroup is not null)
        {
            var groupHeight = EstimateRowsHeight(
                rows,
                firstGroup.StartRowIndex,
                firstGroup.RowCount,
                columnCount,
                tableWidthMillimeters,
                format);
            if (height + groupHeight <= freshBodyHeightMillimeters + HeightTolerance)
                rowContribution = groupHeight;
        }

        return Math.Max(6d, height + rowContribution);
    }

    public double EstimateAtomicHeight(
        string? caption,
        ResolvedTextFormat? captionFormat,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        ResolvedTableFormat format,
        double contentWidthMillimeters)
    {
        ArgumentNullException.ThrowIfNull(format);
        var tableWidthMillimeters = ResolveTableWidth(contentWidthMillimeters, format.WidthPercent);
        var columnCount = Math.Max(
            1,
            Math.Max(headers.Count, rows.Count == 0 ? 0 : rows.Max(row => row.Count)));
        return Math.Max(
            8d,
            EstimateCaptionHeight(caption, captionFormat, tableWidthMillimeters)
            + EstimateHeaderHeight(headers, tableWidthMillimeters, format)
            + rows.Sum(row => EstimateRowHeight(row, columnCount, tableWidthMillimeters, format)));
    }

    private FragmentMetrics CreateFragmentMetrics(
        int fragmentIndex,
        string? caption,
        ResolvedTextFormat? captionFormat,
        IReadOnlyList<string> columnHeaders,
        string? sourceError,
        bool repeatHeader,
        ResolvedTableFormat format,
        double tableWidthMillimeters)
    {
        var isFirstFragment = fragmentIndex == 0;
        var fragmentCaption = isFirstFragment ? caption : null;
        var hasHeader = columnHeaders.Count > 0;
        var isHeaderRepeated = hasHeader && repeatHeader && !isFirstFragment;
        var captionHeight = EstimateCaptionHeight(fragmentCaption, captionFormat, tableWidthMillimeters);
        var headerHeight = hasHeader
            ? EstimateHeaderHeight(columnHeaders, tableWidthMillimeters, format)
            : 0d;
        var sourceErrorHeight = !string.IsNullOrWhiteSpace(sourceError) && isFirstFragment
            ? _measurement.EstimatePlainTextHeight(sourceError, 10d, tableWidthMillimeters)
            : 0d;

        return new FragmentMetrics(
            fragmentCaption,
            hasHeader,
            isHeaderRepeated,
            captionHeight + headerHeight + sourceErrorHeight);
    }

    private double EstimateRowsHeight(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int startRowIndex,
        int rowCount,
        int columnCount,
        double tableWidthMillimeters,
        ResolvedTableFormat format)
    {
        var height = 0d;
        var endRowIndex = Math.Min(rows.Count, startRowIndex + rowCount);
        for (var index = startRowIndex; index < endRowIndex; index++)
            height += EstimateRowHeight(rows[index], columnCount, tableWidthMillimeters, format);

        return height;
    }

    private double EstimateRowHeight(
        IReadOnlyList<string> row,
        int columnCount,
        double tableWidthMillimeters,
        ResolvedTableFormat format) =>
        EstimateFormattedRowHeight(row, columnCount, tableWidthMillimeters, format, isHeader: false);

    private double EstimateCaptionHeight(
        string? caption,
        ResolvedTextFormat? captionFormat,
        double widthMillimeters)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return 0d;

        if (captionFormat is null)
            return _measurement.EstimatePlainTextHeight(caption, 11d, widthMillimeters, bold: true) + 1d;

        var measured = _measurement.MeasureResolvedText(caption, captionFormat, widthMillimeters);
        return DeterministicTextMeasurement.PointsToMillimeters(captionFormat.SpaceBeforePoints)
            + measured.TotalHeightMillimeters
            + DeterministicTextMeasurement.PointsToMillimeters(captionFormat.SpaceAfterPoints);
    }

    private double EstimateHeaderHeight(
        IReadOnlyList<string> headers,
        double tableWidthMillimeters,
        ResolvedTableFormat format) =>
        headers.Count == 0
            ? 0d
            : EstimateFormattedRowHeight(headers, headers.Count, tableWidthMillimeters, format, isHeader: true);

    private double EstimateFormattedRowHeight(
        IReadOnlyList<string> cells,
        int columnCount,
        double tableWidthMillimeters,
        ResolvedTableFormat format,
        bool isHeader)
    {
        if (format.Columns.Count == 0
            && format.PreferredRowHeightMillimeters <= 0d
            && format.CellMarginTopMillimeters <= 0d
            && format.CellMarginBottomMillimeters <= 0d
            && format.CellMarginLeftMillimeters <= 0d
            && format.CellMarginRightMillimeters <= 0d)
        {
            return _measurement.EstimateTableRowHeight(
                cells,
                columnCount,
                tableWidthMillimeters,
                bold: isHeader);
        }

        var widths = ResolveColumnWidths(format, columnCount, tableWidthMillimeters);
        var verticalMargins = Math.Max(0d, format.CellMarginTopMillimeters)
            + Math.Max(0d, format.CellMarginBottomMillimeters);
        var horizontalMargins = Math.Max(0d, format.CellMarginLeftMillimeters)
            + Math.Max(0d, format.CellMarginRightMillimeters);
        var maxHeight = 0d;

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var column = ResolveColumnFormat(format, columnIndex);
            var text = columnIndex < cells.Count ? cells[columnIndex] : string.Empty;
            var fontSize = isHeader ? column.HeaderFontSizePoints : column.BodyFontSizePoints;
            var fontFamily = isHeader ? column.HeaderFontFamilyName : column.BodyFontFamilyName;
            var bold = isHeader ? column.HeaderBold : column.BodyBold;
            var usableWidth = Math.Max(1d, widths[columnIndex] - horizontalMargins);
            var textHeight = _measurement.EstimatePlainTextHeight(
                text,
                fontSize,
                usableWidth,
                bold,
                fontFamily,
                noWrap: column.NoWrap);
            maxHeight = Math.Max(maxHeight, textHeight + verticalMargins);
        }

        return Math.Max(Math.Max(0d, format.PreferredRowHeightMillimeters), maxHeight);
    }

    private static IReadOnlyList<double> ResolveColumnWidths(
        ResolvedTableFormat format,
        int columnCount,
        double tableWidthMillimeters)
    {
        var normalizedColumnCount = Math.Max(1, columnCount);
        var weights = Enumerable.Range(0, normalizedColumnCount)
            .Select(index => index < format.Columns.Count && format.Columns[index].WidthWeight > 0d
                ? format.Columns[index].WidthWeight
                : 1d)
            .ToArray();
        var weightTotal = weights.Sum();
        if (weightTotal <= 0d)
            return Enumerable.Repeat(tableWidthMillimeters / normalizedColumnCount, normalizedColumnCount).ToArray();

        return weights
            .Select(weight => Math.Max(1d, tableWidthMillimeters * weight / weightTotal))
            .ToArray();
    }

    private static ResolvedTableColumnFormat ResolveColumnFormat(
        ResolvedTableFormat format,
        int columnIndex) =>
        columnIndex >= 0 && columnIndex < format.Columns.Count
            ? format.Columns[columnIndex]
            : FallbackColumnFormat;

    private static double ResolveTableWidth(double contentWidthMillimeters, double widthPercent)
    {
        var normalizedPercent = widthPercent > 0d ? Math.Min(widthPercent, 100d) : 100d;
        return Math.Max(1d, contentWidthMillimeters * normalizedPercent / 100d);
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

    private static IReadOnlyList<TableCellSpan> ValidateSemanticSpans(
        string tableName,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<TableCellSpan> spans,
        int columnCount,
        List<string> warnings)
    {
        var valid = new List<TableCellSpan>();
        foreach (var span in spans)
        {
            var endExclusive = (long)span.RowIndex + span.RowSpan;
            var isValid = span.RowIndex >= 0
                          && span.ColumnIndex >= 0
                          && span.RowSpan >= 2
                          && span.RowIndex < rows.Count
                          && endExclusive <= rows.Count
                          && span.ColumnIndex < columnCount
                          && span.ColumnIndex < rows[span.RowIndex].Count;
            if (!isValid)
            {
                AddWarningOnce(
                    warnings,
                    $"'{tableName}' tablosunda geçersiz hücre birleştirme aralığı yok sayıldı (satır {span.RowIndex}, sütun {span.ColumnIndex}, span {span.RowSpan}).");
                continue;
            }

            valid.Add(span);
        }

        return valid;
    }

    private static IReadOnlyDictionary<int, TableRowGroup> BuildKeepTogetherGroupLookup(
        string tableName,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<TableRowGroup> rowGroups,
        List<string> warnings)
    {
        var groups = new Dictionary<int, TableRowGroup>();
        foreach (var group in rowGroups.Where(group => group.KeepTogetherWhenPossible))
        {
            if (!IsValidRowGroup(group, rows.Count) || groups.ContainsKey(group.StartRowIndex))
            {
                AddWarningOnce(
                    warnings,
                    $"'{tableName}' tablosunda geçersiz satır grubu yok sayıldı (başlangıç {group.StartRowIndex}, adet {group.RowCount}).");
                continue;
            }

            groups.Add(group.StartRowIndex, group);
        }

        return groups;
    }

    private static bool IsValidRowGroup(TableRowGroup group, int rowCount) =>
        group.StartRowIndex >= 0
        && group.RowCount > 0
        && group.StartRowIndex < rowCount
        && (long)group.StartRowIndex + group.RowCount <= rowCount;

    private static ProjectedTableFragment ProjectFragmentRowsAndSpans(
        IReadOnlyList<IReadOnlyList<string>> completeRows,
        IReadOnlyList<TableCellSpan> completeSpans,
        int fragmentStartRowIndex,
        int fragmentRowCount)
    {
        var fragmentRows = completeRows
            .Skip(fragmentStartRowIndex)
            .Take(fragmentRowCount)
            .Select(row => row.ToList())
            .ToList();
        var fragmentSpans = new List<TableCellSpan>();
        var fragmentEndExclusive = fragmentStartRowIndex + fragmentRows.Count;

        foreach (var span in completeSpans)
        {
            var semanticStart = span.RowIndex;
            var semanticEndExclusive = span.RowIndex + span.RowSpan;
            var intersectionStart = Math.Max(semanticStart, fragmentStartRowIndex);
            var intersectionEnd = Math.Min(semanticEndExclusive, fragmentEndExclusive);
            if (intersectionStart >= intersectionEnd)
                continue;

            var localRowIndex = intersectionStart - fragmentStartRowIndex;
            if (intersectionStart > semanticStart)
            {
                EnsureCellExists(fragmentRows[localRowIndex], span.ColumnIndex);
                fragmentRows[localRowIndex][span.ColumnIndex] = completeRows[semanticStart][span.ColumnIndex];
            }

            var localLength = intersectionEnd - intersectionStart;
            if (localLength >= 2)
            {
                fragmentSpans.Add(new TableCellSpan
                {
                    RowIndex = localRowIndex,
                    ColumnIndex = span.ColumnIndex,
                    RowSpan = localLength
                });
            }
        }

        return new ProjectedTableFragment(
            fragmentRows.Select(row => (IReadOnlyList<string>)row).ToList(),
            fragmentSpans);
    }

    private static void EnsureCellExists(List<string> row, int columnIndex)
    {
        while (row.Count <= columnIndex)
            row.Add(string.Empty);
    }

    private static void AddWarningOnce(List<string> warnings, string warning)
    {
        if (!warnings.Any(existing => string.Equals(existing, warning, StringComparison.Ordinal)))
            warnings.Add(warning);
    }

    private static PositionedPageBlock CreateBlock(
        LayoutPageFlow flow,
        Guid? elementId,
        string name,
        string? caption,
        ResolvedTextFormat? captionFormat,
        TableCaptionSequenceProfile? captionSequence,
        int? captionSequenceNumber,
        IReadOnlyList<string> columnHeaders,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<TableCellSpan> cellSpans,
        int startRowIndex,
        bool hasHeader,
        bool isHeaderRepeated,
        string? sourceError,
        int fragmentIndex,
        bool isEditable,
        ResolvedTableFormat format,
        double tableWidthMillimeters,
        double heightMillimeters) =>
        new()
        {
            ElementId = elementId,
            Region = DocumentPageRegion.Body,
            Kind = PageBlockKind.Table,
            XMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters),
            YMillimeters = flow.BodyYMillimeters,
            WidthMillimeters = tableWidthMillimeters,
            HeightMillimeters = heightMillimeters,
            FragmentIndex = fragmentIndex,
            IsContinuation = fragmentIndex > 0,
            IsEditableReportElement = isEditable,
            Payload = new TablePageBlockPayload
            {
                Name = name,
                Caption = caption,
                CaptionFormat = captionFormat,
                CaptionSequence = captionSequence,
                CaptionSequenceNumber = captionSequenceNumber,
                ColumnHeaders = columnHeaders,
                Rows = rows,
                CellSpans = cellSpans,
                Format = format,
                StartRowIndex = startRowIndex,
                HasHeader = hasHeader,
                IsHeaderRepeated = isHeaderRepeated,
                SourceError = sourceError
            }
        };

    private sealed record FragmentMetrics(
        string? FragmentCaption,
        bool HasHeader,
        bool IsHeaderRepeated,
        double OverheadHeight);

    private sealed record ProjectedTableFragment(
        IReadOnlyList<IReadOnlyList<string>> Rows,
        IReadOnlyList<TableCellSpan> CellSpans);
}
