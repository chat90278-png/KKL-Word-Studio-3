namespace KKL.WordStudio.Infrastructure.Tests;

using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.Export.Exporters;
using KKL.WordStudio.Infrastructure.Word;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Historical Sprint 15 method identities are retained. The product no longer
/// persists a native project package; these tests now protect the equivalent
/// process-lifetime session behavior and read-only source handling.
/// </summary>
public class Sprint8PersistenceTests
{
    [Fact]
    public void TableCaption_RoundTripsThroughProjectPersistence()
    {
        var (project, _, section) = CreateProjectAndReport();
        var table = new TableElement
        {
            Name = "EngineTable",
            Caption = "Motor Tipleri"
        };
        section.Root.Children.Add(table);

        var sessionTable = Assert.IsType<TableElement>(
            project.Reports.Single().Pages.Single().Sections.Single().Root.Children.Single());
        Assert.Same(table, sessionTable);
        Assert.Equal("Motor Tipleri", sessionTable.Caption);
    }

    [Fact]
    public void FrontMatterState_RoundTripsThroughProjectPersistence()
    {
        var sourceDocx = CreateFrontMatterDocx();
        try
        {
            var service = new OpenXmlFrontMatterDocumentService();
            var imported = service.Import(sourceDocx);
            Assert.True(imported.IsSuccess, imported.Error);

            var project = new Project { Name = "Session Project" };
            project.FrontMatter = imported.Value;

            Assert.Same(imported.Value, project.FrontMatter);
            Assert.Equal("Kapak İçeriği", ReadBodyText(project.FrontMatter!.ResolvedFilePath!));
            Assert.True(service.IsAvailable(project.FrontMatter));
            Assert.Null(typeof(FrontMatterDocument).GetProperty("EmbeddedAssetEntryName"));
        }
        finally
        {
            File.Delete(sourceDocx);
        }
    }

    [Fact]
    public void FrontMatterImport_ValidatesPackageWithoutModifyingSource()
    {
        var sourceDocx = CreateFrontMatterDocx();
        try
        {
            var beforeHash = SHA256.HashData(File.ReadAllBytes(sourceDocx));
            var service = new OpenXmlFrontMatterDocumentService();

            var result = service.Import(sourceDocx);

            var afterHash = SHA256.HashData(File.ReadAllBytes(sourceDocx));
            Assert.True(result.IsSuccess);
            Assert.Equal(beforeHash, afterHash);
            Assert.Equal(sourceDocx, result.Value.ResolvedFilePath);
        }
        finally
        {
            File.Delete(sourceDocx);
        }
    }

    [Fact]
    public void MissingFrontMatterSource_DoesNotPreventProjectOpen()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "KayipKapak.docx");
        var project = new Project { Name = "Missing Front Matter" };
        project.FrontMatter = new FrontMatterDocument
        {
            FileName = "KayipKapak.docx",
            OriginalSourcePath = missingPath,
            ResolvedFilePath = missingPath
        };

        var service = new OpenXmlFrontMatterDocumentService();
        Assert.NotNull(project.FrontMatter);
        Assert.Equal("KayipKapak.docx", project.FrontMatter!.FileName);
        Assert.False(service.IsAvailable(project.FrontMatter));
        Assert.NotEmpty(project.Reports); // session remains usable even when the optional source is unavailable
    }

    private static (Project Project, Report Report, Section Section) CreateProjectAndReport()
    {
        var project = KKL.WordStudio.Application.Workspace.WorkspaceSessionFactory.CreateDefault();
        var report = Assert.Single(project.Reports);
        var section = Assert.Single(Assert.Single(report.Pages).Sections);
        return (project, report, section);
    }

    private static string CreateFrontMatterDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("Kapak İçeriği")))));
        mainPart.Document.Save();
        return path;
    }

    private static string ReadBodyText(string path)
    {
        using var document = WordprocessingDocument.Open(path, false);
        return document.MainDocumentPart!.Document.Body!.InnerText;
    }
}

