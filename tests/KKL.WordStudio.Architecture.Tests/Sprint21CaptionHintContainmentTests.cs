namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint21CaptionHintContainmentTests
{
    [Fact]
    public void EmptyCaptionHint_UsesBoundedAdornerAndNoDetachedPopupSurface()
    {
        var root = SolutionRootLocator.Find();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.UI",
            "Views",
            "PreviewView.CaptionHint.cs"));

        Assert.Contains("EmptyCaptionHintAdorner", source, StringComparison.Ordinal);
        Assert.Contains("AdornerLayer.GetAdornerLayer(host)", source, StringComparison.Ordinal);
        Assert.Contains("host.Unloaded += CaptionHintTarget_Unloaded", source, StringComparison.Ordinal);
        Assert.Contains("_emptyCaptionHintLayer.Remove(_emptyCaptionHintAdorner)", source, StringComparison.Ordinal);
        Assert.Contains("Clip = new RectangleGeometry", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetHelpText", source, StringComparison.Ordinal);

        Assert.DoesNotContain("new Popup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PlacementMode", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Controls.Primitives", source, StringComparison.Ordinal);
    }
}
