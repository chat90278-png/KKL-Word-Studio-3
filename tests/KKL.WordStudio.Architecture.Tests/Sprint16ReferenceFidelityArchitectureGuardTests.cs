namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using Xunit;

public sealed class Sprint16ReferenceFidelityArchitectureGuardTests
{
    [Fact]
    public void ReferenceFormatAndFrontMatter_RemainDistinctProjectOwnedAssets()
    {
        var frontMatter = typeof(Project).GetProperty(nameof(Project.FrontMatter));
        var referenceFormat = typeof(Project).GetProperty(nameof(Project.ReferenceFormat));

        Assert.NotNull(frontMatter);
        Assert.NotNull(referenceFormat);
        Assert.NotEqual(frontMatter!.PropertyType, referenceFormat!.PropertyType);
        Assert.Equal(typeof(ReferenceFormatDocument), referenceFormat.PropertyType);
        var referenceDocument = new ReferenceFormatDocument
        {
            FileName = "reference.docx"
        };

        Assert.Equal(
            "resources/reference-format/reference-format.docx",
            referenceDocument.EmbeddedAssetEntryName);
        Assert.DoesNotContain(
            "front",
            referenceDocument.EmbeddedAssetEntryName,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationEngineAndUI_DoNotParseReferenceDocx()
    {
        var root = SolutionRootLocator.Find();
        var files = SourceScan.ReadCodeFiles(root, "src", ".cs", ".xaml")
            .Where(file =>
                file.RelativePath.StartsWith("src/KKL.WordStudio.Application/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.Engine/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/", StringComparison.Ordinal))
            .ToList();

        var offenders = SourceScan.FindMatches(
            files,
            @"\bWordprocessingDocument\b|\bDocumentFormat\.OpenXml\b|\bAlternativeFormatImportPart\b",
            RegexOptions.CultureInvariant);

        Assert.True(
            offenders.Count == 0,
            "Reference DOCX parsing belongs behind IReferenceDocumentFormatProvider in Infrastructure. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void ReportContentBuilder_UsesReferenceProviderAndResolverAfterSemanticComposition()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.Application", "Content", "ReportContentBuilder.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Contains("IReferenceDocumentFormatProvider", source, StringComparison.Ordinal);
        Assert.Contains("IReportContentFormatResolver", source, StringComparison.Ordinal);
        Assert.Contains("_referenceDocumentFormatProvider.ReadAsync", source, StringComparison.Ordinal);
        Assert.Contains("_reportContentFormatResolver.ResolveText", source, StringComparison.Ordinal);
        Assert.Contains("_reportContentFormatResolver.ResolveTable", source, StringComparison.Ordinal);
        Assert.Contains("_reportContentFormatResolver.ResolvePageLayout", source, StringComparison.Ordinal);

        var composedStart = source.IndexOf("private TableContentNode BuildComposedTableNode", StringComparison.Ordinal);
        var errorStart = source.IndexOf("private TableContentNode BuildMultiSourceErrorNode", composedStart, StringComparison.Ordinal);
        Assert.True(composedStart >= 0 && errorStart > composedStart, "BuildComposedTableNode segment could not be located.");
        var segment = source[composedStart..errorStart];
        var compose = segment.IndexOf("_tableContentRowComposer.Compose", StringComparison.Ordinal);
        var resolve = segment.IndexOf("_reportContentFormatResolver.ResolveTable", StringComparison.Ordinal);

        Assert.True(compose >= 0, "Semantic table composer call is missing.");
        Assert.True(resolve > compose, "Serial/Quantity composition must remain before table format resolution.");
    }

    [Fact]
    public void EngineUiAndWord_DoNotHardCodeSeroSpecificTitles()
    {
        var root = SolutionRootLocator.Find();
        var files = Sprint16ConsumerFiles(root);
        var patterns = new[]
        {
            "System Component Configuration List",
            "UAV Tail Number",
            "Unmanned Aerial Vehicle",
            "Generator & Trailer Configuration List"
        };

        var offenders = files
            .Where(file => patterns.Any(pattern => file.Text.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            .Select(file => file.RelativePath)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Engine/UI/Word must consume resolved formats and must not hard-code Sero titles. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void EngineUiAndWord_DoNotEmbedExactSeroColumnWidthArrays()
    {
        var root = SolutionRootLocator.Find();
        var files = Sprint16ConsumerFiles(root);
        var exactTable1Array = @"465\D{0,20}2722\D{0,20}1404\D{0,20}1661\D{0,20}1910\D{0,20}900";
        var exactTable2Array = @"469\D{0,20}2550\D{0,20}1579\D{0,20}1579\D{0,20}1802\D{0,20}1021";
        var offenders = files
            .Where(file =>
                Regex.IsMatch(file.Text, exactTable1Array, RegexOptions.CultureInvariant)
                || Regex.IsMatch(file.Text, exactTable2Array, RegexOptions.CultureInvariant))
            .Select(file => file.RelativePath)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Exact Sero grid arrays belong in the resolved reference profile, not Engine/UI/Word source. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void WordWriters_ConsumeResolvedFormatsAndNeverReadReferenceAssetPaths()
    {
        var root = SolutionRootLocator.Find();
        var wordFiles = SourceScan.ReadCodeFiles(
            root,
            "src/KKL.WordStudio.Infrastructure/Export/Exporters/Word",
            ".cs");
        var source = string.Join("\n", wordFiles.Select(file => file.Text));

        Assert.Contains("text.Format", source, StringComparison.Ordinal);
        Assert.Contains("tableNode.Format", source, StringComparison.Ordinal);
        Assert.Contains("HeaderDistanceMillimeters", source, StringComparison.Ordinal);
        Assert.Contains("FooterDistanceMillimeters", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferenceFormat", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OriginalSourcePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolvedFilePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument.Open", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SerialQuantityGrouping_StillUsesStableGuidColumnIdentities()
    {
        Assert.Equal(typeof(Guid), typeof(SerialQuantityGrouping).GetProperty(nameof(SerialQuantityGrouping.MatchKeyColumnId))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(SerialQuantityGrouping).GetProperty(nameof(SerialQuantityGrouping.SerialNumberColumnId))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(SerialQuantityGrouping).GetProperty(nameof(SerialQuantityGrouping.QuantityColumnId))!.PropertyType);
    }

    [Fact]
    public void NoDocxToReportElementReverseEngineeringTypesWereAdded()
    {
        var root = SolutionRootLocator.Find();
        var files = SourceScan.ReadCodeFiles(root, "src", ".cs");
        var offenders = SourceScan.FindMatches(
            files,
            @"\bIDocumentStructureAnalyzer\b|\bDocumentImportProposal\b|\bDocxToReportElement\b|\bReferenceDocumentStructureAnalyzer\b",
            RegexOptions.CultureInvariant);

        Assert.True(
            offenders.Count == 0,
            "DOCX -> ReportElement reverse engineering is out of Sprint 16 scope. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void LoadedSourcesSelector_RemainsSeparateFromKaynakVeriSurface()
    {
        var root = SolutionRootLocator.Find();
        var excelWorkspacePath = Path.Combine(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.xaml");
        var loadedSourcesPath = Path.Combine(root, "src", "KKL.WordStudio.UI", "Views", "LoadedSourcesView.xaml");
        var excelWorkspace = SourceScan.ReadWithoutComments(excelWorkspacePath);
        var loadedSources = SourceScan.ReadWithoutComments(loadedSourcesPath);

        Assert.Contains("KAYNAK VERİ", excelWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("Yüklenen Kaynaklar", excelWorkspace, StringComparison.Ordinal);
        Assert.Contains("Yüklenen Kaynaklar", loadedSources, StringComparison.Ordinal);
        Assert.DoesNotContain("KAYNAK VERİ", loadedSources, StringComparison.Ordinal);
    }

    [Fact]
    public void Sprint16Scope_DoesNotIntroduceInteropWebViewOrNewPdfImplementation()
    {
        var root = SolutionRootLocator.Find();
        var projectFiles = SourceScan.ReadCodeFiles(root, "src", ".cs", ".xaml", ".csproj");
        var interopOrWebView = SourceScan.FindMatches(
            projectFiles,
            @"Microsoft\.Office\.Interop|\bWebView2?\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.True(
            interopOrWebView.Count == 0,
            "Office Interop/WebView are prohibited. Offenders: " + string.Join(", ", interopOrWebView));

        var pdfPath = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "PdfExporter.cs");
        var pdfSource = SourceScan.ReadWithoutComments(pdfPath);
        Assert.Contains("PDF export is not yet implemented.", pdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestPDF", string.Join("\n", projectFiles.Select(file => file.Text)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PdfSharp", string.Join("\n", projectFiles.Select(file => file.Text)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WordCaptionFormattingGap_IsEitherTransportedOrExplicitlyRequested()
    {
        var root = SolutionRootLocator.Find();
        var requestPath = Path.Combine(root, "docs", "CONTRACT_CHANGE_REQUEST-D.md");
        var captionFormat = typeof(KKL.WordStudio.Application.Content.TableContentNode).GetProperty("CaptionFormat");

        if (captionFormat is null)
        {
            Assert.True(File.Exists(requestPath), "The missing caption-format transport requires CONTRACT_CHANGE_REQUEST-D.md.");
            return;
        }

        Assert.Equal(typeof(KKL.WordStudio.Application.Formatting.ResolvedTextFormat), captionFormat.PropertyType);
    }

    [Fact]
    public void ReferenceFormatImportBoundary_IsSingleAndPreviewUsesInjectedService()
    {
        var root = SolutionRootLocator.Find();
        var applicationInterface = Path.Combine(
            root, "src", "KKL.WordStudio.Application", "Formatting", "IReferenceFormatDocumentService.cs");
        var infrastructureInterface = Path.Combine(
            root, "src", "KKL.WordStudio.Infrastructure", "ReferenceFormatting", "IReferenceFormatDocumentService.cs");
        var simpleApplicationService = Path.Combine(
            root, "src", "KKL.WordStudio.Application", "Formatting", "ReferenceFormatDocumentService.cs");
        var openXmlServicePath = Path.Combine(
            root, "src", "KKL.WordStudio.Infrastructure", "ReferenceFormatting", "OpenXmlReferenceFormatDocumentService.cs");
        var previewPath = Path.Combine(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewViewModel.cs");
        var infrastructureDiPath = Path.Combine(
            root, "src", "KKL.WordStudio.Infrastructure", "DependencyInjection", "InfrastructureServiceCollectionExtensions.cs");

        Assert.True(File.Exists(applicationInterface));
        Assert.False(File.Exists(infrastructureInterface));
        Assert.False(File.Exists(simpleApplicationService));

        var openXmlService = SourceScan.ReadWithoutComments(openXmlServicePath);
        var preview = SourceScan.ReadWithoutComments(previewPath);
        var infrastructureDi = SourceScan.ReadWithoutComments(infrastructureDiPath);
        Assert.Contains("using KKL.WordStudio.Application.Formatting;", openXmlService, StringComparison.Ordinal);
        Assert.Contains(": IReferenceFormatDocumentService", openXmlService, StringComparison.Ordinal);
        Assert.Contains("IReferenceFormatDocumentService referenceFormatService", preview, StringComparison.Ordinal);
        Assert.Contains("_referenceFormatService = referenceFormatService;", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("new ReferenceFormatDocumentService(", preview, StringComparison.Ordinal);

        var uiFiles = SourceScan.ReadCodeFiles(root, "src/KKL.WordStudio.UI", ".cs", ".xaml");
        Assert.DoesNotContain(uiFiles, file =>
            file.Text.Contains("KKL.WordStudio.Infrastructure.ReferenceFormatting", StringComparison.Ordinal));
        Assert.Contains("using KKL.WordStudio.Application.Formatting;", infrastructureDi, StringComparison.Ordinal);
        Assert.Contains(
            "AddSingleton<IReferenceFormatDocumentService, OpenXmlReferenceFormatDocumentService>()",
            infrastructureDi,
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<(string RelativePath, string Text)> Sprint16ConsumerFiles(string root) =>
        SourceScan.ReadCodeFiles(root, "src", ".cs", ".xaml")
            .Where(file =>
                file.RelativePath.StartsWith("src/KKL.WordStudio.Engine/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/", StringComparison.Ordinal)
                || file.RelativePath.EndsWith("/Export/Exporters/WordExporter.cs", StringComparison.Ordinal))
            .ToList();
}
