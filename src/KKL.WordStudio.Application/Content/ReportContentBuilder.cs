namespace KKL.WordStudio.Application.Content;

using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// Default IReportContentBuilder. Splits a Report's sections into three
/// regions by Section.Kind: PageHeader/PageFooter sections become the
/// repeating Header/Footer regions; everything else (Body, ReportHeader,
/// ReportFooter, GroupHeader, GroupFooter) flows into the Body region in
/// page/section order — a deliberate simplification for this "foundation"
/// sprint (see ADR 0007) rather than modeling per-group repetition, which
/// is real pagination/execution work for the future Engine (ADR 0002).
///
/// PageLayout is taken from the Report's first Page — every Section/
/// Element after that shares one page template, consistent with how the
/// Report Designer only creates a single Page today.
/// </summary>
public sealed class ReportContentBuilder : IReportContentBuilder
{
    private readonly IDataProviderRegistry _dataProviderRegistry;
    private readonly ITableContentRowComposer _tableContentRowComposer;
    private readonly IReferenceDocumentFormatProvider _referenceDocumentFormatProvider;
    private readonly IReportContentFormatResolver _reportContentFormatResolver;

    public ReportContentBuilder(IDataProviderRegistry dataProviderRegistry)
        : this(
            dataProviderRegistry,
            new PassthroughTableContentRowComposer(),
            new NoReferenceDocumentFormatProvider(),
            new DefaultReportContentFormatResolver())
    {
    }

    public ReportContentBuilder(
        IDataProviderRegistry dataProviderRegistry,
        ITableContentRowComposer tableContentRowComposer)
        : this(
            dataProviderRegistry,
            tableContentRowComposer,
            new NoReferenceDocumentFormatProvider(),
            new DefaultReportContentFormatResolver())
    {
    }

    public ReportContentBuilder(
        IDataProviderRegistry dataProviderRegistry,
        ITableContentRowComposer tableContentRowComposer,
        IReferenceDocumentFormatProvider referenceDocumentFormatProvider,
        IReportContentFormatResolver reportContentFormatResolver)
    {
        _dataProviderRegistry = dataProviderRegistry;
        _tableContentRowComposer = tableContentRowComposer;
        _referenceDocumentFormatProvider = referenceDocumentFormatProvider;
        _reportContentFormatResolver = reportContentFormatResolver;
    }

    public async Task<ReportContentDocument> BuildAsync(Project project, Report report, CancellationToken cancellationToken = default)
    {
        var formatResult = await _referenceDocumentFormatProvider.ReadAsync(project, cancellationToken);
        var formatProfile = formatResult.Profile;
        var formatWarnings = new List<string>();
        if (formatProfile is not null)
            formatWarnings.AddRange(formatProfile.Warnings);
        if (formatResult.IsMissing && !string.IsNullOrWhiteSpace(formatResult.StatusMessage))
            formatWarnings.Add(formatResult.StatusMessage);
        formatWarnings = formatWarnings.Distinct(StringComparer.Ordinal).ToList();

        var headerNodes = new List<ReportContentNode>();
        var bodyNodes = new List<ReportContentNode>();
        var footerNodes = new List<ReportContentNode>();

        foreach (var page in report.Pages)
        {
            foreach (var section in page.Sections)
            {
                var target = section.Kind switch
                {
                    SectionKind.PageHeader => headerNodes,
                    SectionKind.PageFooter => footerNodes,
                    _ => bodyNodes
                };
                await BuildFromContainerAsync(project, section.Root, target, formatProfile, cancellationToken);
            }
        }

        var tableOfContents = report.IncludeTableOfContents
            ? BuildTableOfContents(bodyNodes)
            : Array.Empty<TocEntry>();

        var firstPage = report.Pages.FirstOrDefault();
        var authoredPageLayout = new PageLayout
        {
            WidthMillimeters = firstPage?.WidthMillimeters ?? 210,
            HeightMillimeters = firstPage?.HeightMillimeters ?? 297,
            MarginTopMillimeters = firstPage?.MarginsMillimeters.Top ?? 20,
            MarginBottomMillimeters = firstPage?.MarginsMillimeters.Bottom ?? 20,
            MarginLeftMillimeters = firstPage?.MarginsMillimeters.Left ?? 20,
            MarginRightMillimeters = firstPage?.MarginsMillimeters.Right ?? 20,
            ShowPageNumbers = firstPage?.ShowPageNumbers ?? true
        };
        var pageLayout = _reportContentFormatResolver.ResolvePageLayout(formatProfile, authoredPageLayout);

        return new ReportContentDocument
        {
            HeaderNodes = headerNodes,
            BodyNodes = bodyNodes,
            FooterNodes = footerNodes,
            TableOfContents = tableOfContents,
            PageLayout = pageLayout,
            FormatWarnings = formatWarnings
        };
    }

