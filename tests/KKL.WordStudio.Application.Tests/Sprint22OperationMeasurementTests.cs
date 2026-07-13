namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Operations;
using Xunit;

public sealed class Sprint22OperationMeasurementTests
{
    [Fact]
    public async Task RunAsync_RecordsSuccessWithoutChangingResult()
    {
        OperationMeasurement? recorded = null;
        var runner = new LongOperationMeasurementRunner(measurement => recorded = measurement);

        var result = await runner.RunAsync("Excel preview", _ => Task.FromResult(42));

        Assert.Equal(42, result);
        Assert.NotNull(recorded);
        Assert.Equal("Excel preview", recorded.OperationName);
        Assert.Equal(OperationCompletionStatus.Succeeded, recorded.Status);
        Assert.Null(recorded.ErrorMessage);
        Assert.True(recorded.Elapsed >= TimeSpan.Zero);
        Assert.True(recorded.AllocatedBytes >= 0);
    }

    [Fact]
    public async Task RunAsync_RecordsFailureAndRethrowsOriginalException()
    {
        OperationMeasurement? recorded = null;
        var runner = new LongOperationMeasurementRunner(measurement => recorded = measurement);
        var expected = new InvalidOperationException("broken workbook");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync<int>("Excel load", _ => Task.FromException<int>(expected)));

        Assert.Same(expected, actual);
        Assert.NotNull(recorded);
        Assert.Equal(OperationCompletionStatus.Failed, recorded.Status);
        Assert.Equal("broken workbook", recorded.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_RecordsCancellationAndPreservesCancellation()
    {
        OperationMeasurement? recorded = null;
        var runner = new LongOperationMeasurementRunner(measurement => recorded = measurement);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync("Word export", _ => Task.CompletedTask, cancellation.Token));

        Assert.NotNull(recorded);
        Assert.Equal(OperationCompletionStatus.Cancelled, recorded.Status);
        Assert.Null(recorded.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_DiagnosticsCallbackCannotBreakSuccessfulOperation()
    {
        var runner = new LongOperationMeasurementRunner(_ =>
            throw new InvalidOperationException("diagnostics sink failed"));

        var result = await runner.RunAsync("Preview", _ => Task.FromResult("ok"));

        Assert.Equal("ok", result);
    }
}
