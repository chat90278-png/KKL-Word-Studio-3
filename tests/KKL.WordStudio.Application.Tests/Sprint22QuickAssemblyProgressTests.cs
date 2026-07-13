namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.QuickAssembly;
using Xunit;

public sealed class Sprint22QuickAssemblyProgressTests
{
    [Fact]
    public async Task Batch_ReportsDeterministicStartAndCompletionProgress()
    {
        var reports = new List<QuickAssemblyProgress>();
        var orchestrator = new QuickAssemblyBatchOrchestrator();
        var targets = new[]
        {
            Target("C:/data/b.xlsx", "b.xlsx", "B1", workbookOrder: 1, worksheetOrder: 0),
            Target("C:/data/a.xlsx", "a.xlsx", "A1", workbookOrder: 0, worksheetOrder: 0)
        };

        var result = await orchestrator.ExecuteAsync(
            targets,
            (target, _) => Task.FromResult(new QuickAssemblyTransferOutcome
            {
                Status = QuickAssemblyTransferStatus.Created,
                Message = target.WorksheetName
            }),
            progress: new CaptureProgress<QuickAssemblyProgress>(reports.Add));

        Assert.False(result.IsCancelled);
        Assert.Equal(2, result.TotalTargetCount);
        Assert.Equal(2, result.CreatedCount);
        Assert.Collection(
            reports,
            report => AssertProgress(report, completed: 0, total: 2, target: null, status: null),
            report => AssertProgress(report, completed: 0, total: 2, target: "A1", status: null),
            report => AssertProgress(report, completed: 1, total: 2, target: "A1", status: QuickAssemblyTransferStatus.Created),
            report => AssertProgress(report, completed: 1, total: 2, target: "B1", status: null),
            report => AssertProgress(report, completed: 2, total: 2, target: "B1", status: QuickAssemblyTransferStatus.Created));
    }

    [Fact]
    public async Task CancellationBetweenTargets_RetainsCompletedResultsAndDoesNotStartNextTarget()
    {
        using var cancellation = new CancellationTokenSource();
        var called = new List<string>();
        var reports = new List<QuickAssemblyProgress>();
        var orchestrator = new QuickAssemblyBatchOrchestrator();
        var targets = new[]
        {
            Target("C:/data/a.xlsx", "a.xlsx", "A1", 0, 0),
            Target("C:/data/a.xlsx", "a.xlsx", "A2", 0, 1),
            Target("C:/data/a.xlsx", "a.xlsx", "A3", 0, 2)
        };

        var result = await orchestrator.ExecuteAsync(
            targets,
            (target, _) =>
            {
                called.Add(target.WorksheetName);
                cancellation.Cancel();
                return Task.FromResult(new QuickAssemblyTransferOutcome
                {
                    Status = QuickAssemblyTransferStatus.Created
                });
            },
            cancellation.Token,
            new CaptureProgress<QuickAssemblyProgress>(reports.Add));

        Assert.True(result.IsCancelled);
        Assert.Equal(3, result.TotalTargetCount);
        Assert.Single(result.Targets);
        Assert.Equal("A1", Assert.Single(called));
        Assert.Equal(1, result.CreatedCount);
        var cancelled = reports[^1];
        Assert.True(cancelled.IsCancelled);
        Assert.Equal(1, cancelled.CompletedCount);
        Assert.Equal(3, cancelled.TotalCount);
    }

    [Fact]
    public async Task CancellationInsideTarget_DoesNotInventAResultForIncompleteTarget()
    {
        using var cancellation = new CancellationTokenSource();
        var orchestrator = new QuickAssemblyBatchOrchestrator();

        var result = await orchestrator.ExecuteAsync(
            [Target("C:/data/a.xlsx", "a.xlsx", "A1", 0, 0)],
            (_, _) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<QuickAssemblyTransferOutcome>(cancellation.Token);
            },
            cancellation.Token);

        Assert.True(result.IsCancelled);
        Assert.Equal(1, result.TotalTargetCount);
        Assert.Empty(result.Targets);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
    }

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

    private static void AssertProgress(
        QuickAssemblyProgress progress,
        int completed,
        int total,
        string? target,
        QuickAssemblyTransferStatus? status)
    {
        Assert.Equal(completed, progress.CompletedCount);
        Assert.Equal(total, progress.TotalCount);
        Assert.Equal(target, progress.CurrentTarget?.WorksheetName);
        Assert.Equal(status, progress.LastStatus);
        Assert.False(progress.IsCancelled);
    }

    private sealed class CaptureProgress<T>(Action<T> capture) : IProgress<T>
    {
        public void Report(T value) => capture(value);
    }
}