    private static IReadOnlyList<TocEntry> BuildTableOfContents(IReadOnlyList<ReportContentNode> bodyNodes) =>
        bodyNodes
            .OfType<TextContentNode>()
            .Where(t => t.Kind is ReportContentKind.Heading or ReportContentKind.AltHeading)
            .Select(t => new TocEntry
            {
                ElementId = t.ElementId,
                Text = t.Text,
                Level = t.Kind == ReportContentKind.Heading ? 1 : 2
            })
            .ToList();

    private async Task BuildFromContainerAsync(
        Project project,
        Container container,
        List<ReportContentNode> nodes,
        DocumentFormatProfile? formatProfile,
        CancellationToken cancellationToken)
    {
        foreach (var child in container.Children)
        {
            switch (child)
            {
                case Container nested:
                    await BuildFromContainerAsync(project, nested, nodes, formatProfile, cancellationToken);
                    break;

                case TextElement text:
                    nodes.Add(BuildTextNode(text, formatProfile));
                    break;

                case ImageElement image:
                    nodes.Add(new ImageContentNode { ElementId = image.Id, Kind = ReportContentKind.Image, Name = image.Name });
                    break;

                case TableElement table:
                    nodes.Add(await BuildTableNodeAsync(project, table, formatProfile, cancellationToken));
                    break;
            }
        }
    }

    private TextContentNode BuildTextNode(TextElement text, DocumentFormatProfile? formatProfile)
    {
        var kind = HeadingStylePresets.IsHeading(text.Style) ? ReportContentKind.Heading
            : HeadingStylePresets.IsAltHeading(text.Style) ? ReportContentKind.AltHeading
            : ReportContentKind.Paragraph;

        return new TextContentNode
        {
            ElementId = text.Id,
            Kind = kind,
            Text = text.Content.Text,
            Bold = text.Style.Bold,
            FontSize = text.Style.FontSize,
            Format = _reportContentFormatResolver.ResolveText(formatProfile, kind, text.Style)
        };
    }

