namespace KKL.WordStudio.Application.Transfer;

using KKL.WordStudio.Shared.Spreadsheet;

/// <summary>
/// Applies the optional session-only column selection before delegating to the
/// established ExcelReportTransferService. With no explicit selection this is
/// a transparent pass-through and preserves every existing transfer path.
/// </summary>
public sealed class ColumnSelectionExcelReportTransferService : IExcelReportTransferService
{
    private readonly IColumnTransferSelectionSession selectionSession;
    private readonly IExcelReportTransferService inner;

    public ColumnSelectionExcelReportTransferService(
        IColumnTransferSelectionSession selectionSession,
        IExcelReportTransferService? inner = null)
    {
        this.selectionSession = selectionSession ?? throw new ArgumentNullException(nameof(selectionSession));
        this.inner = inner ?? new ExcelReportTransferService();
    }

    public ExcelTransferResult Transfer(
        Domain.Projects.Project project,
        Domain.Reports.Report report,
        ExcelTransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        var selected = selectionSession.GetSelection(request.WorkbookFilePath, request.WorksheetName);
        if (selected is null)
            return inner.Transfer(project, report, request);

        if (selected.Count == 0)
            return ExcelTransferResult.Failure("Rapora aktarılacak en az bir sütun seçin.");

        var selectedSet = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredMappings = request.AppliedColumnMappings?
            .Where(mapping => selectedSet.Contains(mapping.SourceColumn))
            .ToList();

        var transferColumns = request.WorkingDataColumns is { Count: > 0 }
            ? FilterWorkingDataColumns(request.WorkingDataColumns, selectedSet)
            : BuildSourceRangeProjection(request, selectedSet, filteredMappings);

        if (transferColumns.Count == 0)
            return ExcelTransferResult.Failure("Seçilen sütunlar etkin Excel aralığında bulunamadı.");

        var filteredRequest = new ExcelTransferRequest
        {
            WorkbookFilePath = request.WorkbookFilePath,
            WorkbookFileName = request.WorkbookFileName,
            WorksheetName = request.WorksheetName,
            Range = request.Range,
            HeaderTexts = request.HeaderTexts,
            AppliedColumnMappings = filteredMappings,
            WorkingDataColumns = transferColumns,
            TargetElementId = request.TargetElementId,
            ExistingTableMode = request.ExistingTableMode,
            SourceFieldMappings = request.SourceFieldMappings,
            PreferredDataSourceName = request.PreferredDataSourceName
        };

        return inner.Transfer(project, report, filteredRequest);
    }

    private static IReadOnlyList<TransferWorkingColumn> FilterWorkingDataColumns(
        IReadOnlyList<TransferWorkingColumn> workingColumns,
        HashSet<string> selected)
    {
        return workingColumns
            .Where(column =>
                selected.Contains(column.SourceField)
                || (!string.IsNullOrWhiteSpace(column.OriginalSourceColumn)
                    && selected.Contains(column.OriginalSourceColumn)))
            .Select(column => new TransferWorkingColumn
            {
                SourceField = column.SourceField,
                Header = column.Header,
                OriginalSourceColumn = column.OriginalSourceColumn
            })
            .ToList();
    }

    private static IReadOnlyList<TransferWorkingColumn> BuildSourceRangeProjection(
        ExcelTransferRequest request,
        HashSet<string> selected,
        IReadOnlyList<TransferColumnMapping>? mappings)
    {
        if (request.Range.StartColumn is not { } start
            || request.Range.EndColumn is not { } end
            || end < start)
        {
            return Array.Empty<TransferWorkingColumn>();
        }

        var mappingLookup = mappings?
            .GroupBy(mapping => mapping.SourceColumn, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, TransferColumnMapping>(StringComparer.OrdinalIgnoreCase);

        var columns = new List<TransferWorkingColumn>();
        for (var sourceColumn = start; sourceColumn <= end; sourceColumn++)
        {
            var letter = ColumnLetterConverter.ToLetters(sourceColumn);
            if (!selected.Contains(letter))
                continue;

            var relativeIndex = sourceColumn - start;
            var header = mappingLookup.TryGetValue(letter, out var mapping)
                ? mapping.FieldName
                : relativeIndex < request.HeaderTexts.Count
                    ? request.HeaderTexts[relativeIndex]
                    : string.Empty;

            columns.Add(new TransferWorkingColumn
            {
                SourceField = letter,
                OriginalSourceColumn = letter,
                Header = string.IsNullOrWhiteSpace(header) ? $"Sütun {letter}" : header.Trim()
            });
        }

        return columns;
    }
}
