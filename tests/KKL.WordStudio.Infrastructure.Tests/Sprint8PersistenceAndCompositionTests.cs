namespace KKL.WordStudio.Infrastructure.Tests;

using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.Export.Exporters;
using KKL.WordStudio.Infrastructure.Word;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Historical Sprint 15 identities retained after native project persistence
/// removal. These tests protect process-lifetime session state and read-only
/// imported Word sources.
/// </summary>
public class Sprint8PersistenceTests
{
    [Fact]
    public void TableCaption_RoundTripsThroughProjectPersistence()
    {
        var project = WorkspaceSessionFactory.CreateDefault();
        var section = Assert.Single(Assert.Single(Assert.Single(project.Reports).Pages).Sections);
        var table = new TableElement { Name = "EngineTable", Caption = "Motor Tipleri" };
        section.Root.Children.Add(table);

        var sessionTable = Assert.IsType<TableElement>(Assert.Single(section.Root.Children));
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

            var project = WorkspaceSessionFactory.CreateDefault();
            project.FrontMatter = imported.Value;

            Assert.Same(imported.Value, project.FrontMatter);
            Assert.True(service.IsAvailable(project.FrontMatter!));
            Assert.Equal(sourceDocx, project.FrontMatter!.ResolvedFilePath);
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
            var result = new OpenXmlFrontMatterDocumentService().Import(sourceDocx);
            var afterHash = SHA256.HashData(File.ReadAllBytes(sourceDocx));

            Assert.True(result.IsSuccess, result.Error);
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
        var project = WorkspaceSessionFactory.CreateDefault();
        project.FrontMatter = new FrontMatterDocument
        {
            FileName = "KayipKapak.docx",
            OriginalSourcePath = missingPath,
            ResolvedFilePath = missingPath
        };

        var service = new OpenXmlFrontMatterDocumentService();
        Assert.False(service.IsAvailable(project.FrontMatter));
        Assert.Single(project.Reports);
        Assert.Single(Assert.Single(project.Reports).Pages);
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
        var frontMatterPath = CreateFrontMatterDocx();
        try
        {
            var (project, report) = CreateGeneratedReport(frontMatterPath);
            var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);

            Assert.True(result.IsSuccess, result.Error);
            using var document = WordprocessingDocument.Open(result.Value, false);
            var body = document.MainDocumentPart!.Document.Body!;
            var children = body.ChildElements.ToList();
            var altChunkIndex = children.FindIndex(child => child is AltChunk);
            var generatedIndex = children.FindIndex(child =>
                child is Paragraph paragraph
                && paragraph.InnerText.Contains("KKL Rapor İçeriği", StringComparison.Ordinal));

            Assert.Equal(0, altChunkIndex);
            Assert.True(generatedIndex > altChunkIndex);
            Assert.Contains(children.OfType<Paragraph>().SelectMany(p => p.Descendants<Break>()),
                item => item.Type?.Value == BreakValues.Page);
        }
        finally
        {
            File.Delete(frontMatterPath);
        }
    }

    [Fact]
    public async Task ComposedWordDocument_ReopensWithOpenXml()
    {
        var frontMatterPath = CreateFrontMatterDocx();
        try
        {
            var (project, report) = CreateGeneratedReport(frontMatterPath);
            var result = await CreateExporter().ExportAsync(project, report, ExportOptions.Default);
            Assert.True(result.IsSuccess, result.Error);

            using var copy = new MemoryStream();
            await result.Value.CopyToAsync(copy);
            copy.Position = 0;

            using var reopened = WordprocessingDocument.Open(copy, false);
            Assert.NotNull(reopened.MainDocumentPart?.Document.Body);
            Assert.NotEmpty(reopened.MainDocumentPart!.Document.Body!.Elements<AltChunk>());
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
        var project = WorkspaceSessionFactory.CreateDefault();
        project.FrontMatter = new FrontMatterDocument
        {
            FileName = "Kapak.docx",
            OriginalSourcePath = frontMatterPath,
            ResolvedFilePath = frontMatterPath
        };

        var report = Assert.Single(project.Reports);
        report.IncludeTableOfContents = true;
        var body = Assert.Single(Assert.Single(report.Pages).Sections);
        body.Root.Children.Add(new TextElement
        {
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("KKL Rapor İçeriği")
        });
        return (project, report);
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
