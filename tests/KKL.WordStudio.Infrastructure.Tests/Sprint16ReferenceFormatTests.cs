namespace KKL.WordStudio.Infrastructure.Tests;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using KKL.WordStudio.Application.DependencyInjection;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Infrastructure.DependencyInjection;
using KKL.WordStudio.Infrastructure.Persistence;
using KKL.WordStudio.Infrastructure.ReferenceFormatting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class Sprint16ReferenceFormatTests
{
    [Fact]
    public void ReferenceImport_RejectsNonDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
        File.WriteAllText(path, "not a docx");
        try
        {
            var service = new OpenXmlReferenceFormatDocumentService();
            var result = service.Import(path);

            Assert.True(result.IsFailure);
            Assert.Contains(".docx", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReferenceImport_DoesNotModifySource()
    {
        var path = Sprint16ReferenceFixture.CreateSeroLikeDocx();
        try
        {
            var before = SHA256.HashData(File.ReadAllBytes(path));
            var service = new OpenXmlReferenceFormatDocumentService();

            var result = service.Import(path);

            var after = SHA256.HashData(File.ReadAllBytes(path));
            Assert.True(result.IsSuccess);
            Assert.Equal(before, after);
            Assert.Equal(path, result.Value.ResolvedFilePath);
            Assert.Equal(ReferenceFormatDocument.DefaultEmbeddedAssetEntryName, result.Value.EmbeddedAssetEntryName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReferencePersistence_EmbedsAndReopensWithoutOriginalPath()
    {
        var sourcePath = Sprint16ReferenceFixture.CreateSeroLikeDocx();
        var saveBasePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var savedPath = saveBasePath + ".kws";
        try
        {
            var repository = new KwsProjectRepository(NullLogger<KwsProjectRepository>.Instance);
            var project = new Project { Name = "Reference Persistence" };
            project.ReferenceFormat = new ReferenceFormatDocument
            {
                FileName = "Sero.docx",
                OriginalSourcePath = sourcePath,
                ResolvedFilePath = sourcePath
            };

            Assert.True((await repository.SaveAsync(project, saveBasePath)).IsSuccess);
            File.Delete(sourcePath);

            using (var archive = ZipFile.OpenRead(savedPath))
                Assert.NotNull(archive.GetEntry(ReferenceFormatDocument.DefaultEmbeddedAssetEntryName));

            var opened = await repository.OpenAsync(savedPath);

            Assert.True(opened.IsSuccess);
            Assert.NotNull(opened.Value.ReferenceFormat);
            Assert.Equal("Sero.docx", opened.Value.ReferenceFormat!.FileName);
            Assert.NotNull(opened.Value.ReferenceFormat.ResolvedFilePath);
            Assert.True(File.Exists(opened.Value.ReferenceFormat.ResolvedFilePath));

            var provider = new OpenXmlReferenceDocumentFormatProvider();
            var formatResult = await provider.ReadAsync(opened.Value);
            Assert.NotNull(formatResult.Profile);
            Assert.False(formatResult.IsMissing);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(savedPath);
        }
    }

    [Fact]
    public async Task ReferenceAndFrontMatter_UseSeparateAssets()
    {
        var referencePath = Sprint16ReferenceFixture.CreateSeroLikeDocx();
        var frontMatterPath = Sprint16ReferenceFixture.CreateSimpleDocx("Kapak");
        var saveBasePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var savedPath = saveBasePath + ".kws";
        try
        {
            var project = new Project { Name = "Separate Assets" };
            project.ReferenceFormat = new ReferenceFormatDocument
            {
                FileName = "Reference.docx",
                OriginalSourcePath = referencePath,
                ResolvedFilePath = referencePath
            };
            project.FrontMatter = new FrontMatterDocument
            {
                FileName = "Cover.docx",
                OriginalSourcePath = frontMatterPath,
                ResolvedFilePath = frontMatterPath
            };

            var repository = new KwsProjectRepository(NullLogger<KwsProjectRepository>.Instance);
            Assert.True((await repository.SaveAsync(project, saveBasePath)).IsSuccess);

            using var archive = ZipFile.OpenRead(savedPath);
            var referenceEntry = archive.GetEntry(ReferenceFormatDocument.DefaultEmbeddedAssetEntryName);
            var frontMatterEntry = archive.GetEntry(FrontMatterDocument.DefaultEmbeddedAssetEntryName);
            Assert.NotNull(referenceEntry);
            Assert.NotNull(frontMatterEntry);
            Assert.NotEqual(referenceEntry!.FullName, frontMatterEntry!.FullName);
        }
        finally
        {
            File.Delete(referencePath);
            File.Delete(frontMatterPath);
            File.Delete(savedPath);
        }
    }

    [Fact]
    public async Task SeroProfile_ExtractsA4And25MmMargins()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            Assert.InRange(profile.Page.WidthMillimeters, 209.9d, 210.1d);
            Assert.InRange(profile.Page.HeightMillimeters, 296.9d, 297.1d);
            Assert.InRange(profile.Page.MarginTopMillimeters, 24.9d, 25.1d);
            Assert.InRange(profile.Page.MarginBottomMillimeters, 24.9d, 25.1d);
            Assert.InRange(profile.Page.MarginLeftMillimeters, 24.9d, 25.1d);
            Assert.InRange(profile.Page.MarginRightMillimeters, 24.9d, 25.1d);
            Assert.InRange(profile.Page.HeaderDistanceMillimeters, 12.4d, 12.6d);
            Assert.InRange(profile.Page.FooterDistanceMillimeters, 12.4d, 12.6d);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroProfile_ExtractsArialBase()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            Assert.Equal("Arial", profile.BodyText.FontFamilyName);
            Assert.Equal(10d, profile.BodyText.FontSizePoints, 3);
            Assert.False(profile.BodyText.Bold);
            Assert.Equal(ParagraphAlignment.Left, profile.BodyText.Alignment);
            Assert.Equal(1d, profile.BodyText.LineSpacingMultiple, 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroProfile_ExtractsPrimaryAndSecondaryHeadingFormats()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            Assert.Equal("Arial", profile.PrimaryHeading.FontFamilyName);
            Assert.Equal(12d, profile.PrimaryHeading.FontSizePoints, 3);
            Assert.True(profile.PrimaryHeading.Bold);
            Assert.True(profile.PrimaryHeading.Italic);
            Assert.True(profile.PrimaryHeading.KeepWithNext);
            Assert.Equal(4d, profile.PrimaryHeading.SpaceBeforePoints, 3);
            Assert.Equal(2d, profile.PrimaryHeading.SpaceAfterPoints, 3);

            Assert.Equal(12d, profile.SecondaryHeading.FontSizePoints, 3);
            Assert.True(profile.SecondaryHeading.Bold);
            Assert.False(profile.SecondaryHeading.Italic);
            Assert.True(profile.SecondaryHeading.KeepWithNext);
            Assert.InRange(profile.SecondaryHeading.LeftIndentMillimeters, 21.7d, 21.8d);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SeroProfile_ExtractsCenteredBodySupport()
    {
        var path = Sprint16ReferenceFixture.CreateSeroLikeDocx();
        try
        {
            using var document = WordprocessingDocument.Open(path, false);
            var mainPart = document.MainDocumentPart!;
            var documentXml = Sprint16ReferenceFixture.LoadXml(mainPart);
            var stylesXml = Sprint16ReferenceFixture.LoadXml(mainPart.StyleDefinitionsPart!);
            var resolver = new OpenXmlStyleResolver(stylesXml);
            var paragraph = documentXml.Descendants(Sprint16ReferenceFixture.W + "p")
                .Single(p => p.Value.Contains("UAV Tail Number", StringComparison.Ordinal));
            var run = paragraph.Descendants(Sprint16ReferenceFixture.W + "r").Single();

            var paragraphFormat = resolver.ResolveParagraph(paragraph);
            var runFormat = resolver.ResolveRun(paragraph, run);

            Assert.Equal(ParagraphAlignment.Center, paragraphFormat.Alignment);
            Assert.Equal("Arial", runFormat.FontFamilyName);
            Assert.Equal(11d, runFormat.FontSizePoints, 3);
            Assert.True(runFormat.Bold);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroProfile_ExtractsCaptionAndSeqMetadata()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            Assert.Equal("Arial", profile.TableCaption.FontFamilyName);
            Assert.Equal(8d, profile.TableCaption.FontSizePoints, 3);
            Assert.True(profile.TableCaption.Bold);
            Assert.Equal(ParagraphAlignment.Center, profile.TableCaption.Alignment);
            Assert.True(profile.TableCaption.KeepWithNext);
            Assert.Equal(2d, profile.TableCaption.LineSpacingMultiple, 3);

            var sequence = Assert.IsType<TableCaptionSequenceProfile>(profile.TableCaptionSequence);
            Assert.Equal("Table", sequence.DisplayLabel);
            Assert.Equal("Tablo", sequence.SequenceIdentifier);
            Assert.Equal(". ", sequence.Separator);
            Assert.Contains(profile.Warnings, warning => warning.Contains("görünür etiket", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(profile.Warnings, warning => warning.Contains("yazı boyut", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroProfile_ExtractsTwoSeparateTableProfiles()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            Assert.Equal(2, profile.TableFormats.Count);
            Assert.Equal("table-001", profile.TableFormats[0].Key);
            Assert.Equal("table-002", profile.TableFormats[1].Key);
            Assert.Equal("Referans Tablo 1", profile.TableFormats[0].DisplayName);
            Assert.Equal("Referans Tablo 2", profile.TableFormats[1].DisplayName);
            Assert.Equal(new[] { "No", "Product Name", "Product Number", "NSN", "Serial No", "Quantity" }, profile.TableFormats[0].ReferenceHeaders);
            Assert.Equal(new[] { "No", "Product Name", "Product No", "NSN", "Serial No", "Quantity" }, profile.TableFormats[1].ReferenceHeaders);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroTable1_ExtractsUnequalColumnWeights()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            var weights = profile.TableFormats[0].Format.Columns.Select(column => column.WidthWeight).ToArray();
            Assert.Equal(6, weights.Length);
            AssertClose(5.1313d, weights[0], 0.02d);
            AssertClose(30.0375d, weights[1], 0.02d);
            AssertClose(15.4933d, weights[2], 0.02d);
            AssertClose(18.3293d, weights[3], 0.02d);
            AssertClose(21.0770d, weights[4], 0.02d);
            AssertClose(9.9316d, weights[5], 0.02d);
            AssertClose(100d, weights.Sum(), 0.001d);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroTable1_ExtractsPerColumnAlignment()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            var columns = profile.TableFormats[0].Format.Columns;
            Assert.Equal(ParagraphAlignment.Center, columns[0].BodyAlignment);
            Assert.Equal(ParagraphAlignment.Left, columns[1].BodyAlignment);
            Assert.All(columns.Skip(2), column => Assert.Equal(ParagraphAlignment.Center, column.BodyAlignment));
            Assert.Equal(9d, columns[0].BodyFontSizePoints, 3);
            Assert.False(columns[0].BodyBold);
            Assert.Equal(12d, columns[3].BodyFontSizePoints, 3);
            Assert.Equal(10d, columns[4].BodyFontSizePoints, 3);
            Assert.All(columns, column => Assert.Equal(VerticalContentAlignment.Center, column.VerticalAlignment));
            Assert.All(columns, column => Assert.True(column.NoWrap));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroTable1_ExtractsBorderCellMarginsAndRowHeight()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            var table = profile.TableFormats[0].Format;
            Assert.Equal(100d, table.WidthPercent, 3);
            Assert.True(table.FixedLayout);
            Assert.Equal(0.5d, table.BorderSizePoints, 3);
            Assert.InRange(table.CellMarginLeftMillimeters, 1.23d, 1.24d);
            Assert.InRange(table.CellMarginRightMillimeters, 1.23d, 1.24d);
            Assert.InRange(table.PreferredRowHeightMillimeters, 10.19d, 10.20d);
            Assert.True(table.RepeatHeader);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeroTable2_PreservesSeparateColumnRatios()
    {
        var (path, profile) = await ReadProfileAsync();
        try
        {
            var table = profile.TableFormats[1].Format;
            var weights = table.Columns.Select(column => column.WidthWeight).ToArray();
            Assert.InRange(table.WidthPercent, 99.30d, 99.34d);
            AssertClose(5.2111d, weights[0], 0.02d);
            AssertClose(28.3333d, weights[1], 0.02d);
            AssertClose(17.5444d, weights[2], 0.02d);
            AssertClose(17.5444d, weights[3], 0.02d);
            AssertClose(20.0222d, weights[4], 0.02d);
            AssertClose(11.3444d, weights[5], 0.02d);
            Assert.NotEqual(profile.TableFormats[0].Format.Columns[1].WidthWeight, weights[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Provider_OpensDocxReadOnly()
    {
        var path = Sprint16ReferenceFixture.CreateSeroLikeDocx();
        try
        {
            var beforeHash = SHA256.HashData(File.ReadAllBytes(path));
            var beforeWrite = File.GetLastWriteTimeUtc(path);
            var project = ProjectFor(path);
            var provider = new OpenXmlReferenceDocumentFormatProvider();

            var result = await provider.ReadAsync(project);

            Assert.NotNull(result.Profile);
            Assert.False(result.IsMissing);
            Assert.Equal(beforeHash, SHA256.HashData(File.ReadAllBytes(path)));
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ProductionDi_ReplacesBootstrapProviderWithoutDuplicateEffectiveRegistration()
    {
        var services = new ServiceCollection();
        services.AddWordStudioApplication();
        services.AddWordStudioInfrastructure();

        var providerDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IReferenceDocumentFormatProvider));
        Assert.Equal(typeof(OpenXmlReferenceDocumentFormatProvider), providerDescriptor.ImplementationType);

        var importDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IReferenceFormatDocumentService));
        Assert.Equal(typeof(OpenXmlReferenceFormatDocumentService), importDescriptor.ImplementationType);
    }

    [Fact]
    public void ReferenceFormatImportBoundary_UsesSingleApplicationContractAndRealOpenXmlImplementation()
    {
        var services = new ServiceCollection();
        services.AddWordStudioApplication();
        services.AddWordStudioInfrastructure();

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IReferenceFormatDocumentService));
        Assert.Equal(typeof(OpenXmlReferenceFormatDocumentService), descriptor.ImplementationType);
        Assert.Null(Type.GetType(
            "KKL.WordStudio.Infrastructure.ReferenceFormatting.IReferenceFormatDocumentService, KKL.WordStudio.Infrastructure",
            throwOnError: false));

        var invalidPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");
        File.WriteAllText(invalidPath, "not an Open XML package");
        try
        {
            var project = new Project();
            var service = Assert.IsAssignableFrom<IReferenceFormatDocumentService>(
                Activator.CreateInstance(descriptor.ImplementationType!));

            var result = service.Import(invalidPath);

            Assert.True(result.IsFailure);
            Assert.Null(project.ReferenceFormat);
            Assert.Contains("geçerli bir .docx", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(invalidPath);
        }
    }

    [Fact]
    public async Task ExactSeroProfile_ExtractsAuthoritativeSupportedProperties()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Sero.Reference.docx");
        Assert.True(
            File.Exists(path),
            "Exact Sero.docx regression fixture is required at TestData/Sero.Reference.docx; do not substitute a synthetic fixture.");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        Assert.Equal("8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20", hash);

        var result = await new OpenXmlReferenceDocumentFormatProvider().ReadAsync(ProjectFor(path));
        var profile = Assert.IsType<DocumentFormatProfile>(result.Profile);

        AssertClose(210.009d, profile.Page.WidthMillimeters, 0.02d);
        AssertClose(297.004d, profile.Page.HeightMillimeters, 0.02d);
        AssertClose(24.994d, profile.Page.MarginTopMillimeters, 0.02d);
        AssertClose(24.994d, profile.Page.MarginBottomMillimeters, 0.02d);
        AssertClose(24.994d, profile.Page.MarginLeftMillimeters, 0.02d);
        AssertClose(24.994d, profile.Page.MarginRightMillimeters, 0.02d);
        AssertClose(12.488d, profile.Page.HeaderDistanceMillimeters, 0.02d);
        AssertClose(12.488d, profile.Page.FooterDistanceMillimeters, 0.02d);

        Assert.Equal("Arial", profile.BodyText.FontFamilyName);
        AssertClose(10d, profile.BodyText.FontSizePoints, 0.1d);
        AssertClose(12d, profile.PrimaryHeading.FontSizePoints, 0.1d);
        Assert.True(profile.PrimaryHeading.Bold);
        Assert.True(profile.PrimaryHeading.Italic);
        Assert.True(profile.PrimaryHeading.KeepWithNext);

        Assert.Equal("Arial", profile.TableCaption.FontFamilyName);
        Assert.Equal(ParagraphAlignment.Center, profile.TableCaption.Alignment);
        Assert.True(profile.TableCaption.Bold);
        var sequence = Assert.IsType<TableCaptionSequenceProfile>(profile.TableCaptionSequence);
        Assert.Equal("Tablo", sequence.SequenceIdentifier);
        Assert.False(string.IsNullOrWhiteSpace(sequence.DisplayLabel));
        Assert.False(string.IsNullOrEmpty(sequence.Separator));

        Assert.Equal(2, profile.TableFormats.Count);
        var table1 = profile.TableFormats[0];
        var table2 = profile.TableFormats[1];
        Assert.NotEqual(table1.Key, table2.Key);
        Assert.Equal(6, table1.Format.Columns.Count);
        AssertClose(100d, table1.Format.WidthPercent, 0.05d);
        Assert.True(table1.Format.FixedLayout);
        AssertClose(0.5d, table1.Format.BorderSizePoints, 0.05d);
        AssertClose(1.235d, table1.Format.CellMarginLeftMillimeters, 0.02d);
        AssertClose(1.235d, table1.Format.CellMarginRightMillimeters, 0.02d);
        AssertClose(10.195d, table1.Format.PreferredRowHeightMillimeters, 0.02d);
        Assert.True(table1.Format.RepeatHeader);
        var table1Weights = table1.Format.Columns.Select(column => column.WidthWeight).ToArray();
        foreach (var pair in new[] { (5.13d, 0), (30.04d, 1), (15.49d, 2), (18.33d, 3), (21.08d, 4), (9.93d, 5) })
            AssertClose(pair.Item1, table1Weights[pair.Item2], 0.05d);
        Assert.Equal(ParagraphAlignment.Left, table1.Format.Columns[1].BodyAlignment);
        Assert.All(new[] { 0, 2, 3, 4, 5 }, index =>
            Assert.Equal(ParagraphAlignment.Center, table1.Format.Columns[index].BodyAlignment));

        Assert.Equal(6, table2.Format.Columns.Count);
        AssertClose(99.32d, table2.Format.WidthPercent, 0.05d);
        var table2Weights = table2.Format.Columns.Select(column => column.WidthWeight).ToArray();
        foreach (var pair in new[] { (5.21d, 0), (28.33d, 1), (17.54d, 2), (17.54d, 3), (20.02d, 4), (11.34d, 5) })
            AssertClose(pair.Item1, table2Weights[pair.Item2], 0.05d);
    }

    [Fact]
    public async Task MissingReferenceAsset_ReturnsFriendlyMissingState()
    {
        var project = new Project { Name = "Missing Reference" };
        project.ReferenceFormat = new ReferenceFormatDocument
        {
            FileName = "Kayip-Bicim.docx",
            OriginalSourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Kayip-Bicim.docx")
        };
        var provider = new OpenXmlReferenceDocumentFormatProvider();

        var result = await provider.ReadAsync(project);

        Assert.Null(result.Profile);
        Assert.True(result.IsMissing);
        Assert.Contains("Biçim şablonu bulunamadı", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kayip-Bicim.docx", result.StatusMessage, StringComparison.Ordinal);
    }

    private static async Task<(string Path, DocumentFormatProfile Profile)> ReadProfileAsync()
    {
        var path = Sprint16ReferenceFixture.CreateSeroLikeDocx();
        var provider = new OpenXmlReferenceDocumentFormatProvider();
        var result = await provider.ReadAsync(ProjectFor(path));
        Assert.NotNull(result.Profile);
        Assert.False(result.IsMissing);
        return (path, result.Profile!);
    }

    private static Project ProjectFor(string path)
    {
        var project = new Project { Name = "Sero Fixture" };
        project.ReferenceFormat = new ReferenceFormatDocument
        {
            FileName = "Sero.docx",
            OriginalSourcePath = path,
            ResolvedFilePath = path
        };
        return project;
    }

    private static void AssertClose(double expected, double actual, double tolerance) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
}

internal static class Sprint16ReferenceFixture
{
    internal static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static string CreateSeroLikeDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");
        using var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = package.AddMainDocumentPart();
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        SaveXml(stylesPart, BuildStyles());
        SaveXml(mainPart, BuildDocument());
        return path;
    }

    public static string CreateSimpleDocx(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");
        using var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = package.AddMainDocumentPart();
        SaveXml(mainPart, new XDocument(
            new XElement(W + "document",
                new XAttribute(XNamespace.Xmlns + "w", W),
                new XElement(W + "body", Paragraph(text), SectionProperties()))));
        return path;
    }

    public static XDocument LoadXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static XDocument BuildStyles() => new(
        new XElement(W + "styles",
            new XAttribute(XNamespace.Xmlns + "w", W),
            new XElement(W + "docDefaults",
                new XElement(W + "rPrDefault",
                    new XElement(W + "rPr",
                        RunFonts("Arial"),
                        new XElement(W + "sz", WAttr("val", "20")))),
                new XElement(W + "pPrDefault",
                    new XElement(W + "pPr",
                        new XElement(W + "spacing", WAttr("after", "0"), WAttr("line", "240"), WAttr("lineRule", "auto"))))),
            Style("Normal", "Normal", null,
                new XElement(W + "pPr",
                    new XElement(W + "jc", WAttr("val", "left")),
                    new XElement(W + "spacing", WAttr("after", "0"), WAttr("line", "240"), WAttr("lineRule", "auto"))),
                new XElement(W + "rPr", RunFonts("Arial"), new XElement(W + "sz", WAttr("val", "20")))),
            Style("Balk4", "Başlık 4", "Normal",
                new XElement(W + "pPr",
                    new XElement(W + "keepNext"),
                    new XElement(W + "spacing", WAttr("before", "80"), WAttr("after", "40"), WAttr("line", "240"), WAttr("lineRule", "auto"))),
                new XElement(W + "rPr", new XElement(W + "i"))),
            Style("Balk5", "Başlık 5", "Normal",
                new XElement(W + "pPr",
                    new XElement(W + "keepNext"),
                    new XElement(W + "spacing", WAttr("before", "80"), WAttr("after", "40"), WAttr("line", "240"), WAttr("lineRule", "auto"))),
                new XElement(W + "rPr", new XElement(W + "b"), new XElement(W + "sz", WAttr("val", "24")))),
            Style("ResimYazs", "Caption", "Normal",
                new XElement(W + "pPr",
                    new XElement(W + "keepNext"),
                    new XElement(W + "jc", WAttr("val", "center")),
                    new XElement(W + "spacing", WAttr("after", "0"), WAttr("line", "480"), WAttr("lineRule", "auto")),
                    new XElement(W + "ind", WAttr("firstLine", "708"))),
                new XElement(W + "rPr", RunFonts("Arial"), new XElement(W + "sz", WAttr("val", "16")), new XElement(W + "b"))),
            Style("TabloMetni", "Tablo Metni", "Normal", null,
                new XElement(W + "rPr", RunFonts("Arial"), new XElement(W + "sz", WAttr("val", "24"))))));

    private static XDocument BuildDocument()
    {
        var body = new XElement(W + "body");
        body.Add(Paragraph(
            "System Component Configuration List",
            ParagraphProperties("Balk4"),
            RunProperties(24, true, false, "Arial", "000000")));
        body.Add(Paragraph(
            "System Component Configuration List",
            new XElement(W + "pPr",
                new XElement(W + "pStyle", WAttr("val", "Balk5")),
                new XElement(W + "ind", WAttr("left", "1233")))));
        body.Add(Paragraph(
            "UAV Tail Number: x",
            new XElement(W + "pPr",
                new XElement(W + "pStyle", WAttr("val", "Normal")),
                new XElement(W + "jc", WAttr("val", "center"))),
            RunProperties(22, true, false, "Arial")));
        body.Add(Caption("Table", "Unmanned Aerial Vehicle", 16));
        body.Add(BuildTable(
            5000,
            new[] { 465, 2722, 1404, 1661, 1910, 900 },
            new[] { "No", "Product Name", "Product Number", "NSN", "Serial No", "Quantity" },
            tableTwo: false));
        body.Add(Paragraph(
            "System Component Configuration List - Generator & Trailer",
            ParagraphProperties("Balk5")));
        body.Add(Caption("Tablo", "Generator & Trailer Configuration List", 24));
        body.Add(BuildTable(
            4966,
            new[] { 469, 2550, 1579, 1579, 1802, 1021 },
            new[] { "No", "Product Name", "Product No", "NSN", "Serial No", "Quantity" },
            tableTwo: true));
        body.Add(SectionProperties());

        return new XDocument(
            new XElement(W + "document",
                new XAttribute(XNamespace.Xmlns + "w", W),
                body));
    }

    private static XElement BuildTable(int widthPercentFiftieths, int[] gridWidths, string[] headers, bool tableTwo)
    {
        var table = new XElement(W + "tbl",
            new XElement(W + "tblPr",
                new XElement(W + "tblW", WAttr("w", widthPercentFiftieths.ToString()), WAttr("type", "pct")),
                new XElement(W + "tblLayout", WAttr("type", "fixed")),
                new XElement(W + "tblBorders",
                    Border("top"), Border("left"), Border("bottom"), Border("right"), Border("insideH"), Border("insideV")),
                new XElement(W + "tblCellMar",
                    Margin("top", 0), Margin("bottom", 0), Margin("left", 70), Margin("right", 70))),
            new XElement(W + "tblGrid", gridWidths.Select(width => new XElement(W + "gridCol", WAttr("w", width.ToString())))));

        var headerFontSize = tableTwo ? 24 : 20;
        table.Add(new XElement(W + "tr",
            new XElement(W + "trPr",
                new XElement(W + "tblHeader"),
                new XElement(W + "trHeight", WAttr("val", "578"), WAttr("hRule", "atLeast"))),
            headers.Select((header, index) => Cell(
                header,
                index == 1 ? "left" : "center",
                tableTwo && index == 0 ? 24 : tableTwo ? 24 : index == 0 ? 18 : headerFontSize,
                true,
                styleId: null,
                noWrap: false,
                vMerge: null))));

        for (var rowIndex = 1; rowIndex <= 4; rowIndex++)
        {
            table.Add(new XElement(W + "tr",
                new XElement(W + "trPr", new XElement(W + "trHeight", WAttr("val", "578"), WAttr("hRule", "atLeast"))),
                Cell(rowIndex.ToString(), "center", tableTwo ? 24 : 18, tableTwo || rowIndex == 1, null, true, tableTwo ? rowIndex == 1 ? "restart" : rowIndex == 2 ? "continue" : null : null),
                Cell($"Product {rowIndex}", "left", 20, false, null, true, tableTwo ? rowIndex == 1 ? "restart" : rowIndex == 2 ? "continue" : null : null),
                Cell($"PN-{rowIndex}", "center", 20, false, null, true, tableTwo ? rowIndex == 1 ? "restart" : rowIndex == 2 ? "continue" : null : null),
                Cell($"NSN-{rowIndex}", "center", 24, false, "TabloMetni", true, tableTwo ? rowIndex == 1 ? "restart" : rowIndex == 2 ? "continue" : null : null),
                Cell($"SER-{rowIndex}", "center", rowIndex == 2 && !tableTwo ? 24 : 20, false, rowIndex == 2 && !tableTwo ? "TabloMetni" : "Normal", true, null),
                Cell(tableTwo && rowIndex == 2 ? string.Empty : "1", "center", 20, false, null, true, tableTwo ? rowIndex == 1 ? "restart" : rowIndex == 2 ? "continue" : null : null)));
        }

        return table;
    }

    private static XElement Caption(string displayLabel, string description, int directFontHalfPoints) =>
        new(W + "p",
            ParagraphProperties("ResimYazs"),
            new XElement(W + "r",
                directFontHalfPoints == 16 ? null : RunProperties(directFontHalfPoints, true, false, "Arial"),
                new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), displayLabel + " ")),
            new XElement(W + "fldSimple",
                WAttr("instr", " SEQ Tablo \\* ARABIC "),
                new XElement(W + "r", new XElement(W + "t", displayLabel == "Table" ? "1" : "2"))),
            new XElement(W + "r",
                directFontHalfPoints == 16 ? null : RunProperties(directFontHalfPoints, true, false, "Arial"),
                new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), ". " + description)));

    private static XElement Cell(
        string text,
        string alignment,
        int fontHalfPoints,
        bool bold,
        string? styleId,
        bool noWrap,
        string? vMerge)
    {
        var tcPr = new XElement(W + "tcPr",
            new XElement(W + "vAlign", WAttr("val", "center")));
        if (noWrap)
            tcPr.Add(new XElement(W + "noWrap"));
        if (vMerge is not null)
            tcPr.Add(new XElement(W + "vMerge", WAttr("val", vMerge)));

        var pPr = new XElement(W + "pPr", new XElement(W + "jc", WAttr("val", alignment)));
        if (!string.IsNullOrWhiteSpace(styleId))
            pPr.AddFirst(new XElement(W + "pStyle", WAttr("val", styleId)));

        return new XElement(W + "tc",
            tcPr,
            new XElement(W + "p",
                pPr,
                new XElement(W + "r",
                    RunProperties(fontHalfPoints, bold, false, "Arial"),
                    new XElement(W + "t", text))));
    }

    private static XElement Paragraph(
        string text,
        XElement? paragraphProperties = null,
        XElement? runProperties = null) =>
        new(W + "p",
            paragraphProperties,
            new XElement(W + "r", runProperties, new XElement(W + "t", text)));

    private static XElement ParagraphProperties(string styleId) =>
        new(W + "pPr", new XElement(W + "pStyle", WAttr("val", styleId)));

    private static XElement RunProperties(
        int fontSizeHalfPoints,
        bool bold,
        bool italic,
        string fontFamily,
        string? color = null)
    {
        var properties = new XElement(W + "rPr",
            RunFonts(fontFamily),
            new XElement(W + "sz", WAttr("val", fontSizeHalfPoints.ToString())));
        if (bold)
            properties.Add(new XElement(W + "b"));
        if (italic)
            properties.Add(new XElement(W + "i"));
        if (color is not null)
            properties.Add(new XElement(W + "color", WAttr("val", color)));
        return properties;
    }

    private static XElement RunFonts(string family) =>
        new(W + "rFonts", WAttr("ascii", family), WAttr("hAnsi", family));

    private static XElement Style(string id, string name, string? basedOn, XElement? pPr, XElement? rPr)
    {
        var style = new XElement(W + "style", WAttr("type", "paragraph"), WAttr("styleId", id),
            new XElement(W + "name", WAttr("val", name)));
        if (basedOn is not null)
            style.Add(new XElement(W + "basedOn", WAttr("val", basedOn)));
        if (pPr is not null)
            style.Add(pPr);
        if (rPr is not null)
            style.Add(rPr);
        return style;
    }

    private static XElement Border(string name) =>
        new(W + name, WAttr("val", "single"), WAttr("sz", "4"), WAttr("color", "000000"));

    private static XElement Margin(string name, int twips) =>
        new(W + name, WAttr("w", twips.ToString()), WAttr("type", "dxa"));

    private static XElement SectionProperties() =>
        new(W + "sectPr",
            new XElement(W + "pgSz", WAttr("w", "11906"), WAttr("h", "16838")),
            new XElement(W + "pgMar",
                WAttr("top", "1417"), WAttr("bottom", "1417"), WAttr("left", "1417"), WAttr("right", "1417"),
                WAttr("header", "708"), WAttr("footer", "708")));

    private static XAttribute WAttr(string localName, string value) => new(W + localName, value);

    private static void SaveXml(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        document.Save(stream, SaveOptions.DisableFormatting);
    }
}
