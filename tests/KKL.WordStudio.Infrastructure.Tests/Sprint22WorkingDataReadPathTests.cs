namespace KKL.WordStudio.Infrastructure.Tests;

using System.Security.Cryptography;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Infrastructure.Excel;
using KKL.WordStudio.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class Sprint22WorkingDataReadPathTests
{
    private static OpenXmlExcelWorkbookReader CreateReader() =>
        new(NullLogger<OpenXmlExcelWorkbookReader>.Instance);

    [Fact]
    public async Task ImplicitEndColumn_DiscoversShapeDuringWorkingDataRead()
    {
        var scenario = Sprint22WorkbookScenario.NormalSixColumn;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var result = await CreateReader().ReadWorkingDataAsync(
                filePath,
                Sprint22WorkbookFixtureFactory.WorksheetName(1),
                new DataRange
                {
                    HeaderRowIndex = 1,
                    DataStartRow = 2,
                    DataEndRow = scenario.DataRowCount + 1,
                    StartColumn = 1,
                    EndColumn = null
                });

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(scenario.ColumnCount, result.Value.Columns.Count);
            Assert.Equal(scenario.DataRowCount, result.Value.Rows.Count);
            Assert.Equal("Column001", result.Value.Columns[0].Header);
            Assert.Equal("Column006", result.Value.Columns[^1].Header);
            Assert.Equal(
                Sprint22WorkbookFixtureFactory.ExpectedValue(1, scenario.DataRowCount, scenario.ColumnCount),
                result.Value.Rows[^1].Values[^1]);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SparseImplicitRange_PreservesNullCellsAndSourceHash()
    {
        var scenario = Sprint22WorkbookScenario.SparseDirty;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var beforeHash = SHA256.HashData(await File.ReadAllBytesAsync(filePath));

            var result = await CreateReader().ReadWorkingDataAsync(
                filePath,
                Sprint22WorkbookFixtureFactory.WorksheetName(1),
                new DataRange
                {
                    HeaderRowIndex = 1,
                    DataStartRow = 2,
                    DataEndRow = scenario.DataRowCount + 1,
                    StartColumn = 1,
                    EndColumn = null
                });

            var afterHash = SHA256.HashData(await File.ReadAllBytesAsync(filePath));
            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(beforeHash, afterHash);
            Assert.Equal(scenario.ColumnCount, result.Value.Columns.Count);
            Assert.Equal(scenario.DataRowCount, result.Value.Rows.Count);
            Assert.Null(result.Value.Rows[0].Values[20]); // deterministic sparse cell: data row 1, column 21
            Assert.NotNull(result.Value.Rows[0].Values[0]);
            Assert.NotNull(result.Value.Rows[0].Values[^1]);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExplicitColumnRange_ProjectsOnlyConfiguredColumns()
    {
        var scenario = Sprint22WorkbookScenario.VeryWide;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var result = await CreateReader().ReadWorkingDataAsync(
                filePath,
                Sprint22WorkbookFixtureFactory.WorksheetName(1),
                new DataRange
                {
                    HeaderRowIndex = 1,
                    DataStartRow = 2,
                    DataEndRow = scenario.DataRowCount + 1,
                    StartColumn = 5,
                    EndColumn = 12
                });

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(8, result.Value.Columns.Count);
            Assert.Equal("Column005", result.Value.Columns[0].Header);
            Assert.Equal("Column012", result.Value.Columns[^1].Header);
            Assert.Equal(
                Sprint22WorkbookFixtureFactory.ExpectedValue(1, 1, 5),
                result.Value.Rows[0].Values[0]);
            Assert.Equal(
                Sprint22WorkbookFixtureFactory.ExpectedValue(1, 1, 12),
                result.Value.Rows[0].Values[^1]);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task PreCancelledWorkingDataRead_ThrowsCancellation()
    {
        var scenario = Sprint22WorkbookScenario.NormalSixColumn;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateReader().ReadWorkingDataAsync(
                    filePath,
                    Sprint22WorkbookFixtureFactory.WorksheetName(1),
                    new DataRange
                    {
                        HeaderRowIndex = 1,
                        DataStartRow = 2,
                        DataEndRow = scenario.DataRowCount + 1,
                        StartColumn = 1,
                        EndColumn = scenario.ColumnCount
                    },
                    cancellation.Token));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
