namespace KKL.WordStudio.Application.Operations;

using System.Diagnostics;

public enum OperationCompletionStatus
{
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>
/// One non-invasive observation of a potentially long operation. The values
/// are process-level signals intended for comparing deterministic scenarios;
/// they are not hard release thresholds by themselves.
/// </summary>
public sealed record OperationMeasurement(
    string OperationName,
    TimeSpan Elapsed,
    long ManagedHeapBeforeBytes,
    long ManagedHeapAfterBytes,
    long TotalAllocatedBeforeBytes,
    long TotalAllocatedAfterBytes,
    OperationCompletionStatus Status,
    string? ErrorMessage)
{
    public long ManagedHeapDeltaBytes => ManagedHeapAfterBytes - ManagedHeapBeforeBytes;

    public long AllocatedBytes => Math.Max(0, TotalAllocatedAfterBytes - TotalAllocatedBeforeBytes);
}

/// <summary>
/// Measures an existing asynchronous service seam without changing its result,
/// exception, or cancellation behavior. Recording is best-effort: a diagnostics
/// callback must never turn a successful product operation into a failure.
/// </summary>
public sealed class LongOperationMeasurementRunner
{
    private readonly Action<OperationMeasurement> _recordMeasurement;

    public LongOperationMeasurementRunner(Action<OperationMeasurement>? recordMeasurement = null)
    {
        _recordMeasurement = recordMeasurement ?? (_ => { });
    }

    public async Task RunAsync(
        string operationName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await RunAsync<object?>(
            operationName,
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> RunAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name is required.", nameof(operationName));
        ArgumentNullException.ThrowIfNull(operation);

        var managedHeapBefore = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        var stopwatch = Stopwatch.StartNew();
        var status = OperationCompletionStatus.Succeeded;
        string? errorMessage = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            status = OperationCompletionStatus.Cancelled;
            throw;
        }
        catch (Exception exception)
        {
            status = OperationCompletionStatus.Failed;
            errorMessage = exception.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var measurement = new OperationMeasurement(
                operationName.Trim(),
                stopwatch.Elapsed,
                managedHeapBefore,
                GC.GetTotalMemory(forceFullCollection: false),
                allocatedBefore,
                GC.GetTotalAllocatedBytes(precise: false),
                status,
                errorMessage);

            try
            {
                _recordMeasurement(measurement);
            }
            catch (Exception)
            {
                // Measurements are diagnostics only and must never replace the
                // product operation's result, exception, or cancellation.
            }
        }
    }
}
