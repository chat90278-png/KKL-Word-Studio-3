namespace KKL.WordStudio.Infrastructure.Tests;

using KKL.WordStudio.Infrastructure.Excel;
using KKL.WordStudio.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class Sprint22AsyncExcelReadTests
{
    private static OpenXmlExcelWorkbookReader CreateReader() =>
        new(NullLogger<OpenXmlExcelWorkbookReader>.Instance);

    [Fact]
    public async Task PreCancelledOpenWorkbook_ThrowsCancellation()
    {
        var filePath = Sprint22WorkbookFixtureFactory.Create(
            Sprint22WorkbookScenario.NormalSixColumn);
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateReader().OpenWorkbookAsync(filePath, cancellation.Token));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task PreCancelledSheetPreview_ThrowsCancellation()
    {
        var filePath = Sprint22WorkbookFixtureFactory.Create(
            Sprint22WorkbookScenario.NormalSixColumn);
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateReader().GetSheetPreviewAsync(
                    filePath,
                    Sprint22WorkbookFixtureFactory.WorksheetName(1),
                    cancellationToken: cancellation.Token));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task PreCancelledRangeDetection_ThrowsCancellation()
    {
        var filePath = Sprint22WorkbookFixtureFactory.Create(
            Sprint22WorkbookScenario.NormalSixColumn);
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateReader().DetectDataRangeAsync(
                    filePath,
                    Sprint22WorkbookFixtureFactory.WorksheetName(1),
                    dataStartRow: 2,
                    startColumn: 1,
                    endColumn: Sprint22WorkbookScenario.NormalSixColumn.ColumnCount,
                    cancellationToken: cancellation.Token));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
