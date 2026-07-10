namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.DataSources;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.Export.Exporters;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

public sealed class Sprint14WordFidelityTests
{
    [Fact]
    public void HeadingWordParagraph_UsesKeepNext()
    {
        var node = new TextContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Heading,
            Text = "Başlık",
            FontSize = 18,
            Bold = true
        };

        var paragraph = WordParagraphWriter.BuildParagraph(node);

        Assert.NotNull(paragraph.ParagraphProperties?.GetFirstChild<KeepNext>());
    }

    [Fact]
    public void WordTable_HeaderRowRepeats()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode());

        var headerRow = table.Elements<OpenXmlTableRow>().First();
        Assert.NotNull(headerRow.TableRowProperties?.GetFirstChild<TableHeader>());
    }

    [Fact]
    public void WordTable_UsesDeterministicFullWidthLayout()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode());
        var properties = table.GetFirstChild<TableProperties>()!;
        var tableWidth = properties.GetFirstChild<TableWidth>()!;
        var layout = properties.GetFirstChild<TableLayout>()!;
        var firstRowWidths = table.Elements<OpenXmlTableRow>().First().Elements<TableCell>()
            .Select(cell => cell.TableCellProperties!.GetFirstChild<TableCellWidth>()!.Width!.Value)
            .ToArray();

        Assert.Equal(TableWidthUnitValues.Pct, tableWidth.Type!.Value);
        Assert.Equal("5000", tableWidth.Width!.Value);
        Assert.Equal(TableLayoutValues.Fixed, layout.Type!.Value);
        Assert.Equal(2, firstRowWidths.Length);
        Assert.All(firstRowWidths, width => Assert.Equal("2500", width));
    }

    [Fact]
    public void LandscapePageLayout_WritesConsistentOrientation()
    {
        var sectionProperties = new SectionProperties();
        var layout = new PageLayout
        {
            WidthMillimeters = 297,
            HeightMillimeters = 210,
            MarginTopMillimeters = 20,
            MarginBottomMillimeters = 20,
            MarginLeftMillimeters = 20,
            MarginRightMillimeters = 20,
            ShowPageNumbers = true
        };

        WordPageLayoutWriter.AppendPageLayout(sectionProperties, layout);

        var pageSize = sectionProperties.GetFirstChild<PageSize>()!;
        Assert.Equal(PageOrientationValues.Landscape, pageSize.Orient!.Value);
        Assert.True(pageSize.Width!.Value > pageSize.Height!.Value);
    }

    [Fact]
    public async Task FrontMatterAltChunkComposition_RemainsIntact()
    {
        var frontMatterPath = CreateFrontMatterDocx();
        try
        {
            var (project, report) = CreateReport(frontMatterPath);
            var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);

            Assert.True(result.IsSuccess);
            using var document = WordprocessingDocument.Open(result.Value, false);
            var body = document.MainDocumentPart!.Document.Body!;
            var children = body.ChildElements.ToList();
            var altChunk = Assert.IsType<AltChunk>(children[0]);

            Assert.Contains(children[1].Descendants<Break>(), item => item.Type?.Value == BreakValues.Page);
            Assert.IsType<AlternativeFormatImportPart>(document.MainDocumentPart.GetPartById(altChunk.Id!.Value!));
            Assert.Contains(children.Skip(2).OfType<Paragraph>(), paragraph => paragraph.InnerText.Contains("Sprint 14 içerik", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(frontMatterPath);
        }
    }

    [Fact]
    public async Task GeneratedWord_ReopensWithOpenXmlAfterFidelityChanges()
    {
        var frontMatterPath = CreateFrontMatterDocx();
        try
        {
            var (project, report) = CreateReport(frontMatterPath);
            var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);

            Assert.True(result.IsSuccess);
            using var copy = new MemoryStream();
            await result.Value.CopyToAsync(copy);
            copy.Position = 0;

            using var reopened = WordprocessingDocument.Open(copy, false);
            Assert.NotNull(reopened.MainDocumentPart?.Document.Body);
            Assert.NotEmpty(reopened.MainDocumentPart!.Document.Body!.Elements<AltChunk>());
            Assert.Contains(
                reopened.MainDocumentPart.Document.Body.Descendants<Paragraph>(),
                paragraph => paragraph.ParagraphProperties?.GetFirstChild<KeepNext>() is not null);
        }
        finally
        {
            File.Delete(frontMatterPath);
        }
    }

    private static TableContentNode CreateTableNode() => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = ReportContentKind.Table,
        Name = "Tablo",
        Caption = "Tablo 1",
        ColumnHeaders = new[] { "A", "B" },
        Rows = new IReadOnlyList<string>[]
        {
            new[] { "1", "2" },
            new[] { "3", "4" }
        },
        DataSourceName = null,
        SourceCount = 0,
        SourceError = null,
        FilterWasIgnored = false
    };

    private static WordExporter CreateExporter() =>
        new(new ReportContentBuilder(new NoOpDataProviderRegistry()), NullLogger<WordExporter>.Instance);

    private static (Project Project, Report Report) CreateReport(string frontMatterPath)
    {
        var project = new Project { Name = "Sprint 14 Word" };
        project.FrontMatter = new FrontMatterDocument
        {
            FileName = "Kapak.docx",
            OriginalSourcePath = frontMatterPath,
            ResolvedFilePath = frontMatterPath
        };

        var report = new Report { Name = "Word Fidelity" };
        var page = new Page
        {
            WidthMillimeters = 297,
            HeightMillimeters = 210,
            Orientation = PageOrientation.Landscape
        };
        var body = new Section { Kind = SectionKind.Body };
        body.Root.Children.Add(new TextElement
        {
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("Sprint 14 içerik")
        });
        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report);
    }

    private static string CreateFrontMatterDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document, autoSave: false);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("Kapak")))));
        mainPart.Document.Save();
        return path;
    }

    private sealed class NoOpDataProviderRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider)
        {
        }

        public IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException("This fixture does not contain bound tables.");
    }
}
