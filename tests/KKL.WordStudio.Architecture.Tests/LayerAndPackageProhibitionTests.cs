namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

public sealed class LayerAndPackageProhibitionTests
{
    [Theory]
    [InlineData(@"\bSystem\.Windows\b", "WPF/System.Windows")]
    [InlineData(@"\bDocumentFormat\.OpenXml\b", "DocumentFormat.OpenXml")]
    [InlineData(@"\bWordprocessingDocument\b", "WordprocessingDocument")]
    [InlineData(@"\bIDataProvider\b|\bExcelDataProvider\b", "Excel data-provider access")]
    [InlineData(@"\.\s*(Binding|Sources)\b", "TableElement Binding/Sources semantic access")]
    [InlineData(@"\bSpreadsheetDocument\s*\.\s*Open\s*\(", "SpreadsheetDocument.Open")]
    public void EngineSource_DoesNotCrossExecutionOrRenderingBoundaries(string pattern, string prohibitedCapability)
    {
        var root = SolutionRootLocator.Find();
        var engineFiles = SourceScan.ReadCodeFiles(root, "src/KKL.WordStudio.Engine", ".cs");
        var offenders = SourceScan.FindMatches(engineFiles, pattern, RegexOptions.IgnoreCase);

        Assert.True(
            offenders.Count == 0,
            $"Engine must consume ReportContentDocument and own layout only; prohibited {prohibitedCapability} found in: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void ProductionCode_DoesNotIntroduceInteropOrWebView()
    {
        var root = SolutionRootLocator.Find();
        var productionFiles = SourceScan.ReadCodeFiles(root, "src", ".cs", ".xaml", ".csproj");

        var interop = SourceScan.FindMatches(productionFiles, @"\bMicrosoft\.Office\.Interop\b|\bOffice\.Interop\b", RegexOptions.IgnoreCase);
        var webView = SourceScan.FindMatches(productionFiles, @"\bWebView2?\b|<\s*WebView2?\b", RegexOptions.IgnoreCase);

        Assert.True(interop.Count == 0, $"Office COM/Interop is prohibited. Offenders: {string.Join(", ", interop)}");
        Assert.True(webView.Count == 0, $"WebView controls/packages are prohibited. Offenders: {string.Join(", ", webView)}");
    }

    [Fact]
    public void ProductionProjects_DoNotAddPdfImplementationPackages()
    {
        var root = SolutionRootLocator.Find();
        var packages = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference")
                .Select(reference => new
                {
                    Project = Path.GetRelativePath(root, path),
                    Package = (string?)reference.Attribute("Include") ?? string.Empty
                }))
            .Where(item => Regex.IsMatch(item.Package, @"pdf|itext|questpdf", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .ToList();

        Assert.True(
            packages.Count == 0,
            $"Sprint 14 must not add PDF implementation work/packages. Offenders: {string.Join(", ", packages.Select(p => $"{p.Project}:{p.Package}"))}");

        var pdfExporterPath = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "PdfExporter.cs");
        var pdfExporter = SourceScan.ReadWithoutComments(pdfExporterPath);
        Assert.Contains("PDF export is not yet implemented.", pdfExporter, StringComparison.Ordinal);
    }
}
