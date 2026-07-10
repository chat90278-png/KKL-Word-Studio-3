namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;
using Xunit;

public sealed class Sprint14IntegrationGuardTests
{
    [Fact]
    public void PreviewTextEditing_ReconstructsSemanticTextAcrossFragments()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewViewModel.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Contains("GetSemanticTextForElement(Guid elementId)", source, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(
                @"GetSemanticTextForElement\s*\(\s*Guid\s+elementId\s*\)[\s\S]{0,900}?GroupBy\s*\([^\)]*FragmentIndex[^\)]*\)[\s\S]{0,350}?OrderBy\s*\([^\)]*Key[^\)]*\)[\s\S]{0,350}?PlainText",
                RegexOptions.CultureInvariant),
            source);
        Assert.Matches(
            new Regex(
                @"BeginTextEdit\s*\([^\)]*\)[\s\S]{0,500}?EditText\s*=\s*GetSemanticTextForElement\s*\(",
                RegexOptions.CultureInvariant),
            source);
        Assert.Matches(
            new Regex(
                @"CommitTextEdit\s*\([^\)]*\)[\s\S]{0,900}?semanticText\s*=\s*GetSemanticTextForElement\s*\(\s*elementId\s*\)[\s\S]{0,500}?string\.Equals\s*\(\s*block\.EditText\s*,\s*semanticText",
                RegexOptions.CultureInvariant),
            source);
        Assert.DoesNotContain("block.EditText == block.PlainText", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewStructureGestures_AreBodyRegionOnly()
    {
        var root = SolutionRootLocator.Find();
        var pageBlockPath = Path.Combine(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewPageViewModel.cs");
        var previewViewModelPath = Path.Combine(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewViewModel.cs");
        var previewViewPath = Path.Combine(root, "src", "KKL.WordStudio.UI", "Views", "PreviewView.xaml.cs");
        var pageBlockSource = SourceScan.ReadWithoutComments(pageBlockPath);
        var previewViewModelSource = SourceScan.ReadWithoutComments(previewViewModelPath);
        var previewViewSource = SourceScan.ReadWithoutComments(previewViewPath);

        Assert.Contains("public bool CanInteract =>", pageBlockSource, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(@"CanStructureInteract\s*=>\s*CanInteract\s*&&\s*Region\s*==\s*DocumentPageRegion\.Body", RegexOptions.CultureInvariant),
            pageBlockSource);
        Assert.Contains("candidate.CanStructureInteract", previewViewModelSource, StringComparison.Ordinal);
        Assert.Contains("!block.CanStructureInteract", previewViewModelSource, StringComparison.Ordinal);
        Assert.Contains("block.CanStructureInteract && block.ElementId.HasValue", previewViewModelSource, StringComparison.Ordinal);
        Assert.Contains("_dragSourceElementId = block.CanStructureInteract ? elementId : null", previewViewSource, StringComparison.Ordinal);
        Assert.Contains("!block.CanStructureInteract", previewViewSource, StringComparison.Ordinal);
        Assert.Contains("!targetBlock.CanStructureInteract", previewViewSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewRenderer_SurfacesImportedPreviewStatus()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.UI", "Preview", "PreviewRenderer.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Contains("frontMatter.StatusMessage", source, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(@"!string\.IsNullOrWhiteSpace\s*\(\s*frontMatter\.StatusMessage\s*\)", RegexOptions.CultureInvariant),
            source);
        Assert.Matches(
            new Regex(@"layout\.Warnings[\s\S]{0,250}?Append\s*\(\s*frontMatter\.StatusMessage\s*\)", RegexOptions.CultureInvariant),
            source);
        Assert.Matches(
            new Regex(@"new\s+DocumentLayoutResult[\s\S]{0,350}?Pages\s*=\s*layout\.Pages[\s\S]{0,250}?Warnings\s*=\s*mergedWarnings", RegexOptions.CultureInvariant),
            source);
    }
}
