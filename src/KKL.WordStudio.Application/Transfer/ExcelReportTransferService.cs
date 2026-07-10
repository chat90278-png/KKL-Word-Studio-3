namespace KKL.WordStudio.Application.Transfer;

using System.Text.RegularExpressions;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Domain.Visitors;
using KKL.WordStudio.Shared.Spreadsheet;

/// <summary>
/// Coordinates the direct Excel-to-report flow. Sprint 10 keeps legacy
/// Binding behavior intact and adds ordered TableElement.Sources only when a
/// configured table explicitly receives an additional source.
/// </summary>
public interface IExcelReportTransferService
{
    ExcelTransferResult Transfer(Project project, Report report, ExcelTransferRequest request);
}

public sealed class ExcelReportTransferService : IExcelReportTransferService
{
    private readonly ISerialQuantityGroupingDetector groupingDetector;

    private static readonly Regex PlaceholderHeaderPattern = new(@"^(Column|Sütun)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ExcelReportTransferService(ISerialQuantityGroupingDetector? groupingDetector = null)
    {
        this.groupingDetector = groupingDetector ?? new SerialQuantityGroupingDetector();
    }

    public ExcelTransferResult Transfer(Project project, Report report, ExcelTransferRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorksheetName))
            return ExcelTransferResult.Failure("Önce bir Excel sayfası seçin.");
        if (request.Range.DataEndRow is null || request.Range.DataEndRow < request.Range.DataStartRow)
            return ExcelTransferResult.Failure("Önce veri aralığını yapılandırın.");

        var columnCount = ResolveColumnCount(request);
        if (columnCount <= 0)
            return ExcelTransferResult.Failure("Seçili aralıkta aktarılacak sütun bulunamadı.");

        var target = request.TargetElementId is { } id ? ReportElementFlattener.FindById(report, id) : null;

        // Capture the legacy source BEFORE the active worksheet's mutable
        // SelectedRange/mappings are updated by this transfer. This is what
        // turns the old binding into a pinned source #1 when source #2 is added.
        TableSourceBinding? capturedLegacySource = null;
        string? legacyCaptureError = null;
        if (target is TableElement legacyTable
            && request.ExistingTableMode == ExistingTableTransferMode.AddAsSource
            && legacyTable.Sources.Count == 0
            && legacyTable.Binding is not null)
        {
            capturedLegacySource = TryCaptureLegacySource(project, legacyTable, out legacyCaptureError);
            if (capturedLegacySource is null)
                return ExcelTransferResult.Failure(legacyCaptureError ?? "Mevcut tablo kaynağı güvenli biçimde sabitlenemedi.");
        }

