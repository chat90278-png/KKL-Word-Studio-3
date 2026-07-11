namespace KKL.WordStudio.Engine.Tests;

using Xunit;

public sealed class Sprint16PreviewFormatStaticTests
{
    [Fact]
    public void PreviewText_ConsumesResolvedTextFormat()
    {
        var projection = ReadSource("src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs");
        var xaml = ReadSource("src/KKL.WordStudio.UI/Views/PreviewView.xaml");

        Assert.Contains("text.Format.FontFamilyName", projection, StringComparison.Ordinal);
        Assert.Contains("text.Format.FontSizePoints", projection, StringComparison.Ordinal);
        Assert.Contains("text.Format.ForegroundColor", projection, StringComparison.Ordinal);
        Assert.Contains("text.SemanticKind is null ? text.Alignment : text.Format.Alignment", projection, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{Binding FontFamily}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{Binding Foreground}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTable_ConsumesResolvedTableFormat()
    {
        var projection = ReadSource("src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs");
        var control = ReadSource("src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs");

        Assert.Contains("Format = table.Format", projection, StringComparison.Ordinal);
        Assert.Contains("FormatProperty", control, StringComparison.Ordinal);
        Assert.Contains("ResolvedTableFormat", control, StringComparison.Ordinal);
        Assert.Contains("Format ?? DefaultFormatProfiles.Table", control, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTable_UsesColumnWidthWeights()
    {
        var control = ReadSource("src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs");

        Assert.Contains("columnFormat.WidthWeight", control, StringComparison.Ordinal);
        Assert.Contains("new GridLength(widthWeight, GridUnitType.Star)", control, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTable_UsesPreferredRowHeight()
    {
        var control = ReadSource("src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs");

        Assert.Contains("format.PreferredRowHeightMillimeters", control, StringComparison.Ordinal);
        Assert.Contains("MinHeight = preferredRowHeight", control, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTable_UsesPerColumnAlignment()
    {
        var control = ReadSource("src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs");
        var projection = ReadSource("src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs");

        Assert.Contains("column.BodyAlignment", control, StringComparison.Ordinal);
        Assert.Contains("column.HeaderAlignment", projection, StringComparison.Ordinal);
        Assert.Contains("HeaderAlignment = ProjectAlignment", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTable_PreservesGridRowSpan()
    {
        var control = ReadSource("src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs");

        Assert.Contains("coveredCells.Contains", control, StringComparison.Ordinal);
        Assert.Contains("SetRowSpan(cell, rowSpan)", control, StringComparison.Ordinal);
    }

    [Fact]
    public void EditableHeader_UsesReferenceColumnWidths()
    {
        var xaml = ReadSource("src/KKL.WordStudio.UI/Views/PreviewView.xaml");
        var panel = ReadSource("src/KKL.WordStudio.UI/Preview/PreviewTableColumnsPanel.cs");

        Assert.Contains("PreviewTableColumnsPanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<UniformGrid Rows=\"1\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("Format.Columns[index].WidthWeight", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewCaption_ConsumesResolvedCaptionFormat()
    {
        var payload = ReadSource("src/KKL.WordStudio.Application/Layout/DocumentLayoutContracts.cs");
        var projection = ReadSource("src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs");
        var viewModel = ReadSource("src/KKL.WordStudio.UI/ViewModels/PreviewPageViewModel.cs");
        var xaml = ReadSource("src/KKL.WordStudio.UI/Views/PreviewView.xaml");
        var hint = ReadSource("src/KKL.WordStudio.UI/Views/PreviewView.CaptionHint.cs");

        Assert.Contains("ResolvedTextFormat? CaptionFormat", payload, StringComparison.Ordinal);
        Assert.Contains("CaptionFormat = table.CaptionFormat", projection, StringComparison.Ordinal);
        Assert.Contains("CaptionLineHeight", viewModel, StringComparison.Ordinal);
        Assert.Contains("CaptionFirstLineIndent", viewModel, StringComparison.Ordinal);
        Assert.Contains("PreviewTextBlockControl Runs=\"{Binding CaptionRuns}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextAlignment=\"{Binding CaptionAlignment}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("LineHeight=\"{Binding CaptionLineHeight}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FirstLineIndent=\"{Binding CaptionFirstLineIndent}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tablo başlığı eklemek için çift tıklayın", hint, StringComparison.Ordinal);
        Assert.Contains("StaysOpen = false", hint, StringComparison.Ordinal);
        Assert.Contains("BeginTableCaptionEdit(block)", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void NoReferenceProfile_GenericPreviewStillWorks()
    {
        var control = ReadSource("src/KKL.WordStudio.UI/Preview/PreviewTableGridControl.cs");
        var projection = ReadSource("src/KKL.WordStudio.UI/ViewModels/PreviewPageProjection.cs");

        Assert.Contains("Format ?? DefaultFormatProfiles.Table", control, StringComparison.Ordinal);
        Assert.Contains("FallbackColumnFormat", control, StringComparison.Ordinal);
        Assert.Contains("FallbackColumnFormat", projection, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(Path.Combine(FindSolutionRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindSolutionRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Distinct())
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "KKL.WordStudio.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("KKL.WordStudio.sln bulunamadı.");
    }
}
