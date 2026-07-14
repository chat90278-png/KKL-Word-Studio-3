namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint23ReportStructureArchitectureTests
{
    [Fact]
    public void ContentsCommands_RouteThroughSingleDocumentStructurePolicy()
    {
        var root = SolutionRootLocator.Find();
        var xaml = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContentsView.xaml");
        var viewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ContentsViewModel.Sprint23Structure.cs");
        var policy = Read(root, "src", "KKL.WordStudio.Application", "Structure", "ReportDocumentStructurePolicy.cs");
        var workspace = Read(root, "src", "KKL.WordStudio.Application", "Workspace", "Workspace.cs");

        Assert.Contains("ItemsSource=\"{Binding StructuredRootNodes}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AddStructuredHeadingCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("AddStructuredAltHeadingCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DeleteStructuredSelectedCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ReportDocumentStructurePolicy.InsertHeading", viewModel, StringComparison.Ordinal);
        Assert.Contains("ReportDocumentStructurePolicy.InsertAltHeading", viewModel, StringComparison.Ordinal);
        Assert.Contains("ReportDocumentStructurePolicy.Move", viewModel, StringComparison.Ordinal);
        Assert.Contains("EnsureRootAndRenumber(report)", workspace, StringComparison.Ordinal);
        Assert.Contains("No second tree or persisted parent relation is introduced", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("ParentId", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("ContentsNode", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void DragDrop_ExposesBeforeIntoAfterFeedbackAndDelegatesMutation()
    {
        var root = SolutionRootLocator.Find();
        var xaml = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContentsView.xaml");
        var codeBehind = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContentsView.xaml.cs");

        Assert.Contains("DragOver=\"Tree_DragOver\"", xaml, StringComparison.Ordinal);
        Assert.Contains("StructureDropMode.Before", codeBehind, StringComparison.Ordinal);
        Assert.Contains("StructureDropMode.Into", codeBehind, StringComparison.Ordinal);
        Assert.Contains("StructureDropMode.After", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Önüne taşı", codeBehind, StringComparison.Ordinal);
        Assert.Contains("İçine taşı", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Sonrasına taşı", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MoveByDragDropV23", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Root.Children.Insert", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Root.Children.Remove", codeBehind, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