    private async Task<TableContentNode> BuildTableNodeAsync(
        Project project,
        TableElement table,
        DocumentFormatProfile? formatProfile,
        CancellationToken cancellationToken)
    {
        if (table.Sources.Count > 0)
            return await BuildMultiSourceTableNodeAsync(project, table, formatProfile, cancellationToken);

        if (table.Binding is not null)
        {
            var dataSource = project.DataSources.FirstOrDefault(ds => ds.Name == table.Binding.DataSourceName);
            if (dataSource is not null)
            {
                var provider = _dataProviderRegistry.Resolve(dataSource.ProviderKey);
                var rowsResult = await provider.GetRowsAsync(dataSource, cancellationToken, table.Binding.WorksheetName);

                if (rowsResult.IsSuccess)
                {
                    List<string> displayHeaders;
                    List<string> fieldKeys;
                    if (table.Columns.Count > 0 && table.Columns.Any(c => !string.IsNullOrWhiteSpace(c.SourceField)))
                    {
                        displayHeaders = table.Columns.Select(c => c.Header).ToList();
                        fieldKeys = table.Columns
                            .Select(c => string.IsNullOrWhiteSpace(c.SourceField) ? c.Header : c.SourceField!)
                            .ToList();
                    }
                    else
                    {
                        var fields = ResolveFieldsForBinding(dataSource, table.Binding.WorksheetName);
                        displayHeaders = fields.Select(f => f.Name).ToList();
                        fieldKeys = displayHeaders;
                    }

                    IEnumerable<IReadOnlyDictionary<string, object?>> rows = rowsResult.Value;
                    if (table.Binding.SortFields.Count > 0)
                        rows = ApplySort(rows, table.Binding.SortFields);

                    var renderedRows = rows
                        .Select(row => (IReadOnlyList<string>)fieldKeys
                            .Select(field => row.TryGetValue(field, out var value) ? value?.ToString() ?? string.Empty : string.Empty)
                            .ToList())
                        .ToList();

                    return BuildComposedTableNode(
                        table,
                        formatProfile,
                        displayHeaders,
                        renderedRows,
                        dataSource.Name,
                        sourceCount: 1,
                        filterWasIgnored: table.Binding.Filter is not null);
                }
            }
        }

        var headers = table.Columns.Select(c => c.Header).ToList();
        var staticRows = table.Rows
            .Where(r => r.Kind == TableRowKind.Detail)
            .Select(r => (IReadOnlyList<string>)r.Cells.Select(ExtractCellText).ToList())
            .ToList();

        return BuildComposedTableNode(
            table,
            formatProfile,
            headers,
            staticRows,
            dataSourceName: null,
            sourceCount: 0,
            filterWasIgnored: false);
    }

    private async Task<TableContentNode> BuildMultiSourceTableNodeAsync(
        Project project,
        TableElement table,
        DocumentFormatProfile? formatProfile,
        CancellationToken cancellationToken)
    {
        var headers = table.Columns.Select(column => column.Header).ToList();
        var renderedRows = new List<IReadOnlyList<string>>();

        foreach (var source in table.Sources)
        {
            var dataSource = project.DataSources.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, source.DataSourceName, StringComparison.OrdinalIgnoreCase));
            if (dataSource is null)
                return BuildMultiSourceErrorNode(table, formatProfile, headers, renderedRows,
                    $"'{source.DataSourceName}' veri kaynağı projede bulunamadı.");

            var mappings = table.Columns.Select(column => new
            {
                Column = column,
                Mapping = source.FieldMappings.FirstOrDefault(candidate => candidate.TableColumnId == column.Id)
            }).ToList();
            var missingMapping = mappings.FirstOrDefault(item => item.Mapping is null);
            if (missingMapping is not null)
                return BuildMultiSourceErrorNode(table, formatProfile, headers, renderedRows,
                    $"'{source.DataSourceName} / {source.WorksheetName}' kaynağında '{missingMapping.Column.Header}' sütunu için alan eşlemesi eksik.");

            var provider = _dataProviderRegistry.Resolve(dataSource.ProviderKey);
            var rowsResult = await provider.GetRowsAsync(
                dataSource,
                cancellationToken,
                source.WorksheetName,
                source.Range);
            if (rowsResult.IsFailure)
                return BuildMultiSourceErrorNode(table, formatProfile, headers, renderedRows,
                    $"'{source.DataSourceName} / {source.WorksheetName}' kaynağı kullanılamıyor: {rowsResult.Error}");

            IEnumerable<IReadOnlyDictionary<string, object?>> sourceRows = rowsResult.Value;
            if (table.Binding is { SortFields.Count: > 0 })
                sourceRows = ApplySourceSort(sourceRows, table.Binding.SortFields, table, source);

