namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Shared.Results;
using Xunit;

public sealed class Sprint15ContractBootstrapTests
{
    [Fact]
    public void PassthroughComposer_PreservesRowsAndEmitsNoSpansOrGroups()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            new[] { "PN-1", "SN-1", "1" }
        ];
        var composer = new PassthroughTableContentRowComposer();

        var result = composer.Compose(new TableElement(), rows);

        Assert.Same(rows, result.Rows);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ReportContentBuilder_ComposesRowsAfterNormalizedSingleSourceRows()
    {
        var source = CreateSource("Sales");
        var project = new Project();
        project.DataSources.Add(source);
        var (report, table) = CreateReportWithTable();
        table.Binding = new Binding { DataSourceName = source.Name, WorksheetName = "Sheet1" };
        table.Binding.SortFields.Add(new SortField
        {
            FieldName = "CustomerName",
            Direction = SortDirection.Ascending
        });
        table.Columns.Add(new TableColumn { Header = "Müşteri", SourceField = "CustomerName" });
        table.Columns.Add(new TableColumn { Header = "Tutar", SourceField = "Amount" });
        var composer = new SpyComposer();
        var builder = new ReportContentBuilder(
            new FakeRegistry(new FakeProvider(_ =>
            [
                Row(("CustomerName", "Bob"), ("Amount", "200")),
                Row(("CustomerName", "Alice"), ("Amount", "100"))
            ])),
            composer);

        var document = await builder.BuildAsync(project, report);

        var composedRows = Assert.Single(composer.Calls);
        Assert.Equal(new[] { "Alice", "100" }, composedRows[0]);
        Assert.Equal(new[] { "Bob", "200" }, composedRows[1]);
        var node = Assert.IsType<TableContentNode>(Assert.Single(document.BodyNodes));
        Assert.Equal("COMPOSED", node.Rows[0][0]);
        Assert.Single(node.CellSpans);
        Assert.Single(node.RowGroups);
        Assert.Single(node.CompositionWarnings);
    }

    [Fact]
    public async Task ReportContentBuilder_ComposesRowsAfterAllMultiSourceRows()
    {
        var firstSource = CreateSource("Source A");
        var secondSource = CreateSource("Source B");
        var project = new Project();
        project.DataSources.Add(firstSource);
        project.DataSources.Add(secondSource);
        var (report, table) = CreateReportWithTable();
        var column = new TableColumn { Header = "Parça", SourceField = "Part" };
        table.Columns.Add(column);
        table.Sources.Add(CreateTableSource(firstSource.Name, column.Id, "AField"));
        table.Sources.Add(CreateTableSource(secondSource.Name, column.Id, "BField"));
        var composer = new SpyComposer();
        var provider = new FakeProvider(definition => definition.Name == firstSource.Name
            ? [Row(("AField", "A1")), Row(("AField", "A2"))]
            : [Row(("BField", "B1"))]);
        var builder = new ReportContentBuilder(new FakeRegistry(provider), composer);

        await builder.BuildAsync(project, report);

        var composedRows = Assert.Single(composer.Calls);
        Assert.Equal(3, composedRows.Count);
        Assert.Equal("A1", composedRows[0][0]);
        Assert.Equal("A2", composedRows[1][0]);
        Assert.Equal("B1", composedRows[2][0]);
    }

    [Fact]
    public async Task ReportContentBuilder_SourceError_DoesNotComposePartialRows()
    {
        var source = CreateSource("Source A");
        var project = new Project();
        project.DataSources.Add(source);
        var (report, table) = CreateReportWithTable();
        var column = new TableColumn { Header = "Parça", SourceField = "Part" };
        table.Columns.Add(column);
        table.Sources.Add(CreateTableSource(source.Name, column.Id, "AField"));
        table.Sources.Add(CreateTableSource("Missing Source", column.Id, "BField"));
        var composer = new SpyComposer();
        var builder = new ReportContentBuilder(
            new FakeRegistry(new FakeProvider(_ => [Row(("AField", "A1"))])),
            composer);

        var document = await builder.BuildAsync(project, report);

        Assert.Empty(composer.Calls);
        var node = Assert.IsType<TableContentNode>(Assert.Single(document.BodyNodes));
        Assert.Equal("A1", Assert.Single(node.Rows)[0]);
        Assert.NotNull(node.SourceError);
        Assert.Empty(node.CellSpans);
        Assert.Empty(node.RowGroups);
        Assert.Empty(node.CompositionWarnings);
    }

    [Fact]
    public void TableContentNode_DefaultsToEmptySpanGroupAndWarnings()
    {
        var node = new TableContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Table,
            Name = "Table",
            ColumnHeaders = [],
            Rows = []
        };

        Assert.Empty(node.CellSpans);
        Assert.Empty(node.RowGroups);
        Assert.Empty(node.CompositionWarnings);
    }

    [Fact]
    public void TablePagePayload_DefaultsToEmptyFragmentSpans()
    {
        var payload = new TablePageBlockPayload
        {
            Name = "Table",
            Caption = null,
            ColumnHeaders = [],
            Rows = [],
            StartRowIndex = 0,
            HasHeader = false,
            IsHeaderRepeated = false,
            SourceError = null
        };

        Assert.Empty(payload.CellSpans);
    }

    private static ExcelDataSource CreateSource(string name)
    {
        var source = new ExcelDataSource
        {
            Name = name,
            Workbook = new Workbook { FileName = $"{name}.xlsx" },
            ActiveWorksheetName = "Sheet1"
        };
        source.Workbook.Worksheets.Add(new Worksheet { Name = "Sheet1" });
        return source;
    }

    private static (Report Report, TableElement Table) CreateReportWithTable()
    {
        var report = new Report();
        var page = new Page();
        var section = new Section { Kind = SectionKind.Body };
        var table = new TableElement { Name = "Table" };
        section.Root.Children.Add(table);
        page.Sections.Add(section);
        report.Pages.Add(page);
        return (report, table);
    }

    private static TableSourceBinding CreateTableSource(string sourceName, Guid columnId, string sourceField)
    {
        var source = new TableSourceBinding
        {
            DataSourceName = sourceName,
            WorksheetName = "Sheet1",
            Range = new DataRange
            {
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 1,
                EndColumn = 1
            }
        };
        source.FieldMappings.Add(new TableSourceFieldMapping
        {
            TableColumnId = columnId,
            SourceField = sourceField
        });
        return source;
    }

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value);

    private sealed class SpyComposer : ITableContentRowComposer
    {
        public List<IReadOnlyList<IReadOnlyList<string>>> Calls { get; } = new();

        public TableRowCompositionResult Compose(
            TableElement table,
            IReadOnlyList<IReadOnlyList<string>> normalizedRows)
        {
            Calls.Add(normalizedRows);
            return new TableRowCompositionResult
            {
                Rows = [new[] { "COMPOSED" }],
                CellSpans = [new TableCellSpan { RowIndex = 0, ColumnIndex = 0, RowSpan = 2 }],
                RowGroups = [new TableRowGroup { StartRowIndex = 0, RowCount = 2, KeepTogetherWhenPossible = true }],
                Warnings = ["composition warning"]
            };
        }
    }

    private sealed class FakeRegistry(IDataProvider provider) : IDataProviderRegistry
    {
        public void Register(IDataProvider candidate)
        {
        }

        public IDataProvider Resolve(string providerKey) => provider;
    }

    private sealed class FakeProvider(
        Func<IDataSourceDefinition, IReadOnlyList<IReadOnlyDictionary<string, object?>>> rowsFactory) : IDataProvider
    {
        public string ProviderKey => "excel";

        public Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> GetRowsAsync(
            IDataSourceDefinition definition,
            CancellationToken cancellationToken = default,
            string? worksheetNameOverride = null,
            DataRange? rangeOverride = null) =>
            Task.FromResult(Result.Success(rowsFactory(definition)));
    }
}
