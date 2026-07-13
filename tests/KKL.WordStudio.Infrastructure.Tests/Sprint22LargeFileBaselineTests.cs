namespace KKL.WordStudio.Infrastructure.Tests;

using System.Security.Cryptography;
using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Operations;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Infrastructure.Excel;
using KKL.WordStudio.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

public sealed class Sprint22LargeFileBaselineTests
{
    private readonly ITestOutputHelper _output;

    public Sprint22LargeFileBaselineTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ThousandsOfRows_ReaderPreservesShapeAndSourceHash()
    {
        var scenario = Sprint22WorkbookScenario.ThousandsOfRows;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var beforeHash = Hash(filePath);
            var measurements = new List<OperationMeasurement>();
            var runner = new LongOperationMeasurementRunner(measurements.Add);
            var reader = CreateReader();
            var worksheetName = Sprint22WorkbookFixtureFactory.WorksheetName(1);

            var workbook = await runner.RunAsync(
                "excel.open-workbook.thousands",
                token => reader.OpenWorkbookAsync(filePath, token));
            var preview = await runner.RunAsync(
                "excel.preview.thousands",
                token => reader.GetSheetPreviewAsync(filePath, worksheetName, maxPreviewRows: 100, token));
            var range = await runner.RunAsync(
                "excel.autorange.thousands",
                token => reader.DetectDataRangeAsync(filePath, worksheetName, dataStartRow: 2, startColumn: 1, endColumn: scenario.ColumnCount, token));
            var workingData = await runner.RunAsync(
                "excel.working-data.thousands",
                token => reader.ReadWorkingDataAsync(
                    filePath,
                    worksheetName,
                    new DataRange
                    {
                        HeaderRowIndex = 1,
                        DataStartRow = 2,
                        DataEndRow = scenario.DataRowCount + 1,
                        StartColumn = 1,
                        EndColumn = scenario.ColumnCount
                    },
                    token));

            Assert.True(workbook.IsSuccess, workbook.Error);
            Assert.Single(workbook.Value.Worksheets);
            Assert.True(preview.IsSuccess, preview.Error);
            Assert.Equal(100, preview.Value.Rows.Count);
            Assert.Equal(scenario.ColumnCount, preview.Value.ColumnCount);
            Assert.True(preview.Value.IsTruncated);
            Assert.True(range.IsSuccess, range.Error);
            Assert.Equal(scenario.DataRowCount + 1, range.Value.DataEndRow);
            Assert.True(workingData.IsSuccess, workingData.Error);
            Assert.Equal(scenario.ColumnCount, workingData.Value.Columns.Count);
            Assert.Equal(scenario.DataRowCount, workingData.Value.Rows.Count);
            Assert.Equal(
                Sprint22WorkbookFixtureFactory.ExpectedValue(1, scenario.DataRowCount, scenario.ColumnCount),
                workingData.Value.Rows[^1].Values[^1]);
            Assert.Equal(beforeHash, Hash(filePath));
            AssertSuccessfulMeasurements(measurements, expectedCount: 4);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task VeryWideWorksheet_PreviewPreservesAllColumnsBeyondZ()
    {
        var scenario = Sprint22WorkbookScenario.VeryWide;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var beforeHash = Hash(filePath);
            var measurements = new List<OperationMeasurement>();
            var runner = new LongOperationMeasurementRunner(measurements.Add);
            var reader = CreateReader();
            var worksheetName = Sprint22WorkbookFixtureFactory.WorksheetName(1);

            var preview = await runner.RunAsync(
                "excel.preview.very-wide",
                token => reader.GetSheetPreviewAsync(filePath, worksheetName, maxPreviewRows: 8, token));

            Assert.True(preview.IsSuccess, preview.Error);
            Assert.Equal(8, preview.Value.Rows.Count);
            Assert.Equal(scenario.ColumnCount, preview.Value.ColumnCount);
            Assert.Equal("Column160", preview.Value.Rows[0][159]);
            Assert.Equal(
                Sprint22WorkbookFixtureFactory.ExpectedValue(1, dataRowIndex: 1, columnIndex: 160),
                preview.Value.Rows[1][159]);
            Assert.Equal(beforeHash, Hash(filePath));
            AssertSuccessfulMeasurements(measurements, expectedCount: 1);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ManyWorksheets_OpenWorkbookPreservesDeterministicSheetOrder()
    {
        var scenario = Sprint22WorkbookScenario.ManyWorksheets;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var beforeHash = Hash(filePath);
            var measurements = new List<OperationMeasurement>();
            var runner = new LongOperationMeasurementRunner(measurements.Add);
            var reader = CreateReader();

            var workbook = await runner.RunAsync(
                "excel.open-workbook.many-sheets",
                token => reader.OpenWorkbookAsync(filePath, token));

            Assert.True(workbook.IsSuccess, workbook.Error);
            Assert.Equal(scenario.WorksheetCount, workbook.Value.Worksheets.Count);
            Assert.Equal("Sheet001", workbook.Value.Worksheets[0].Name);
            Assert.Equal("Sheet030", workbook.Value.Worksheets[^1].Name);
            Assert.Equal(
                Enumerable.Range(1, scenario.WorksheetCount).Select(Sprint22WorkbookFixtureFactory.WorksheetName),
                workbook.Value.Worksheets.Select(worksheet => worksheet.Name));
            Assert.Equal(beforeHash, Hash(filePath));
            AssertSuccessfulMeasurements(measurements, expectedCount: 1);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SparseDirtyRows_AutoRangeAndWorkingDataDoNotDropRowsOrColumns()
    {
        var scenario = Sprint22WorkbookScenario.SparseDirty;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var beforeHash = Hash(filePath);
            var measurements = new List<OperationMeasurement>();
            var runner = new LongOperationMeasurementRunner(measurements.Add);
            var reader = CreateReader();
            var worksheetName = Sprint22WorkbookFixtureFactory.WorksheetName(1);

            var range = await runner.RunAsync(
                "excel.autorange.sparse-dirty",
                token => reader.DetectDataRangeAsync(filePath, worksheetName, dataStartRow: 2, startColumn: 1, endColumn: scenario.ColumnCount, token));
            var workingData = await runner.RunAsync(
                "excel.working-data.sparse-dirty",
                token => reader.ReadWorkingDataAsync(
                    filePath,
                    worksheetName,
                    new DataRange
                    {
                        HeaderRowIndex = 1,
                        DataStartRow = 2,
                        DataEndRow = scenario.DataRowCount + 1,
                        StartColumn = 1,
                        EndColumn = scenario.ColumnCount
                    },
                    token));

            Assert.True(range.IsSuccess, range.Error);
            Assert.Equal(scenario.DataRowCount + 1, range.Value.DataEndRow);
            Assert.Equal(1, range.Value.StartColumn);
            Assert.Equal(scenario.ColumnCount, range.Value.EndColumn);
            Assert.True(workingData.IsSuccess, workingData.Error);
            Assert.Equal(scenario.DataRowCount, workingData.Value.Rows.Count);
            Assert.Equal(scenario.ColumnCount, workingData.Value.Columns.Count);
            Assert.All(workingData.Value.Rows, row => Assert.Equal(scenario.ColumnCount, row.Values.Count));
            Assert.Equal(beforeHash, Hash(filePath));
            AssertSuccessfulMeasurements(measurements, expectedCount: 2);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ScenarioCatalog_CoversRequiredInitialWorkbookShapes()
    {
        Assert.Equal(6, Sprint22WorkbookScenario.NormalSixColumn.ColumnCount);
        Assert.True(Sprint22WorkbookScenario.ThousandsOfRows.DataRowCount >= 2_000);
        Assert.True(Sprint22WorkbookScenario.VeryWide.ColumnCount >= 100);
        Assert.True(Sprint22WorkbookScenario.ManyWorksheets.WorksheetCount >= 20);
        Assert.True(Sprint22WorkbookScenario.SparseDirty.IncludeSparseCells);
        Assert.True(Sprint22WorkbookScenario.SparseDirty.IncludeDirtyValues);
    }

    private static OpenXmlExcelWorkbookReader CreateReader() =>
        new(NullLogger<OpenXmlExcelWorkbookReader>.Instance);

    private static byte[] Hash(string filePath) => SHA256.HashData(File.ReadAllBytes(filePath));

    private void AssertSuccessfulMeasurements(
        IReadOnlyList<OperationMeasurement> measurements,
        int expectedCount)
    {
        Assert.Equal(expectedCount, measurements.Count);
        Assert.All(measurements, measurement =>
        {
            Assert.Equal(OperationCompletionStatus.Succeeded, measurement.Status);
            Assert.True(measurement.Elapsed >= TimeSpan.Zero);
            Assert.True(measurement.AllocatedBytes >= 0);
            _output.WriteLine(
                "{0}: elapsed={1:N1} ms, allocated={2:N0} bytes, heapDelta={3:N0} bytes",
                measurement.OperationName,
                measurement.Elapsed.TotalMilliseconds,
                measurement.AllocatedBytes,
                measurement.ManagedHeapDeltaBytes);
        });
    }
}
