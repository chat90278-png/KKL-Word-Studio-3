namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint17SerialQuantityBuilderPipelineTests
{
    [Fact]
    public async Task RealPhysicalSerialRows_FlowThroughReportContentBuilderAsGroupedSemanticRows()
    {
        var table = CreateConfiguredTable(
            "No",
            "Tr İsim",
            "Parça Numarası",
            "NSN",
            "Seri Numarası",
            "Adet");
        AddDetailRow(table, "1", "elma", "1234", "45-50-60", "9999", "1");
        AddDetailRow(table, "2", "armut", "56789", "459-485-5", "9987", "1");
        AddDetailRow(table, "", "armut", "56789", "", "9988", "1");

        var project = new Project { Name = "Sprint17 project" };
        var report = new Report { Name = "Sprint17 report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body };
        body.Root.Children.Add(table);
        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);

        var builder = new ReportContentBuilder(
            new NoProviderRegistry(),
            new SerialQuantityTableContentRowComposer(),
            new NoReferenceDocumentFormatProvider(),
            new ReferenceReportContentFormatResolver());

        var document = await builder.BuildAsync(project, report);
        var node = Assert.IsType<TableContentNode>(Assert.Single(document.BodyNodes));

        Assert.Equal(3, node.Rows.Count);
        Assert.Equal("9999", node.Rows[0][4]);
        Assert.Equal("9987", node.Rows[1][4]);
        Assert.Equal("9988", node.Rows[2][4]);
        Assert.Equal("2", node.Rows[1][5]);
        Assert.Equal(string.Empty, node.Rows[2][5]);

        var group = Assert.Single(node.RowGroups);
        Assert.Equal(1, group.StartRowIndex);
        Assert.Equal(2, group.RowCount);
        Assert.True(group.KeepTogetherWhenPossible);

        Assert.Equal(5, node.CellSpans.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 5 }, node.CellSpans.Select(span => span.ColumnIndex).OrderBy(index => index));
        Assert.All(node.CellSpans, span =>
        {
            Assert.Equal(1, span.RowIndex);
            Assert.Equal(2, span.RowSpan);
        });
        Assert.DoesNotContain(node.CellSpans, span => span.ColumnIndex == 4);
        Assert.Empty(node.CompositionWarnings);
    }

    private static TableElement CreateConfiguredTable(params string[] headers)
    {
        var table = new TableElement { Name = "Sprint17 builder pipeline table" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });

        table.SerialQuantityGrouping = new SerialQuantityGroupingDetector().Detect(table.Columns);
        Assert.NotNull(table.SerialQuantityGrouping);
        return table;
    }

    private static void AddDetailRow(TableElement table, params string[] values)
    {
        var row = new TableRow { Kind = TableRowKind.Detail };
        foreach (var value in values)
        {
            var cell = new Container();
            cell.Children.Add(new TextElement { Content = Expression.Literal(value) });
            row.Cells.Add(cell);
        }
        table.Rows.Add(row);
    }

    private sealed class NoProviderRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider)
        {
        }

        public IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException($"Static table regression should not resolve provider '{providerKey}'.");
    }
}
