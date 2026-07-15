namespace KKL.WordStudio.Architecture.Tests;

using System.Buffers.Binary;
using Xunit;

public sealed class Sprint23TocPaginationBrandingArchitectureTests
{
    [Fact]
    public void ContentsDoubleClick_NavigatesByStableElementIdAndWaitsForCurrentPreview()
    {
        var root = SolutionRootLocator.Find();
        var contentsXaml = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContentsView.xaml");
        var contentsCode = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContentsView.xaml.cs");
        var previewNavigation = Read(root, "src", "KKL.WordStudio.UI", "Views", "PreviewView.Diagnostics.cs");

        Assert.Contains("MouseDoubleClick=\"Tree_MouseDoubleClick\"", contentsXaml, StringComparison.Ordinal);
        Assert.Contains("_previewViewModel.NavigateToElement(node.ElementId)", contentsCode, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToElement(node.DisplayName)", contentsCode, StringComparison.Ordinal);
        Assert.Contains("BringElementIntoView(elementId)", previewNavigation, StringComparison.Ordinal);
        Assert.Contains("block.ElementId == elementId", previewNavigation, StringComparison.Ordinal);
        Assert.Contains("Task.Delay(100, cancellation.Token)", previewNavigation, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewAndWord_ConsumeOneTableToHeadingPageBreakPolicy()
    {
        var root = SolutionRootLocator.Find();
        var policy = Read(root, "src", "KKL.WordStudio.Application", "Content", "ReportFlowPaginationPolicy.cs");
        var previewFlow = Read(root, "src", "KKL.WordStudio.Engine", "Layout", "LayoutPageFlow.cs");
        var wordExporter = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "WordExporter.cs");
        var wordWriter = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordContentWriter.cs");

        Assert.Contains("previousKind == ReportContentKind.Table", policy, StringComparison.Ordinal);
        Assert.Contains("ReportContentKind.Heading or ReportContentKind.AltHeading", policy, StringComparison.Ordinal);
        Assert.Contains("ReportFlowPaginationPolicy.StartsNewPageAfterTable", previewFlow, StringComparison.Ordinal);
        Assert.Contains("ReportFlowPaginationPolicy.StartsNewPageAfterTable", wordExporter, StringComparison.Ordinal);
        Assert.Contains("new PageBreakBefore()", wordWriter, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildPageBreakParagraph()", wordExporter, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedHeadingPlacement_ReusesRealAnchorWithoutCreatingAnotherTree()
    {
        var root = SolutionRootLocator.Find();
        var placementUi = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.DisplayOrder.cs");
        var coordinator = Read(root, "src", "KKL.WordStudio.Application", "Transfer", "ExcelTransferPlacementCoordinator.cs");

        Assert.Contains("_placementAnchorElementId = selectedHeading.Id", placementUi, StringComparison.Ordinal);
        Assert.Contains("var anchorIdForTransfer = altHeading?.Id", coordinator, StringComparison.Ordinal);
        Assert.Contains("?? heading?.Id", coordinator, StringComparison.Ordinal);
        Assert.Contains("?? placement.AnchorElementId", coordinator, StringComparison.Ordinal);
        Assert.Contains("?? rootHeading.Id", coordinator, StringComparison.Ordinal);
        Assert.Contains("targetElementId: anchorIdForTransfer", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("ParentId", placementUi, StringComparison.Ordinal);
        Assert.DoesNotContain("ParentId", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void Branding_UsesSelectedTransparentMarksAndGeneratedMultiResolutionWindowsIcon()
    {
        var root = SolutionRootLocator.Find();
        var brandDirectory = Path.Combine(root, "src", "KKL.WordStudio.UI", "Assets", "Brand");
        var master = File.ReadAllBytes(Path.Combine(brandDirectory, "BrandMark.png"));
        var small = File.ReadAllBytes(Path.Combine(brandDirectory, "BrandMarkSmall.png"));
        var encodedIconSource = File.ReadAllText(Path.Combine(brandDirectory, "AppIcon.base64")).Trim();
        var iconSource = Convert.FromBase64String(encodedIconSource);
        var generator = Read(root, "scripts", "generate-app-icon.ps1");

        AssertPng(master, expectedWidth: 256, expectedHeight: 256);
        AssertPng(small, expectedWidth: 128, expectedHeight: 128);
        Assert.Equal(new byte[] { 0, 0, 1, 0 }, iconSource[..4]);
        Assert.Equal((ushort)1, BitConverter.ToUInt16(iconSource, 4));

        var frameLength = checked((int)BitConverter.ToUInt32(iconSource, 14));
        var frameOffset = checked((int)BitConverter.ToUInt32(iconSource, 18));
        Assert.InRange(frameOffset, 22, iconSource.Length - 8);
        Assert.InRange(frameLength, 8, iconSource.Length - frameOffset);
        AssertPng(iconSource.AsSpan(frameOffset, frameLength).ToArray(), expectedWidth: 256, expectedHeight: 256);

        Assert.Contains("$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)", generator, StringComparison.Ordinal);

        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var shellCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");
        Assert.Contains("Source=\"Assets/Brand/BrandMarkSmall.png\"", shell, StringComparison.Ordinal);
        Assert.Contains("BrandMarkSmall.png", shellCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Icon=\"Assets/Brand/AppIcon.ico\"", shell, StringComparison.Ordinal);
    }

    private static void AssertPng(byte[] bytes, int expectedWidth, int expectedHeight)
    {
        Assert.True(bytes.Length > 33);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.Equal(expectedWidth, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(expectedHeight, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        Assert.Contains((byte)0, bytes.AsSpan(8).ToArray());
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
