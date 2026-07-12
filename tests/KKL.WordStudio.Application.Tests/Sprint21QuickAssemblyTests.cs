namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.QuickAssembly;
using Xunit;

public sealed class Sprint21QuickAssemblyTests
{
    [Fact]
    public void Synchronize_UsesWorkbookAndSheetOrderAndDeduplicatesTargets()
    {
        var selection = new QuickAssemblySelection();

        selection.Synchronize(
        [
            Source("C:/data/a.xlsx", "a.xlsx", "First", "Second", "Second"),
            Source("C:/data/b.xlsx", "b.xlsx", "Third")
        ]);

        Assert.Equal(3, selection.Targets.Count);
        Assert.Collection(
            selection.Targets,
            target => AssertTarget(target, "a.xlsx", "First", 0, 0),
            target => AssertTarget(target, "a.xlsx", "Second", 0, 1),
            target => AssertTarget(target, "b.xlsx", "Third", 1, 0));
    }

    [Fact]
    public void Synchronize_PreservesSessionChoicesAndRemovesStaleSheets()
    {
        var selection = new QuickAssemblySelection();
        selection.Synchronize([Source("C:/data/a.xlsx", "a.xlsx", "Keep", "Remove")]);
        Assert.True(selection.SetSheetSelected("C:/data/a.xlsx", "Keep", true));
        Assert.True(selection.SetCaption("C:/data/a.xlsx", "Keep", "  Inventory  "));

        selection.Synchronize([Source("C:/data/a.xlsx", "a.xlsx", "Keep", "New")]);

        Assert.Equal(2, selection.Targets.Count);
        var kept = Assert.Single(selection.Targets, target => target.WorksheetName == "Keep");
        Assert.True(kept.IsSelected);
        Assert.Equal("Inventory", kept.Caption);
        Assert.DoesNotContain(selection.Targets, target => target.WorksheetName == "Remove");
        Assert.False(Assert.Single(selection.Targets, target => target.WorksheetName == "New").IsSelected);
    }

    [Fact]
    public void WorkbookSelection_TogglesOnlyThatWorkbook()
    {
        var selection = new QuickAssemblySelection();
        selection.Synchronize(
        [
            Source("C:/data/a.xlsx", "a.xlsx", "A1", "A2"),
            Source("C:/data/b.xlsx", "b.xlsx", "B1")
        ]);

        selection.SetWorkbookSelected("C:/data/a.xlsx", true);

        Assert.Equal(2, selection.SelectedTargets.Count);
        Assert.All(selection.SelectedTargets, target => Assert.Equal("a.xlsx", target.WorkbookDisplayName));
        Assert.False(Assert.Single(selection.Targets, target => target.WorksheetName == "B1").IsSelected);
    }

    [Fact]
    public async Task Batch_UsesVisibleOrderAndContinuesAfterFailure()
    {
        var called = new List<string>();
        var orchestrator = new QuickAssemblyBatchOrchestrator();
        var targets = new[]
        {
            Target("C:/data/b.xlsx", "b.xlsx", "B1", workbookOrder: 1, worksheetOrder: 0),
            Target("C:/data/a.xlsx", "a.xlsx", "A2", workbookOrder: 0, worksheetOrder: 1),
            Target("C:/data/a.xlsx", "a.xlsx", "A1", workbookOrder: 0, worksheetOrder: 0)
        };

        var result = await orchestrator.ExecuteAsync(targets, (target, _) =>
        {
            called.Add(target.WorksheetName);
            if (target.WorksheetName == "A2")
                throw new InvalidOperationException("broken sheet");

            return Task.FromResult(new QuickAssemblyTransferOutcome
            {
                Status = target.WorksheetName == "B1"
                    ? QuickAssemblyTransferStatus.Skipped
                    : QuickAssemblyTransferStatus.Created,
                Message = target.WorksheetName
            });
        });

        Assert.Equal(new[] { "A1", "A2", "B1" }, called);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("broken sheet", result.Targets[1].Message);
    }

    [Fact]
    public async Task Batch_RejectsDuplicateWorkbookSheetTargetsBeforeCallingTransfer()
    {
        var called = false;
        var orchestrator = new QuickAssemblyBatchOrchestrator();
        var duplicate = Target("C:/data/a.xlsx", "a.xlsx", "A1", 0, 0);
        var duplicateWithDifferentCase = Target("c:/DATA/A.xlsx", "a.xlsx", "a1", 0, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.ExecuteAsync(
            [duplicate, duplicateWithDifferentCase],
            (_, _) =>
            {
                called = true;
                return Task.FromResult(new QuickAssemblyTransferOutcome
                {
                    Status = QuickAssemblyTransferStatus.Created
                });
            }));

        Assert.False(called);
    }

    private static QuickAssemblySourceSnapshot Source(
        string sourcePath,
        string displayName,
        params string[] worksheets) => new()
    {
        SourcePath = sourcePath,
        DisplayName = displayName,
        WorksheetNames = worksheets
    };

    private static QuickAssemblyTarget Target(
        string sourcePath,
        string displayName,
        string worksheet,
        int workbookOrder,
        int worksheetOrder) => new()
    {
        SourcePath = sourcePath,
        WorkbookDisplayName = displayName,
        WorksheetName = worksheet,
        WorkbookOrder = workbookOrder,
        WorksheetOrder = worksheetOrder,
        IsSelected = true
    };

    private static void AssertTarget(
        QuickAssemblyTarget target,
        string displayName,
        string worksheet,
        int workbookOrder,
        int worksheetOrder)
    {
        Assert.Equal(displayName, target.WorkbookDisplayName);
        Assert.Equal(worksheet, target.WorksheetName);
        Assert.Equal(workbookOrder, target.WorkbookOrder);
        Assert.Equal(worksheetOrder, target.WorksheetOrder);
    }
}