public class Sprint8WordCompositionTests
{
    private sealed class NoOpDataProviderRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider) { }
        public IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException("No bound table is expected in this test.");
    }

    [Fact]
    public async Task ComposedWordDocument_ContainsFrontMatterBeforeGeneratedReport()
    {
        var frontMatterPath = CreateRichFrontMatterDocx();
        try
        {
            var (project, report) = CreateGeneratedReport(frontMatterPath);
            var exporter = CreateExporter();

            var result = await exporter.ExportAsync(project, report, ExportOptions.Default);

            Assert.True(result.IsSuccess);
            using var document = WordprocessingDocument.Open(result.Value, false);
            var mainPart = document.MainDocumentPart!;
            var body = mainPart.Document.Body!;
            var children = body.ChildElements.ToList();
            var altChunk = Assert.IsType<AltChunk>(children.First());
            var generatedParagraphIndex = children.FindIndex(
                child => child is Paragraph paragraph && paragraph.InnerText.Contains("KKL Rapor İçeriği", StringComparison.Ordinal));

            Assert.True(generatedParagraphIndex > 0);
            Assert.True(children.IndexOf(altChunk) < generatedParagraphIndex);
            Assert.IsType<Paragraph>(children[1]);
            Assert.Contains(children[1].Descendants<Break>(), b => b.Type?.Value == BreakValues.Page);

            var relationshipId = altChunk.Id?.Value;
            Assert.False(string.IsNullOrWhiteSpace(relationshipId));
            var importedPart = Assert.IsType<AlternativeFormatImportPart>(mainPart.GetPartById(relationshipId!));

            using var importedPackage = new MemoryStream();
            using (var source = importedPart.GetStream(FileMode.Open, FileAccess.Read))
                source.CopyTo(importedPackage);
            importedPackage.Position = 0;

            using var importedDocument = WordprocessingDocument.Open(importedPackage, false);
            var importedMainPart = importedDocument.MainDocumentPart!;
            Assert.Contains("Kapak İçeriği", importedMainPart.Document.Body!.InnerText);
            Assert.Contains(importedMainPart.StyleDefinitionsPart!.Styles!.Elements<Style>(), s => s.StyleId == "CoverDistinct");
            Assert.NotEmpty(importedMainPart.ImageParts);
        }
        finally
        {
            File.Delete(frontMatterPath);
        }
    }

    [Fact]
    public async Task ComposedWordDocument_ReopensWithOpenXml()
    {
        var frontMatterPath = CreateRichFrontMatterDocx();
        try
        {
            var (project, report) = CreateGeneratedReport(frontMatterPath);
            var exporter = CreateExporter();

            var result = await exporter.ExportAsync(project, report, ExportOptions.Default);
            Assert.True(result.IsSuccess);

            using var copy = new MemoryStream();
            await result.Value.CopyToAsync(copy);
            copy.Position = 0;

            using var reopened = WordprocessingDocument.Open(copy, false);
            Assert.NotNull(reopened.MainDocumentPart);
            Assert.NotNull(reopened.MainDocumentPart!.Document.Body);
            Assert.NotEmpty(reopened.MainDocumentPart.Document.Body!.Elements<AltChunk>());
            Assert.Contains("KKL Rapor İçeriği", reopened.MainDocumentPart.Document.Body.InnerText);
        }
        finally
        {
            File.Delete(frontMatterPath);
        }
    }

    private static WordExporter CreateExporter() =>
        new(new ReportContentBuilder(new NoOpDataProviderRegistry()), NullLogger<WordExporter>.Instance);

    private static (Project Project, Report Report) CreateGeneratedReport(string frontMatterPath)
    {
        var project = new Project { Name = "Composed Project" };
        project.FrontMatter = new FrontMatterDocument
        {
            FileName = "Kapak.docx",
            OriginalSourcePath = frontMatterPath,
            ResolvedFilePath = frontMatterPath
        };

        var report = new Report { Name = "Generated Report", IncludeTableOfContents = true };
        var page = new Page { ShowPageNumbers = true };
        var header = new Section { Kind = SectionKind.PageHeader };
        header.Root.Children.Add(new TextElement { Content = Expression.Literal("KKL Header") });
        var body = new Section { Kind = SectionKind.Body };
        body.Root.Children.Add(new TextElement
        {
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("KKL Rapor İçeriği")
        });
        var footer = new Section { Kind = SectionKind.PageFooter };
        footer.Root.Children.Add(new TextElement { Content = Expression.Literal("KKL Footer") });
        page.Sections.Add(header);
        page.Sections.Add(body);
        page.Sections.Add(footer);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report);
    }

    private static string CreateRichFrontMatterDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document, autoSave: false);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(
            new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "CoverDistinct" }),
                new Run(new Text("Kapak İçeriği")))));

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "CoverDistinct",
            StyleName = new StyleName { Val = "Cover Distinct" }
        });

        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        using (var imageStream = new MemoryStream(pngBytes))
            imagePart.FeedData(imageStream);

        stylesPart.Styles.Save(stylesPart);
        mainPart.Document.Save();
        return path;
    }
}