        var dataSource = ResolveOrCreateDataSource(project, request);
        var worksheet = dataSource.Workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, request.WorksheetName, StringComparison.OrdinalIgnoreCase));
        if (worksheet is null)
        {
            worksheet = new Worksheet { Name = request.WorksheetName };
            dataSource.Workbook.Worksheets.Add(worksheet);
        }

        worksheet.SelectedRange = CloneRange(request.Range);

        if (request.AppliedColumnMappings is { Count: > 0 } && worksheet.ColumnMappings.Count == 0)
        {
            foreach (var mapping in request.AppliedColumnMappings)
            {
                worksheet.ColumnMappings.Add(new ColumnMapping
                {
                    SourceColumn = mapping.SourceColumn,
                    TargetField = new DataField { Name = mapping.FieldName, DataType = mapping.DataType }
                });
            }
        }

        var sourceColumns = BuildSourceColumns(dataSource, worksheet, request, columnCount);

        return target switch
        {
            TableElement table => TransferIntoExistingTable(
                report, table, dataSource, worksheet, request, sourceColumns, capturedLegacySource),
            TextElement heading when IsBodyHeading(report, heading) =>
                CreateBoundTable(report, dataSource, request, sourceColumns, insertAfter: heading),
            _ => CreateBoundTable(report, dataSource, request, sourceColumns, insertAfter: null)
        };
    }

    private ExcelTransferResult TransferIntoExistingTable(
        Report report,
        TableElement table,
        ExcelDataSource dataSource,
        Worksheet worksheet,
        ExcelTransferRequest request,
        IReadOnlyList<SourceColumnDescriptor> sourceColumns,
        TableSourceBinding? capturedLegacySource)
    {
        var isAlreadyConfigured = table.Binding is not null || table.Sources.Count > 0 || HasCustomizedColumns(table);

        if (isAlreadyConfigured && request.ExistingTableMode is null)
        {
            return new ExcelTransferResult
            {
                Outcome = TransferOutcome.RequiresExistingTableDecision,
                Table = table,
                WorksheetName = request.WorksheetName,
                RangeReference = request.Range.RangeReference
            };
        }

        var mode = isAlreadyConfigured
            ? request.ExistingTableMode!.Value
            : ExistingTableTransferMode.ReplaceColumnsFromSource;

        if (mode == ExistingTableTransferMode.AddAsSource)
            return AddSource(report, table, dataSource, worksheet, request, sourceColumns, capturedLegacySource);

        // Existing rebind/replace choices retain their Sprint 7 meaning: they
        // return the table to one authoritative legacy Binding.
        table.Sources.Clear();

        if (mode == ExistingTableTransferMode.ReplaceColumnsFromSource)
        {
            table.Columns.Clear();
            foreach (var sourceColumn in sourceColumns)
                table.Columns.Add(CreateTableColumn(sourceColumn));
            table.SerialQuantityGrouping = groupingDetector.Detect(table.Columns);
        }
        else
        {
            for (var i = 0; i < table.Columns.Count && i < sourceColumns.Count; i++)
                table.Columns[i].SourceField = sourceColumns[i].LogicalField;

            if (!HasValidGrouping(table))
                table.SerialQuantityGrouping = groupingDetector.Detect(table.Columns);
        }

        ApplyBinding(table, dataSource, request);
        EnsureHeaderRow(table);
        return Success(table, request, createdNew: false, addedAsSource: false);
    }

    private ExcelTransferResult AddSource(
        Report report,
        TableElement table,
        ExcelDataSource dataSource,
        Worksheet worksheet,
        ExcelTransferRequest request,
        IReadOnlyList<SourceColumnDescriptor> sourceColumns,
        TableSourceBinding? capturedLegacySource)
    {
        if (table.Columns.Count == 0)
            return ExcelTransferResult.Failure("Kaynak eklemek için hedef tabloda en az bir sütun bulunmalıdır.");

        var mappingResult = ResolveSourceFieldMappings(table, sourceColumns, request.SourceFieldMappings);
        if (mappingResult.Requirements.Count > 0)
        {
            return new ExcelTransferResult
            {
                Outcome = TransferOutcome.RequiresSourceFieldMapping,
                Table = table,
                WorksheetName = request.WorksheetName,
                RangeReference = request.Range.RangeReference,
                SourceFieldMappingRequirements = mappingResult.Requirements
            };
        }

        if (table.Sources.Count == 0 && table.Binding is not null)
        {
            if (capturedLegacySource is null)
                return ExcelTransferResult.Failure("Mevcut tablo kaynağı güvenli biçimde sabitlenemedi; yeni kaynak eklenmedi.");
            table.Sources.Add(capturedLegacySource);
        }

        var source = new TableSourceBinding
        {
            DataSourceName = dataSource.Name,
            WorksheetName = worksheet.Name,
            Range = CloneRange(request.Range)
        };
        foreach (var mapping in mappingResult.Mappings)
            source.FieldMappings.Add(mapping);
        table.Sources.Add(source);

        if (table.SerialQuantityGrouping is null)
            table.SerialQuantityGrouping = groupingDetector.Detect(table.Columns);

        EnsureHeaderRow(table);
        return Success(table, request, createdNew: false, addedAsSource: true);
    }

    private ExcelTransferResult CreateBoundTable(
        Report report,
        ExcelDataSource dataSource,
        ExcelTransferRequest request,
        IReadOnlyList<SourceColumnDescriptor> sourceColumns,
        TextElement? insertAfter)
    {
        var table = new TableElement { Name = NextTableName(report) };
        foreach (var sourceColumn in sourceColumns)
            table.Columns.Add(CreateTableColumn(sourceColumn));
        table.SerialQuantityGrouping = groupingDetector.Detect(table.Columns);
        table.Rows.Add(new TableRow { Kind = TableRowKind.Header });
        table.Rows.Add(new TableRow { Kind = TableRowKind.Detail });

        ApplyBinding(table, dataSource, request);

        if (insertAfter is not null && TryInsertAfter(report, insertAfter, table))
            return Success(table, request, createdNew: true, addedAsSource: false);

        var bodySection = report.Pages.SelectMany(p => p.Sections).FirstOrDefault(s => s.Kind == SectionKind.Body);
        if (bodySection is null)
        {
            var page = report.Pages.FirstOrDefault();
            if (page is null)
                return ExcelTransferResult.Failure("Etkin raporda sayfa bulunamadı.");
            bodySection = new Section { Name = SectionKind.Body.ToString(), Kind = SectionKind.Body, AutoHeight = true };
            page.Sections.Add(bodySection);
        }
        bodySection.Root.Children.Add(table);

        return Success(table, request, createdNew: true, addedAsSource: false);
    }

    private static MappingResolution ResolveSourceFieldMappings(
        TableElement table,
        IReadOnlyList<SourceColumnDescriptor> sourceColumns,
        IReadOnlyList<TransferSourceFieldMapping>? explicitMappings)
    {
        var explicitLookup = explicitMappings?
            .GroupBy(mapping => mapping.TableColumnId)
            .ToDictionary(group => group.Key, group => group.Last().SourceField)
            ?? new Dictionary<Guid, string>();
        var usedAutomaticFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new Dictionary<Guid, string>();

        foreach (var tableColumn in table.Columns)
        {
            if (explicitLookup.TryGetValue(tableColumn.Id, out var explicitField))
            {
                var explicitDescriptor = sourceColumns.FirstOrDefault(candidate =>
                    string.Equals(candidate.ProviderField, explicitField, StringComparison.OrdinalIgnoreCase));
                if (explicitDescriptor is not null)
                    resolved[tableColumn.Id] = explicitDescriptor.ProviderField;
                continue;
            }

            var automatic = FindAutomaticMatch(tableColumn, sourceColumns, usedAutomaticFields);
            if (automatic is null) continue;
            resolved[tableColumn.Id] = automatic.ProviderField;
            usedAutomaticFields.Add(automatic.ProviderField);
        }

        if (resolved.Count == table.Columns.Count)
        {
            return new MappingResolution(
                table.Columns.Select(column => new TableSourceFieldMapping
                {
                    TableColumnId = column.Id,
                    SourceField = resolved[column.Id]
                }).ToList(),
                Array.Empty<SourceFieldMappingRequirement>());
        }

        var options = sourceColumns.Select(column => new SourceFieldOption
        {
            SourceField = column.ProviderField,
            DisplayText = BuildSourceFieldDisplay(column)
        }).ToList();

        var requirements = table.Columns.Select(column => new SourceFieldMappingRequirement
        {
            TableColumnId = column.Id,
            TableColumnHeader = column.Header,
            SuggestedSourceField = resolved.TryGetValue(column.Id, out var field) ? field : null,
            AvailableSourceFields = options
        }).ToList();

        return new MappingResolution(Array.Empty<TableSourceFieldMapping>(), requirements);
    }

    private static SourceColumnDescriptor? FindAutomaticMatch(
        TableColumn tableColumn,
        IReadOnlyList<SourceColumnDescriptor> sourceColumns,
        HashSet<string> usedFields)
    {
        SourceColumnDescriptor? Match(Func<SourceColumnDescriptor, bool> predicate) => sourceColumns.FirstOrDefault(candidate =>
            !usedFields.Contains(candidate.ProviderField) && predicate(candidate));

        // 1. Logical mapped identity. TableColumn.SourceField is the stable
        // report-column identity; mapped logical names therefore win first.
        if (!string.IsNullOrWhiteSpace(tableColumn.SourceField))
        {
            var logical = Match(candidate => candidate.IsMapped &&
                string.Equals(candidate.LogicalField, tableColumn.SourceField, StringComparison.OrdinalIgnoreCase));
            if (logical is not null) return logical;
        }

        // 2. Exact case-insensitive field/header match. No positional fallback
        // is allowed for an additional source.
        return Match(candidate =>
            (!string.IsNullOrWhiteSpace(tableColumn.SourceField)
             && (string.Equals(candidate.LogicalField, tableColumn.SourceField, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(candidate.ProviderField, tableColumn.SourceField, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(candidate.Header, tableColumn.SourceField, StringComparison.OrdinalIgnoreCase)))
            || string.Equals(candidate.Header, tableColumn.Header, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.LogicalField, tableColumn.Header, StringComparison.OrdinalIgnoreCase));
    }

    private static TableSourceBinding? TryCaptureLegacySource(Project project, TableElement table, out string? error)
    {
        error = null;
        var binding = table.Binding;
        if (binding is null) return null;

        var dataSource = project.DataSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, binding.DataSourceName, StringComparison.OrdinalIgnoreCase));
        if (dataSource is not ExcelDataSource excelDataSource)
        {
            error = $"'{binding.DataSourceName}' veri kaynağı projede bulunamadı; mevcut tablo kaynağı korunamadı.";
            return null;
        }

        var worksheetName = binding.WorksheetName ?? excelDataSource.ActiveWorksheetName;
        if (string.IsNullOrWhiteSpace(worksheetName))
        {
            error = "Mevcut tablo bağının Excel sayfası belirlenemedi.";
            return null;
        }

        var worksheet = excelDataSource.Workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, worksheetName, StringComparison.OrdinalIgnoreCase));
        if (worksheet?.SelectedRange is null)
        {
            error = $"'{worksheetName}' sayfasının mevcut veri aralığı bulunamadı; yeni kaynak eklenmedi.";
            return null;
        }

        var sourceColumns = BuildConfiguredSourceColumns(excelDataSource, worksheet, worksheet.SelectedRange);
        if (sourceColumns.Count == 0)
        {
            error = "Mevcut tablo kaynağının sütun yapısı belirlenemedi; yeni kaynak eklenmedi.";
            return null;
        }

        var source = new TableSourceBinding
        {
            DataSourceName = excelDataSource.Name,
            WorksheetName = worksheet.Name,
            Range = CloneRange(worksheet.SelectedRange)
        };

        for (var index = 0; index < table.Columns.Count; index++)
        {
            var tableColumn = table.Columns[index];
            var sourceColumn = sourceColumns.FirstOrDefault(candidate =>
                                   !string.IsNullOrWhiteSpace(tableColumn.SourceField)
                                   && string.Equals(candidate.LogicalField, tableColumn.SourceField, StringComparison.OrdinalIgnoreCase))
                               ?? sourceColumns.FirstOrDefault(candidate =>
                                   !string.IsNullOrWhiteSpace(tableColumn.SourceField)
                                   && string.Equals(candidate.ProviderField, tableColumn.SourceField, StringComparison.OrdinalIgnoreCase))
                               ?? sourceColumns.FirstOrDefault(candidate =>
                                   string.Equals(candidate.Header, tableColumn.Header, StringComparison.OrdinalIgnoreCase))
                               // Backward-compatibility only: legacy binding used field order
                               // when TableColumn.SourceField did not yet exist.
                               ?? (index < sourceColumns.Count ? sourceColumns[index] : null);
            if (sourceColumn is null)
            {
                error = $"Mevcut tablonun '{tableColumn.Header}' sütunu eski kaynağa güvenli biçimde eşlenemedi.";
                return null;
            }

            source.FieldMappings.Add(new TableSourceFieldMapping
            {
                TableColumnId = tableColumn.Id,
                SourceField = sourceColumn.ProviderField
            });
        }

        return source;
    }

    private static List<SourceColumnDescriptor> BuildConfiguredSourceColumns(
        ExcelDataSource dataSource,
        Worksheet worksheet,
        DataRange range)
    {
        var mappings = worksheet.ColumnMappings.Count > 0 ? worksheet.ColumnMappings : dataSource.ColumnMappings;
        var columns = new List<SourceColumnDescriptor>();

        if (worksheet.WorkingData is { Columns.Count: > 0 } workingData)
        {
            foreach (var workingColumn in workingData.Columns)
            {
                var mapping = FindMapping(mappings, workingColumn.OriginalSourceColumn ?? workingColumn.SourceField, workingColumn.SourceField);
                columns.Add(new SourceColumnDescriptor(
                    workingColumn.SourceField,
                    mapping?.TargetField.Name ?? workingColumn.SourceField,
                    mapping?.TargetField.Name ?? workingColumn.Header,
                    mapping is not null));
            }
            return columns;
        }

        if (range.StartColumn is not { } start || range.EndColumn is not { } end || end < start)
            return columns;

        for (var columnIndex = start; columnIndex <= end; columnIndex++)
        {
            var letter = ColumnLetterConverter.ToLetters(columnIndex);
            var mapping = FindMapping(mappings, letter, letter);
            columns.Add(new SourceColumnDescriptor(
                letter,
                mapping?.TargetField.Name ?? letter,
                mapping?.TargetField.Name ?? letter,
                mapping is not null));
        }
        return columns;
    }

    private static List<SourceColumnDescriptor> BuildSourceColumns(
        ExcelDataSource dataSource,
        Worksheet worksheet,
        ExcelTransferRequest request,
        int columnCount)
    {
        var mappings = worksheet.ColumnMappings.Count > 0 ? worksheet.ColumnMappings : dataSource.ColumnMappings;
        var columns = new List<SourceColumnDescriptor>(columnCount);

        if (request.WorkingDataColumns is { Count: > 0 } workingColumns)
        {
            foreach (var workingColumn in workingColumns)
            {
                var mapping = FindMapping(mappings, workingColumn.OriginalSourceColumn ?? workingColumn.SourceField, workingColumn.SourceField);
                columns.Add(new SourceColumnDescriptor(
                    workingColumn.SourceField,
                    mapping?.TargetField.Name ?? workingColumn.SourceField,
                    mapping?.TargetField.Name ?? (string.IsNullOrWhiteSpace(workingColumn.Header) ? "Yeni Sütun" : workingColumn.Header),
                    mapping is not null));
            }
            return columns;
        }

        var startColumn = request.Range.StartColumn ?? 1;
        for (var i = 0; i < columnCount; i++)
        {
            var letter = ColumnLetterConverter.ToLetters(startColumn + i);
            var mapping = FindMapping(mappings, letter, letter);
            var headerText = i < request.HeaderTexts.Count ? request.HeaderTexts[i] : null;
            columns.Add(new SourceColumnDescriptor(
                letter,
                mapping?.TargetField.Name ?? letter,
                mapping?.TargetField.Name ?? (string.IsNullOrWhiteSpace(headerText) ? $"Sütun {letter}" : headerText.Trim()),
                mapping is not null));
        }
        return columns;
    }

    private static ColumnMapping? FindMapping(IReadOnlyList<ColumnMapping> mappings, string primaryKey, string fallbackKey) =>
        mappings.FirstOrDefault(candidate =>
            string.Equals(candidate.SourceColumn, primaryKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.SourceColumn, fallbackKey, StringComparison.OrdinalIgnoreCase));

    private static TableColumn CreateTableColumn(SourceColumnDescriptor sourceColumn) => new()
    {
        Header = sourceColumn.Header,
        SourceField = sourceColumn.LogicalField
    };

    private static string BuildSourceFieldDisplay(SourceColumnDescriptor column)
    {
        if (!string.Equals(column.LogicalField, column.ProviderField, StringComparison.OrdinalIgnoreCase))
            return $"{column.LogicalField} · {column.ProviderField}";
        if (!string.Equals(column.Header, column.ProviderField, StringComparison.OrdinalIgnoreCase))
            return $"{column.Header} · {column.ProviderField}";
        return column.ProviderField;
    }

    private static void ApplyBinding(TableElement table, ExcelDataSource dataSource, ExcelTransferRequest request) =>
        table.Binding = new Binding { DataSourceName = dataSource.Name, WorksheetName = request.WorksheetName };

    private static void EnsureHeaderRow(TableElement table)
    {
        if (!table.Rows.Any(r => r.Kind == TableRowKind.Header))
            table.Rows.Insert(0, new TableRow { Kind = TableRowKind.Header });
    }

    private static ExcelTransferResult Success(TableElement table, ExcelTransferRequest request, bool createdNew, bool addedAsSource) => new()
    {
        Outcome = TransferOutcome.Success,
        Table = table,
        CreatedNewTable = createdNew,
        WorksheetName = request.WorksheetName,
        RangeReference = request.Range.RangeReference,
        AddedAsSource = addedAsSource
    };

    private static int ResolveColumnCount(ExcelTransferRequest request)
    {
        if (request.WorkingDataColumns is { Count: > 0 }) return request.WorkingDataColumns.Count;
        if (request.Range.StartColumn is { } start && request.Range.EndColumn is { } end && end >= start) return end - start + 1;
        if (request.HeaderTexts.Count > 0) return request.HeaderTexts.Count;
        return request.AppliedColumnMappings?.Count ?? 0;
    }

    private static bool HasValidGrouping(TableElement table)
    {
        var grouping = table.SerialQuantityGrouping;
        if (grouping is null)
            return false;

        var columnIds = table.Columns.Select(column => column.Id).ToHashSet();
        return grouping.MatchKeyColumnId != grouping.SerialNumberColumnId
            && grouping.MatchKeyColumnId != grouping.QuantityColumnId
            && grouping.SerialNumberColumnId != grouping.QuantityColumnId
            && columnIds.Contains(grouping.MatchKeyColumnId)
            && columnIds.Contains(grouping.SerialNumberColumnId)
            && columnIds.Contains(grouping.QuantityColumnId);
    }

    private static bool HasCustomizedColumns(TableElement table) =>
        table.Columns.Count > 0 && table.Columns.Any(c =>
            !string.IsNullOrWhiteSpace(c.Header) && !PlaceholderHeaderPattern.IsMatch(c.Header.Trim()));

    private static bool IsBodyHeading(Report report, TextElement text)
    {
        if (!HeadingStylePresets.IsHeading(text.Style) && !HeadingStylePresets.IsAltHeading(text.Style)) return false;
        return report.Pages
            .SelectMany(p => p.Sections)
            .Where(s => s.Kind is not (SectionKind.PageHeader or SectionKind.PageFooter))
            .Any(s => FindContainerOf(s.Root, text) is not null);
    }

    private static bool TryInsertAfter(Report report, TextElement heading, TableElement table)
    {
        foreach (var section in report.Pages.SelectMany(p => p.Sections))
        {
            var container = FindContainerOf(section.Root, heading);
            if (container is null) continue;
            var index = container.Children.IndexOf(heading);
            container.Children.Insert(index + 1, table);
            return true;
        }
        return false;
    }

    private static Container? FindContainerOf(Container container, ReportElement element)
    {
        if (container.Children.Contains(element)) return container;
        foreach (var nested in container.Children.OfType<Container>())
        {
            var found = FindContainerOf(nested, element);
            if (found is not null) return found;
        }
        return null;
    }

    private static ExcelDataSource ResolveOrCreateDataSource(Project project, ExcelTransferRequest request)
    {
        var existing = project.DataSources
            .OfType<ExcelDataSource>()
            .FirstOrDefault(ds => string.Equals(ds.Workbook.SourcePath, request.WorkbookFilePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.ActiveWorksheetName = request.WorksheetName;
            return existing;
        }

        var baseName = !string.IsNullOrWhiteSpace(request.PreferredDataSourceName)
            ? request.PreferredDataSourceName.Trim()
            : Path.GetFileNameWithoutExtension(request.WorkbookFileName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Veri Kaynağı";

        var name = baseName;
        var suffix = 2;
        while (project.DataSources.Any(ds => string.Equals(ds.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{baseName} {suffix++}";

        var dataSource = new ExcelDataSource
        {
            Name = name,
            Workbook = new Workbook { FileName = request.WorkbookFileName, SourcePath = request.WorkbookFilePath },
            ActiveWorksheetName = request.WorksheetName
        };
        project.DataSources.Add(dataSource);
        return dataSource;
    }

    private static string NextTableName(Report report)
    {
        var tableCount = ReportElementFlattener.Flatten(report).OfType<TableElement>().Count();
        return $"Tablo {tableCount + 1}";
    }

    private static DataRange CloneRange(DataRange range) => new()
    {
        DataStartRow = range.DataStartRow,
        DataEndRow = range.DataEndRow,
        HeaderRowIndex = range.HeaderRowIndex,
        StartColumn = range.StartColumn,
        EndColumn = range.EndColumn,
        WasAutoDetected = range.WasAutoDetected
    };

    private sealed record SourceColumnDescriptor(
        string ProviderField,
        string LogicalField,
        string Header,
        bool IsMapped);

    private sealed record MappingResolution(
        IReadOnlyList<TableSourceFieldMapping> Mappings,
        IReadOnlyList<SourceFieldMappingRequirement> Requirements);
}
