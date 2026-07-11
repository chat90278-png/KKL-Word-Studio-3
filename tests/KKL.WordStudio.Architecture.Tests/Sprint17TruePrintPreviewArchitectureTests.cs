namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;
using Xunit;

public sealed class Sprint17TruePrintPreviewArchitectureTests
{
    private const string TableInteractionMarker =
        "<!-- Interaction layer: table. Empty-caption editing remains available";

    [Fact]
    public void TableFinalDocumentLayer_DoesNotContainBlockLevelDesignerChrome()
    {
        var source = ReadPreviewXaml();
        var finalTableLayer = ExtractSegment(
            source,
            "<!-- Final document layer: table -->",
            TableInteractionMarker);

        Assert.Contains("CaptionRuns", finalTableLayer, StringComparison.Ordinal);
        Assert.Contains("PreviewTableGridControl", finalTableLayer, StringComparison.Ordinal);
        Assert.DoesNotContain("Tablo başlığı eklemek", finalTableLayer, StringComparison.Ordinal);
        Assert.DoesNotContain("ContinuationText", finalTableLayer, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceError", finalTableLayer, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding Name}\"", finalTableLayer, StringComparison.Ordinal);
    }

    [Fact]
    public void InteractionFeedback_IsAHitTestFreeOverlayAndEditorsDoNotMeasureDocumentFlow()
    {
        var source = ReadPreviewXaml();
        var hostStyle = ExtractSegment(
            source,
            "<Style x:Key=\"PageBlockInteractionHost\"",
            "<Style x:Key=\"PageBlockInteractionOverlay\"");
        var overlayStyle = ExtractSegment(
            source,
            "<Style x:Key=\"PageBlockInteractionOverlay\"",
            "<DataTemplate DataType=\"{x:Type vm:PreviewTextPageBlockViewModel}\"");

        Assert.DoesNotContain("BorderThickness", hostStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderBrush", hostStyle, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"IsHitTestVisible\" Value=\"False\" />", overlayStyle, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", overlayStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("PageBlockContainer", source, StringComparison.Ordinal);

        Assert.Matches(
            new Regex(
                @"<!-- Interaction layer: text -->[\s\S]*?<Canvas>[\s\S]*?<TextBox",
                RegexOptions.CultureInvariant),
            source);
        Assert.Matches(
            new Regex(
                @"<!-- Interaction layer: table header cell -->[\s\S]*?<Canvas>[\s\S]*?<TextBox",
                RegexOptions.CultureInvariant),
            source);
    }

    [Fact]
    public void EmptyCaptionOverlay_PreservesCaptionEditGestureWithoutPuttingPlaceholderInFlow()
    {
        var xaml = ReadPreviewXaml();
        var codeBehindPath = Path.Combine(
            SolutionRootLocator.Find(),
            "src",
            "KKL.WordStudio.UI",
            "Views",
            "PreviewView.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);

        var finalTableLayer = ExtractSegment(
            xaml,
            "<!-- Final document layer: table -->",
            TableInteractionMarker);

        Assert.DoesNotContain("Tablo başlığı eklemek", finalTableLayer, StringComparison.Ordinal);
        Assert.Contains("tableBlock.ShowCaptionArea", codeBehind, StringComparison.Ordinal);
        Assert.Contains("!tableBlock.HasCaption", codeBehind, StringComparison.Ordinal);
        Assert.Contains("tableBlock.CanEditCaption", codeBehind, StringComparison.Ordinal);
        Assert.Contains("BeginTableCaptionEdit(tableBlock)", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyCaptionHint_IsNonStickyAndNeverCoversDocumentCells()
    {
        var root = SolutionRootLocator.Find();
        var xaml = ReadPreviewXaml();
        var hintSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.UI",
            "Views",
            "PreviewView.CaptionHint.cs"));
        var tableTemplate = ExtractSegment(
            xaml,
            "<DataTemplate DataType=\"{x:Type vm:PreviewTablePageBlockViewModel}\"",
            "<DataTemplate DataType=\"{x:Type vm:PreviewTocPageBlockViewModel}\"");
        var finalTableLayer = ExtractSegment(
            tableTemplate,
            "<!-- Final document layer: table -->",
            TableInteractionMarker);

        Assert.DoesNotContain("<Popup", tableTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("PageBlockDesignerBadge", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ContinuationText", tableTemplate, StringComparison.Ordinal);
        Assert.Contains("new Popup", hintSource, StringComparison.Ordinal);
        Assert.Contains("StaysOpen = false", hintSource, StringComparison.Ordinal);
        Assert.Contains("Placement = PlacementMode.Top", hintSource, StringComparison.Ordinal);
        Assert.Contains("+ Tablo başlığı", hintSource, StringComparison.Ordinal);
        Assert.Contains("Tablo başlığı eklemek için çift tıklayın", hintSource, StringComparison.Ordinal);
        Assert.Contains("BeginTableCaptionEdit(block)", hintSource, StringComparison.Ordinal);
        Assert.Contains("Deactivated += CaptionHintOwnerWindow_Deactivated", hintSource, StringComparison.Ordinal);
        Assert.Contains("<StackPanel ClipToBounds=\"True\">", finalTableLayer, StringComparison.Ordinal);
        Assert.DoesNotContain("ClipToBounds=\"False\"", tableTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void CaptionProjection_UsesStructuredSequenceDisplayBlackFallbackAndRawEditorCaption()
    {
        var root = SolutionRootLocator.Find();
        var projection = File.ReadAllText(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.UI",
            "ViewModels",
            "PreviewPageProjection.cs"));
        var viewModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.UI",
            "ViewModels",
            "PreviewViewModel.cs"));

        Assert.Contains("TableCaptionSequenceFormatter.BuildDisplayText", projection, StringComparison.Ordinal);
        Assert.Contains("table.CaptionSequence", projection, StringComparison.Ordinal);
        Assert.Contains("table.CaptionSequenceNumber", projection, StringComparison.Ordinal);
        Assert.Contains("CaptionForeground = CreateBrush(table.CaptionFormat?.ForegroundColor) ?? Brushes.Black", projection, StringComparison.Ordinal);
        Assert.Contains("CaptionEditText = block.Caption ?? string.Empty", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptionEditText = block.CaptionDisplayText", viewModel, StringComparison.Ordinal);
    }

    private static string ReadPreviewXaml()
    {
        var path = Path.Combine(
            SolutionRootLocator.Find(),
            "src",
            "KKL.WordStudio.UI",
            "Views",
            "PreviewView.xaml");
        return File.ReadAllText(path);
    }

    private static string ExtractSegment(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Source marker not found: {startMarker}");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Source end marker not found after {startMarker}: {endMarker}");
        return source[start..end];
    }
}
