namespace KKL.WordStudio.Architecture.Tests;

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using Xunit;

public sealed class StabilizationMarkerTests
{
    [Fact]
    public void UiProject_RetainsSerilogExtensionsHosting()
    {
        var root = SolutionRootLocator.Find();
        var projectPath = Path.Combine(root, "src", "KKL.WordStudio.UI", "KKL.WordStudio.UI.csproj");
        var packages = XDocument.Load(projectPath)
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty);

        Assert.Contains("Serilog.Extensions.Hosting", packages);
    }

    [Fact]
    public void Persistence_RetainsPopulateObjectCreationHandling()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Persistence", "KwsProjectRepository.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Matches(
            new Regex(@"PreferredObjectCreationHandling\s*=\s*(?:System\.Text\.Json\.Serialization\.)?JsonObjectCreationHandling\.Populate", RegexOptions.CultureInvariant),
            source);
    }

    [Fact]
    public void DomainPersistenceMarkers_RetainHistoricalFixes()
    {
        var rangeReference = typeof(DataRange).GetProperty(nameof(DataRange.RangeReference));
        Assert.NotNull(rangeReference);
        Assert.True(rangeReference!.IsDefined(typeof(JsonIgnoreAttribute), inherit: false), "DataRange.RangeReference must remain [JsonIgnore].");

        AssertProperty<Binding>(nameof(Binding.WorksheetName), typeof(string));
        var header = AssertProperty<TableColumn>(nameof(TableColumn.Header), typeof(string));
        var sourceField = AssertProperty<TableColumn>(nameof(TableColumn.SourceField), typeof(string));
        Assert.NotSame(header, sourceField);

        AssertProperty<TableElement>(nameof(TableElement.Sources), typeof(List<TableSourceBinding>));
        AssertProperty<Worksheet>(nameof(Worksheet.WorkingData), typeof(WorksheetWorkingData));
        AssertProperty<Worksheet>(nameof(Worksheet.ColumnMappings), typeof(List<ColumnMapping>));
    }

    [Fact]
    public void SourceExcelOpenXmlPaths_DoNotOpenPackagesForEditing()
    {
        var root = SolutionRootLocator.Find();
        var excelFiles = SourceScan.ReadCodeFiles(root, "src/KKL.WordStudio.Infrastructure/Excel", ".cs");
        var offenders = SourceScan.FindMatches(
            excelFiles,
            @"\bSpreadsheetDocument\s*\.\s*Open\s*\([^,]+,\s*true\s*\)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        Assert.True(
            offenders.Count == 0,
            $"Source Excel OpenXML packages must not be intentionally opened with edit=true. Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void ExistingFrontMatterReader_OpensSourceDocxReadOnly()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Word", "OpenXmlFrontMatterDocumentService.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Matches(
            new Regex(@"WordprocessingDocument\s*\.\s*Open\s*\(\s*filePath\s*,\s*false\s*\)", RegexOptions.CultureInvariant),
            source);
        Assert.DoesNotMatch(
            new Regex(@"WordprocessingDocument\s*\.\s*Open\s*\([^,]+,\s*true\s*\)", RegexOptions.Singleline | RegexOptions.CultureInvariant),
            source);
    }

    [Fact]
    public void WordWriters_RetainExplicitStylesHeaderAndFooterSaveCalls()
    {
        var root = SolutionRootLocator.Find();
        var stylePath = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordStyleWriter.cs");
        var headerFooterPath = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordHeaderFooterWriter.cs");
        var styleSource = SourceScan.ReadWithoutComments(stylePath);
        var headerFooterSource = SourceScan.ReadWithoutComments(headerFooterPath);

        Assert.Contains("styles.Save(stylesPart);", styleSource, StringComparison.Ordinal);
        Assert.Contains("header.Save(headerPart);", headerFooterSource, StringComparison.Ordinal);
        Assert.Contains("footer.Save(footerPart);", headerFooterSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewRefresh_RemainsScopedToReportContentChanged()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewViewModel.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Matches(
            new Regex(@"ReportContentChanged\s*\+=\s*[^;]*(RefreshAsync|RenderAsync)", RegexOptions.CultureInvariant),
            source);
        Assert.DoesNotMatch(
            new Regex(@"WorkspaceChanged\s*\+=\s*[^;]*(RefreshAsync|RenderAsync)", RegexOptions.CultureInvariant),
            source);
    }

    [Fact]
    public void ReportStructure_RemainsRootChildrenBasedWithoutParentIdState()
    {
        var root = SolutionRootLocator.Find();
        var structurePath = Path.Combine(root, "src", "KKL.WordStudio.Application", "Structure", "ReportStructureService.cs");
        var structureSource = SourceScan.ReadWithoutComments(structurePath);

        Assert.Matches(new Regex(@"section\s*\.\s*Root\s*\.\s*Children", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), structureSource);
        Assert.Null(typeof(ReportElement).GetProperty("ParentId"));
    }

    private static System.Reflection.PropertyInfo AssertProperty<T>(string propertyName, Type propertyType)
    {
        var property = typeof(T).GetProperty(propertyName);
        Assert.True(property is not null, $"Historical contract marker missing: {typeof(T).FullName}.{propertyName}.");
        Assert.Equal(propertyType, property!.PropertyType);
        return property;
    }
}
