namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.QuickAssembly;
using Xunit;

public sealed class Sprint24QuickReportStructureTests
{
    [Fact]
    public void Selection_ClickOrderBecomesAuthoritativeReportOrder()
    {
        var selection = CreateSelection();
        var a1 = Find(selection, "A1");
        var a2 = Find(selection, "A2");
        var b1 = Find(selection, "B1");

        selection.SetTargetSelected(b1, true);
        selection.SetTargetSelected(a2, true);
        selection.SetTargetSelected(a1, true);

        Assert.Equal(new[] { "B1", "A2", "A1" }, selection.SelectedTargets.Select(target => target.WorksheetName));
        Assert.Equal(new int?[] { 1, 2, 3 }, selection.SelectedTargets.Select(target => target.SelectionOrder));
    }

    [Fact]
    public void Selection_DeselectAndReselectMovesTargetToEndAndCompactsOrder()
    {
        var selection = CreateSelection();
        var a1 = Find(selection, "A1");
        var a2 = Find(selection, "A2");
        var b1 = Find(selection, "B1");

        selection.SetTargetSelected(a1, true);
        selection.SetTargetSelected(a2, true);
        selection.SetTargetSelected(b1, true);
        selection.SetTargetSelected(a2, false);

        Assert.Equal(new[] { "A1", "B1" }, selection.SelectedTargets.Select(target => target.WorksheetName));
        Assert.Equal(new int?[] { 1, 2 }, selection.SelectedTargets.Select(target => target.SelectionOrder));

        selection.SetTargetSelected(a2, true);

        Assert.Equal(new[] { "A1", "B1", "A2" }, selection.SelectedTargets.Select(target => target.WorksheetName));
        Assert.Equal(new int?[] { 1, 2, 3 }, selection.SelectedTargets.Select(target => target.SelectionOrder));
    }

    [Fact]
    public void Selection_MoveSelectedReordersCompleteStructureBlocks()
    {
        var selection = CreateSelection();
        var a1 = Find(selection, "A1");
        var a2 = Find(selection, "A2");
        var b1 = Find(selection, "B1");
        selection.SetTargetSelected(a1, true);
        selection.SetTargetSelected(a2, true);
        selection.SetTargetSelected(b1, true);

        Assert.True(selection.MoveSelected(b1, -1));
        Assert.Equal(new[] { "A1", "B1", "A2" }, selection.SelectedTargets.Select(target => target.WorksheetName));
        Assert.False(selection.MoveSelected(a1, -1));
    }

    [Fact]
    public void Synchronize_PreservesClickOrderAndEditableStructureMetadata()
    {
        var selection = CreateSelection();
        var a2 = Find(selection, "A2");
        var b1 = Find(selection, "B1");
        selection.SetTargetSelected(b1, true);
        selection.SetTargetSelected(a2, true);

        b1.IncludeHeading = false;
        b1.HeadingText = "Ignored heading";
        b1.IncludeAltHeading = true;
        b1.AltHeadingText = "Custom detail";
        b1.TableName = "Custom table";

        selection.Synchronize(
        [
            Source("C:/data/a.xlsx", "a.xlsx", "A1", "A2", "A3"),
            Source("C:/data/b.xlsx", "b.xlsx", "B1")
        ]);

        Assert.Equal(new[] { "B1", "A2" }, selection.SelectedTargets.Select(target => target.WorksheetName));
        var rebuiltB1 = Find(selection, "B1");
        Assert.False(rebuiltB1.IncludeHeading);
        Assert.Equal("Ignored heading", rebuiltB1.HeadingText);
        Assert.True(rebuiltB1.IncludeAltHeading);
        Assert.Equal("Custom detail", rebuiltB1.AltHeadingText);
        Assert.Equal("Custom table", rebuiltB1.TableName);
        Assert.Equal(1, rebuiltB1.SelectionOrder);
    }

    [Fact]
    public async Task Batch_UsesSelectionOrderBeforeWorkbookAndWorksheetOrder()
    {
        var selection = CreateSelection();
        selection.SetTargetSelected(Find(selection, "B1"), true);
        selection.SetTargetSelected(Find(selection, "A2"), true);
        selection.SetTargetSelected(Find(selection, "A1"), true);
        var called = new List<string>();

        var result = await new QuickAssemblyBatchOrchestrator().ExecuteAsync(
            selection.SelectedTargets,
            (target, _) =>
            {
                called.Add(target.WorksheetName);
                return Task.FromResult(new QuickAssemblyTransferOutcome
                {
                    Status = QuickAssemblyTransferStatus.Created,
                    CreatedElementId = Guid.NewGuid()
                });
            });

        Assert.Equal(new[] { "B1", "A2", "A1" }, called);
        Assert.Equal(3, result.CreatedCount);
    }

    private static QuickAssemblySelection CreateSelection()
    {
        var selection = new QuickAssemblySelection();
        selection.Synchronize(
        [
            Source("C:/data/a.xlsx", "a.xlsx", "A1", "A2"),
            Source("C:/data/b.xlsx", "b.xlsx", "B1")
        ]);
        return selection;
    }

    private static QuickAssemblyTarget Find(QuickAssemblySelection selection, string worksheetName) =>
        Assert.Single(selection.Targets, target => target.WorksheetName == worksheetName);

    private static QuickAssemblySourceSnapshot Source(
        string sourcePath,
        string displayName,
        params string[] worksheetNames) => new()
    {
        SourcePath = sourcePath,
        DisplayName = displayName,
        WorksheetNames = worksheetNames
    };
}
