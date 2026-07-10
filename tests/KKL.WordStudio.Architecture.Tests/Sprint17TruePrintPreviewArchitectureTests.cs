namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;
using Xunit;

public sealed class Sprint17TruePrintPreviewArchitectureTests
{
    [Fact]
    public void TableFinalDocumentLayer_DoesNotContainBlockLevelDesignerChrome()
    {
        var source = ReadPreviewXaml();
        var finalTableLayer = ExtractSegment(
            source,
            "<!-- Final document layer: table -->",
            "<!-- Interaction layer: table -->");

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
            "<Style x:Key=\"PageBlockDesignerBadge\"");

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
            "<!-- Interaction layer: table -->");

        Assert.DoesNotContain("Tablo başlığı eklemek", finalTableLayer, StringComparison.Ordinal);
        Assert.Contains("tableBlock.ShowCaptionArea", codeBehind, StringComparison.Ordinal);
        Assert.Contains("!tableBlock.HasCaption", codeBehind, StringComparison.Ordinal);
        Assert.Contains("tableBlock.CanEditCaption", codeBehind, StringComparison.Ordinal);
        Assert.Contains("BeginTableCaptionEdit(tableBlock)", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyCaptionHint_IsCompactFloatingInteractionBadgeAndNeverHeaderFlowText()
    {
        var xaml = ReadPreviewXaml();
        var badgeStyle = ExtractSegment(
            xaml,
            "<Style x:Key=\"EmptyCaptionHintBadge\"",
            "<DataTemplate DataType=\"{x:Type vm:PreviewTextPageBlockViewModel}\"");
        var tableInteractionLayer = ExtractSegment(
            xaml,
            "<!-- Interaction layer: table -->",
            "<DataTemplate DataType=\"{x:Type vm:PreviewTocPageBlockViewModel}\"");

        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\" />", badgeStyle, StringComparison.Ordinal);
        Assert.Contains("Binding ShowCaptionArea", badgeStyle, StringComparison.Ordinal);
        Assert.Contains("Binding HasCaption", badgeStyle, StringComparison.Ordinal);
        Assert.Contains("Binding CanEditCaption", badgeStyle, StringComparison.Ordinal);
        Assert.Contains("Binding IsSelected", badgeStyle, StringComparison.Ordinal);
        Assert.Contains("Path=IsMouseOver", badgeStyle, StringComparison.Ordinal);

        Assert.Contains("Style=\"{StaticResource EmptyCaptionHintBadge}\"", tableInteractionLayer, StringComparison.Ordinal);
        Assert.Contains("Margin=\"4,-18,0,0\"", tableInteractionLayer, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Tablo başlığı eklemek için çift tıklayın\"", tableInteractionLayer, StringComparison.Ordinal);
        Assert.Contains("MouseLeftButtonDown=\"TableCaption_MouseLeftButtonDown\"", tableInteractionLayer, StringComparison.Ordinal);
        Assert.Contains("Text=\"+ Tablo başlığı\"", tableInteractionLayer, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBlock Text=\"Tablo başlığı eklemek için çift tıklayın\"", xaml, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(
                @"x:Name=\"TableBlockHost\"[\s\S]*?ClipToBounds=\"False\"[\s\S]*?<!-- Final document layer: table -->[\s\S]*?<StackPanel ClipToBounds=\"True\">",
                RegexOptions.CultureInvariant),
            xaml);
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