            foreach (var row in sourceRows)
            {
                renderedRows.Add(mappings
                    .Select(item => row.TryGetValue(item.Mapping!.SourceField, out var value)
                        ? value?.ToString() ?? string.Empty
                        : string.Empty)
                    .ToList());
            }
        }

        return BuildComposedTableNode(
            table,
            formatProfile,
            headers,
            renderedRows,
            table.Sources.Count == 1 ? table.Sources[0].DataSourceName : null,
            table.Sources.Count,
            table.Binding?.Filter is not null);
    }

    private TableContentNode BuildComposedTableNode(
        TableElement table,
        DocumentFormatProfile? formatProfile,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> normalizedRows,
        string? dataSourceName,
        int sourceCount,
        bool filterWasIgnored)
    {
        var composition = _tableContentRowComposer.Compose(table, normalizedRows);

        return new TableContentNode
        {
            ElementId = table.Id,
            Kind = ReportContentKind.Table,
            Name = table.Name,
            Caption = table.Caption,
            ColumnHeaders = headers,
            Format = _reportContentFormatResolver.ResolveTable(formatProfile, table),
            CaptionFormat = formatProfile?.TableCaption,
            CaptionSequence = formatProfile?.TableCaptionSequence,
            Rows = composition.Rows,
            CellSpans = composition.CellSpans,
            RowGroups = composition.RowGroups,
            CompositionWarnings = composition.Warnings,
            DataSourceName = dataSourceName,
            SourceCount = sourceCount,
            SourceError = null,
            FilterWasIgnored = filterWasIgnored
        };
    }

    private TableContentNode BuildMultiSourceErrorNode(
        TableElement table,
        DocumentFormatProfile? formatProfile,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> renderedRows,
        string error) => new()
    {
        ElementId = table.Id,
        Kind = ReportContentKind.Table,
        Name = table.Name,
        Caption = table.Caption,
        ColumnHeaders = headers,
        Format = _reportContentFormatResolver.ResolveTable(formatProfile, table),
        CaptionFormat = formatProfile?.TableCaption,
        CaptionSequence = formatProfile?.TableCaptionSequence,
        Rows = renderedRows,
        DataSourceName = null,
        SourceCount = table.Sources.Count,
        SourceError = error,
        FilterWasIgnored = table.Binding?.Filter is not null
    };

    private static IEnumerable<IReadOnlyDictionary<string, object?>> ApplySourceSort(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyList<SortField> sortFields,
        TableElement table,
        KKL.WordStudio.Domain.DataBinding.TableSourceBinding source)
    {
        var translated = sortFields.Select(sortField =>
        {
            var tableColumn = table.Columns.FirstOrDefault(column =>
                string.Equals(column.SourceField, sortField.FieldName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.Header, sortField.FieldName, StringComparison.OrdinalIgnoreCase));
            var sourceField = tableColumn is null
                ? sortField.FieldName
                : source.FieldMappings.FirstOrDefault(mapping => mapping.TableColumnId == tableColumn.Id)?.SourceField ?? sortField.FieldName;
            return new SortField { FieldName = sourceField, Direction = sortField.Direction };
        }).ToList();
        return ApplySort(rows, translated);
    }


    private static IReadOnlyList<DataField> ResolveFieldsForBinding(KKL.WordStudio.Domain.DataSources.DataSource dataSource, string? worksheetName)
    {
        if (dataSource is KKL.WordStudio.Domain.DataSources.ExcelDataSource excelDataSource)
        {
            var worksheet = excelDataSource.Workbook.Worksheets.FirstOrDefault(w => w.Name == worksheetName);
            if (worksheet is not null && worksheet.ColumnMappings.Count > 0)
                return worksheet.ColumnMappings.Select(m => m.TargetField).ToList();
        }

        return dataSource.Fields;
    }

    private static string ExtractCellText(Container cell) =>
        cell.Children.OfType<TextElement>().FirstOrDefault()?.Content.Text ?? string.Empty;

    private static IEnumerable<IReadOnlyDictionary<string, object?>> ApplySort(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows, IReadOnlyList<SortField> sortFields)
    {
        IOrderedEnumerable<IReadOnlyDictionary<string, object?>>? ordered = null;

        foreach (var sortField in sortFields)
        {
            object? KeySelector(IReadOnlyDictionary<string, object?> row) =>
                row.TryGetValue(sortField.FieldName, out var value) ? value : null;

            ordered = ordered is null
                ? sortField.Direction == SortDirection.Ascending ? rows.OrderBy(KeySelector) : rows.OrderByDescending(KeySelector)
                : sortField.Direction == SortDirection.Ascending ? ordered.ThenBy(KeySelector) : ordered.ThenByDescending(KeySelector);
        }

        return ordered ?? rows;
    }
}
