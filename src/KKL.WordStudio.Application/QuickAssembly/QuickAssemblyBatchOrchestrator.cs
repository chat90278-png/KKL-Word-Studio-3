namespace KKL.WordStudio.Application.QuickAssembly;

public enum QuickAssemblyTransferStatus
{
    Created,
    Skipped,
    Failed
}

public sealed class QuickAssemblyTransferOutcome
{
    public required QuickAssemblyTransferStatus Status { get; init; }
    public string? Message { get; init; }
    public Guid? CreatedElementId { get; init; }
}

public sealed class QuickAssemblyTargetResult
{
    public required QuickAssemblyTarget Target { get; init; }
    public required QuickAssemblyTransferStatus Status { get; init; }
    public string? Message { get; init; }
    public Guid? CreatedElementId { get; init; }
}

/// <summary>
/// One deterministic progress snapshot. A snapshot with a current target and no
/// last status means that target is about to start; a last status means it has
/// completed. Cancellation is reported separately and never fabricates a
/// success/failure result for a target that did not finish.
/// </summary>
public sealed class QuickAssemblyProgress
{
    public required int CompletedCount { get; init; }
    public required int TotalCount { get; init; }
    public QuickAssemblyTarget? CurrentTarget { get; init; }
    public QuickAssemblyTransferStatus? LastStatus { get; init; }
    public bool IsCancelled { get; init; }
}

public sealed class QuickAssemblyBatchResult
{
    public required IReadOnlyList<QuickAssemblyTargetResult> Targets { get; init; }
    public int TotalTargetCount { get; init; }
    public bool IsCancelled { get; init; }
    public int CreatedCount => Targets.Count(target => target.Status == QuickAssemblyTransferStatus.Created);
    public int SkippedCount => Targets.Count(target => target.Status == QuickAssemblyTransferStatus.Skipped);
    public int FailedCount => Targets.Count(target => target.Status == QuickAssemblyTransferStatus.Failed);
}

/// <summary>
/// Orders and accounts for a multi-target operation while delegating each real
/// import to the existing single-target transfer seam supplied by the caller.
/// It never reads Excel or creates report tables directly.
///
/// SelectionOrder is authoritative. Workbook/worksheet order remains a safe
/// fallback for callers created before click-order tracking was introduced.
/// Cancellation is cooperative and bounded: an already-running target gets the
/// token and may stop at its own safe checkpoints; otherwise cancellation takes
/// effect before the next target. Completed target results are always retained.
/// </summary>
public sealed class QuickAssemblyBatchOrchestrator
{
    public async Task<QuickAssemblyBatchResult> ExecuteAsync(
        IEnumerable<QuickAssemblyTarget> targets,
        Func<QuickAssemblyTarget, CancellationToken, Task<QuickAssemblyTransferOutcome>> transferSingleTargetAsync,
        CancellationToken cancellationToken = default,
        IProgress<QuickAssemblyProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(transferSingleTargetAsync);

        var orderedTargets = targets
            .Where(target => target.IsSelected)
            .OrderBy(target => target.SelectionOrder ?? int.MaxValue)
            .ThenBy(target => target.WorkbookOrder)
            .ThenBy(target => target.WorksheetOrder)
            .ToList();
        RejectDuplicates(orderedTargets);

        var results = new List<QuickAssemblyTargetResult>(orderedTargets.Count);
        ReportProgress(progress, results.Count, orderedTargets.Count);

        foreach (var target in orderedTargets)
        {
            if (cancellationToken.IsCancellationRequested)
                return Cancelled(results, orderedTargets.Count, progress);

            ReportProgress(progress, results.Count, orderedTargets.Count, target);

            QuickAssemblyTargetResult targetResult;
            try
            {
                var outcome = await transferSingleTargetAsync(target, cancellationToken);
                targetResult = new QuickAssemblyTargetResult
                {
                    Target = target,
                    Status = outcome.Status,
                    Message = outcome.Message,
                    CreatedElementId = outcome.CreatedElementId
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Cancelled(results, orderedTargets.Count, progress);
            }
            catch (Exception exception)
            {
                targetResult = new QuickAssemblyTargetResult
                {
                    Target = target,
                    Status = QuickAssemblyTransferStatus.Failed,
                    Message = exception.Message
                };
            }

            results.Add(targetResult);
            ReportProgress(
                progress,
                results.Count,
                orderedTargets.Count,
                target,
                targetResult.Status);
        }

        return new QuickAssemblyBatchResult
        {
            Targets = results,
            TotalTargetCount = orderedTargets.Count
        };
    }

    private static QuickAssemblyBatchResult Cancelled(
        IReadOnlyList<QuickAssemblyTargetResult> results,
        int totalCount,
        IProgress<QuickAssemblyProgress>? progress)
    {
        ReportProgress(progress, results.Count, totalCount, isCancelled: true);
        return new QuickAssemblyBatchResult
        {
            Targets = results.ToList(),
            TotalTargetCount = totalCount,
            IsCancelled = true
        };
    }

    private static void ReportProgress(
        IProgress<QuickAssemblyProgress>? progress,
        int completedCount,
        int totalCount,
        QuickAssemblyTarget? currentTarget = null,
        QuickAssemblyTransferStatus? lastStatus = null,
        bool isCancelled = false) =>
        progress?.Report(new QuickAssemblyProgress
        {
            CompletedCount = completedCount,
            TotalCount = totalCount,
            CurrentTarget = currentTarget,
            LastStatus = lastStatus,
            IsCancelled = isCancelled
        });

    private static void RejectDuplicates(IReadOnlyList<QuickAssemblyTarget> targets)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            if (!keys.Add(target.Key))
            {
                throw new ArgumentException(
                    $"Quick-assembly target '{target.WorkbookDisplayName} / {target.WorksheetName}' was submitted more than once.",
                    nameof(targets));
            }
        }
    }
}
