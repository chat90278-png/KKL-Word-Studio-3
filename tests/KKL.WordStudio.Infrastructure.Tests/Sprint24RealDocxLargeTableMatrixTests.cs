namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.Export.Exporters;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainTableRow = KKL.WordStudio.Domain.Elements.TableRow;
using OpenXmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

/// <summary>
/// End-to-end DOCX matrix for the real ReportContentBuilder -> WordExporter path.
/// The fixtures are deliberately moderate (100 rows) so they exercise multi-page
/// Word behavior without turning the normal test suite into a performance test.
/// </summary>
public sealed class Sprint24RealDocxLargeTableMatrixTests
{
    [Fact]
    public async Task HundredRowTable_WritesEverySemanticRowExactlyOnceAndInOrder()
    {
        var (project, report) = CreateReportWithTables(
            CreateStaticTable("Parçalar", "Parça Listesi", rowCount: 100, columnCount: 6));

        var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);

        Assert.True(result.IsSuccess, result.Error);
        using var document = WordprocessingDocument.Open(result.Value, false);
        var body = document.MainDocumentPart!.Document.Body!;
        var table = Assert.Single(body.Elements<OpenXmlTable>());
        var rows = table.Elements<OpenXmlTableRow>().ToList();

        Assert.Equal(101, rows.Count); // one header + one hundred data rows
        Assert.All(rows, row => Assert.Equal(6, row.Elements<TableCell>().Count()));

        var actualKeys = rows
            .Skip(1)
            .Select(row => row.Elements<TableCell>().First().InnerText)
            .ToArray();
        var expectedKeys = Enumerable.Range(1, 100)
            .Select(index => $"ROW-{index:000}")
            .ToArray();

