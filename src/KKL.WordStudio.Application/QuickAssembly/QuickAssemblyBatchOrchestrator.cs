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

public sealed class QuickAssemblyBatchResult
{
    public required IReadOnlyList<QuickAssemblyTargetResult> Targets { get; init; }
    public int CreatedCount => Targets.Count(target => target.Status == QuickAssemblyTransferStatus.Created);
    public int SkippedCount => Targets.Count(target => target.Status == QuickAssemblyTransferStatus.Skipped);
    public int FailedCount => Targets.Count(target => target.Status == QuickAssemblyTransferStatus.Failed);
}

/// <summary>
/// Orders and accounts for a multi-target operation while delegating each real
/// import to the existing single-target transfer seam supplied by the caller.
/// It never reads Excel or creates report tables directly.
/// </summary>
public sealed class QuickAssemblyBatchOrchestrator
{
    public async Task<QuickAssemblyBatchResult> ExecuteAsync(
        IEnumerable<QuickAssemblyTarget> targets,
        Func<QuickAssemblyTarget, CancellationToken, Task<QuickAssemblyTransferOutcome>> transferSingleTargetAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(transferSingleTargetAsync);

        var orderedTargets = targets
            .Where(target => target.IsSelected)
            .OrderBy(target => target.WorkbookOrder)
            .ThenBy(target => target.WorksheetOrder)
            .ToList();
        RejectDuplicates(orderedTargets);

        var results = new List<QuickAssemblyTargetResult>(orderedTargets.Count);
        foreach (var target in orderedTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var outcome = await transferSingleTargetAsync(target, cancellationToken);
                results.Add(new QuickAssemblyTargetResult
                {
                    Target = target,
                    Status = outcome.Status,
                    Message = outcome.Message,
                    CreatedElementId = outcome.CreatedElementId
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                results.Add(new QuickAssemblyTargetResult
                {
                    Target = target,
                    Status = QuickAssemblyTransferStatus.Failed,
                    Message = exception.Message
                });
            }
        }

        return new QuickAssemblyBatchResult { Targets = results };
    }

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
