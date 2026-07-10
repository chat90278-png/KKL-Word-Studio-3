namespace KKL.WordStudio.Infrastructure.Tests;

using System.IO.Compression;
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
using KKL.WordStudio.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class Sprint8PersistenceTests
{
    [Fact]
    public async Task TableCaption_RoundTripsThroughProjectPersistence()
    {
        var repository = new KwsProjectRepository(NullLogger<KwsProjectRepository>.Instance);
        var (project, _, section) = CreateProjectAndReport();
        section.Root.Children.Add(new TableElement
        {
            Name = "EngineTable",
            Caption = "Motor Tipleri"
        });

        var basePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var savedPath = basePath + ".kws";
        try
        {
            Assert.True((await repository.SaveAsync(project, basePath)).IsSuccess);

            var opened = await repository.OpenAsync(savedPath);

            Assert.True(opened.IsSuccess);
            var openedTable = Assert.IsType<TableElement>(
                opened.Value.Reports.Single().Pages.Single().Sections.Single().Root.Children.Single());
            Assert.Equal("Motor Tipleri", openedTable.Caption);
        }
        finally
        {
            File.Delete(savedPath);
        }
    }

    [Fact]
    public async Task FrontMatterState_RoundTripsThroughProjectPersistence()
    {
        var repository = new KwsProjectRepository(NullLogger<KwsProjectRepository>.Instance);
        var sourceDocx = CreateFrontMatterDocx();
        var basePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var savedPath = basePath + ".kws";
        try
        {
            var project = new Project { Name = "Portable Project" };
            project.FrontMatter = new FrontMatterDocument
            {
                FileName = "Kapak.docx",
                OriginalSourcePath = sourceDocx,
                ResolvedFilePath = sourceDocx
            };

            Assert.True((await repository.SaveAsync(project, basePath)).IsSuccess);
            File.Delete(sourceDocx); // project-owned asset must now be sufficient

            using (var package = ZipFile.OpenRead(savedPath))
            {
                Assert.NotNull(package.GetEntry(FrontMatterDocument.DefaultEmbeddedAssetEntryName));
            }

            var opened = await repository.OpenAsync(savedPath);

            Assert.True(opened.IsSuccess);
            Assert.NotNull(opened.Value.FrontMatter);
            Assert.Equal("Kapak.docx", opened.Value.FrontMatter!.FileName);
            Assert.NotNull(opened.Value.FrontMatter.ResolvedFilePath);
            Assert.True(File.Exists(opened.Value.FrontMatter.ResolvedFilePath));
        }
        finally
        {
            File.Delete(sourceDocx);
            File.Delete(savedPath);
        }
    }

    [Fact]
    public void FrontMatterImport_ValidatesPackageWithoutModifyingSource()
    {
        var sourceDocx = CreateFrontMatterDocx();
        try
        {
            var beforeHash = SHA256.HashData(File.ReadAllBytes(sourceDocx));
            var service = new KKL.WordStudio.Infrastructure.Word.OpenXmlFrontMatterDocumentService();

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
    public async Task MissingFrontMatterSource_DoesNotPreventProjectOpen()
    {
        var repository = new KwsProjectRepository(NullLogger<KwsProjectRepository>.Instance);
        var basePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var savedPath = basePath + ".kws";
        try
        {
            var project = new Project { Name = "Missing Front Matter" };
            project.FrontMatter = new FrontMatterDocument
            {
                FileName = "KayipKapak.docx",
                OriginalSourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "KayipKapak.docx")
            };

            Assert.True((await repository.SaveAsync(project, basePath)).IsSuccess);

            var opened = await repository.OpenAsync(savedPath);

            Assert.True(opened.IsSuccess);
            Assert.NotNull(opened.Value.FrontMatter);
            Assert.Equal("KayipKapak.docx", opened.Value.FrontMatter!.FileName);
            Assert.True(string.IsNullOrWhiteSpace(opened.Value.FrontMatter.ResolvedFilePath)
                || !File.Exists(opened.Value.FrontMatter.ResolvedFilePath));
        }
        finally
        {
            File.Delete(savedPath);
        }
    }

    private static (Project Project, Report Report, Section Section) CreateProjectAndReport()
    {
        var project = new Project { Name = "Sprint 8 Persistence" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        var section = new Section { Kind = SectionKind.Body };
        page.Sections.Add(section);
        report.Pages.Add(page);
        project.Reports.Add(report);
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

        // A deterministic one-pixel PNG keeps the fixture independent of
        // System.Drawing/Windows while still proving the imported package's
        // media part survives composition as part of the nested DOCX package.
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