        Assert.Equal(expectedKeys, actualKeys);
        foreach (var key in expectedKeys)
        {
            Assert.Equal(
                1,
                body.Descendants<Text>().Count(text => string.Equals(text.Text, key, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public async Task HundredRowTable_RepeatsHeaderAndMarksEveryRowCantSplit()
    {
        var (project, report) = CreateReportWithTables(
            CreateStaticTable("Motorlar", "Motor Listesi", rowCount: 100, columnCount: 4));

        var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);

        Assert.True(result.IsSuccess, result.Error);
        using var document = WordprocessingDocument.Open(result.Value, false);
        var table = Assert.Single(document.MainDocumentPart!.Document.Body!.Elements<OpenXmlTable>());
        var rows = table.Elements<OpenXmlTableRow>().ToList();

        Assert.Equal(101, rows.Count);
        Assert.NotNull(rows[0].TableRowProperties?.GetFirstChild<TableHeader>());
        Assert.All(rows, row =>
            Assert.NotNull(row.TableRowProperties?.GetFirstChild<CantSplit>()));

        // Shared pagination policy keeps the header and the leading rows chained
        // so Word does not strand a caption/header with no meaningful data start.
        Assert.All(rows.Take(3), row =>
            Assert.All(row.Descendants<Paragraph>(), paragraph =>
                Assert.NotNull(paragraph.ParagraphProperties?.GetFirstChild<KeepNext>())));
    }

    [Fact]
    public async Task ConsecutiveTables_DoNotInsertAnExplicitBlankPageBetweenTables()
    {
        var first = CreateStaticTable("Birinci", "Birinci Tablo", rowCount: 3, columnCount: 3);
        var second = CreateStaticTable("İkinci", "İkinci Tablo", rowCount: 3, columnCount: 3);
        var (project, report) = CreateReportWithTables(first, second);

        var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);

        Assert.True(result.IsSuccess, result.Error);
        using var document = WordprocessingDocument.Open(result.Value, false);
        var body = document.MainDocumentPart!.Document.Body!;
        var children = body.ChildElements.ToList();
        var tableIndexes = children
            .Select((child, index) => (child, index))
            .Where(item => item.child is OpenXmlTable)
            .Select(item => item.index)
            .ToArray();

        Assert.Equal(2, tableIndexes.Length);
        var between = children
            .Skip(tableIndexes[0] + 1)
            .Take(tableIndexes[1] - tableIndexes[0] - 1)
            .ToList();

        Assert.Contains(between.OfType<Paragraph>(), paragraph =>
            paragraph.InnerText.Contains("İkinci Tablo", StringComparison.Ordinal));
        Assert.DoesNotContain(
            between.SelectMany(child => child.Descendants<Break>()),
            pageBreak => pageBreak.Type?.Value == BreakValues.Page);
        Assert.DoesNotContain(
            between.OfType<Paragraph>(),
            paragraph => paragraph.ParagraphProperties?.GetFirstChild<PageBreakBefore>() is not null);
    }

    [Fact]
    public async Task LongCellText_WritesPhysicalDocxThatReopensAndValidatesWithoutDataLoss()
    {
        var table = CreateStaticTable("Uzun Metin", "Uzun Hücreler", rowCount: 40, columnCount: 3);
        var longMarker = "LONG-CELL-MARKER-017";
        var longText = $"{longMarker} {string.Join(" ", Enumerable.Repeat("bakım-açıklaması", 90))}";
        table.Rows[16].Cells[1].Children.Clear();
        table.Rows[16].Cells[1].Children.Add(new TextElement
        {
            Content = Expression.Literal(longText)
        });
        var (project, report) = CreateReportWithTables(table);
        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");

        try
        {
            var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);
            Assert.True(result.IsSuccess, result.Error);

            await using (var output = File.Create(outputPath))
                await result.Value.CopyToAsync(output);

            Assert.True(new FileInfo(outputPath).Length > 0);
            using var document = WordprocessingDocument.Open(outputPath, false);
            var body = document.MainDocumentPart!.Document.Body!;
            var allText = string.Join(" ", body.Descendants<Text>().Select(text => text.Text));
            var validationErrors = new OpenXmlValidator().Validate(document).ToList();

            Assert.Contains(longMarker, allText, StringComparison.Ordinal);
            Assert.Contains("bakım-açıklaması", allText, StringComparison.Ordinal);
            Assert.True(
                validationErrors.Count == 0,
                BuildValidationFailureMessage(validationErrors));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private static string BuildValidationFailureMessage(
        IReadOnlyList<ValidationErrorInfo> validationErrors) =>
        string.Join(
            Environment.NewLine + Environment.NewLine,
            validationErrors.Select((error, index) => string.Join(
                Environment.NewLine,
                $"Validation error {index + 1}",
                $"Id: {error.Id}",
                $"Description: {error.Description}",
                $"Part: {error.Part?.Uri}",
                $"Path: {error.Path}",
                $"Node: {error.Node?.OuterXml}")));

    private static (Project Project, Report Report) CreateReportWithTables(params TableElement[] tables)
    {
        var project = new Project { Name = "DOCX Matrix" };
        var report = new Report { Name = "Large Table Matrix" };
        var page = new Page();
        var body = new Section { Kind = SectionKind.Body };

        foreach (var table in tables)
            body.Root.Children.Add(table);

        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report);
    }

    private static TableElement CreateStaticTable(
        string name,
        string caption,
        int rowCount,
        int columnCount)
    {
        var table = new TableElement { Name = name, Caption = caption };
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            table.Columns.Add(new TableColumn
            {
                Header = $"Sütun {columnIndex + 1}",
                Width = 100d / columnCount
            });
        }

        for (var rowIndex = 1; rowIndex <= rowCount; rowIndex++)
        {
            var row = new DomainTableRow();
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var value = columnIndex == 0
                    ? $"ROW-{rowIndex:000}"
                    : $"R{rowIndex:000}-C{columnIndex + 1:00}";
                var cell = new Container();
                cell.Children.Add(new TextElement { Content = Expression.Literal(value) });
                row.Cells.Add(cell);
            }
            table.Rows.Add(row);
        }

        return table;
    }

    private static WordExporter CreateExporter() =>
        new(new ReportContentBuilder(new NoOpDataProviderRegistry()), NullLogger<WordExporter>.Instance);

    private sealed class NoOpDataProviderRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider)
        {
        }

        public IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException("The real DOCX matrix contains only static tables.");
    }
}
