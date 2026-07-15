namespace KKL.WordStudio.Infrastructure.Tests;

using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Infrastructure.ReferenceFormatting;
using KKL.WordStudio.Infrastructure.Word;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class Sprint24ReleaseFailureMatrixTests
{
    [Fact]
    public async Task MissingExcelSource_ReturnsFailureWithoutThrowing()
    {
        var source = CreateSource(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx"));

        var result = await CreateProvider().GetRowsAsync(source);

        Assert.False(result.IsSuccess);
        Assert.Contains("bulunamadı", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorruptExcelSource_ReturnsFailureWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        await File.WriteAllTextAsync(path, "not-an-openxml-workbook");

        try
        {
            var result = await CreateProvider().GetRowsAsync(CreateSource(path));

            Assert.False(result.IsSuccess);
            Assert.Contains("veri okunamadı", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MissingWorksheetConfiguration_ReturnsFailureWithoutOpeningFile()
    {
        var workbook = new Workbook
        {
            FileName = "missing.xlsx",
            SourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx")
        };
        workbook.Worksheets.Add(new Worksheet
        {
            Name = "Sayfa1",
            SelectedRange = new DataRange { DataStartRow = 2 }
        });
        var source = new ExcelDataSource
        {
            Name = "Kaynak",
            Workbook = workbook,
            ActiveWorksheetName = "OlmayanSayfa"
        };

        var result = await CreateProvider().GetRowsAsync(source);

        Assert.False(result.IsSuccess);
        Assert.Contains("sayfası", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingRangeConfiguration_ReturnsFailureWithoutOpeningFile()
    {
        var workbook = new Workbook
        {
            FileName = "missing.xlsx",
            SourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx")
        };
        workbook.Worksheets.Add(new Worksheet { Name = "Sayfa1" });
        var source = new ExcelDataSource
        {
            Name = "Kaynak",
            Workbook = workbook,
            ActiveWorksheetName = "Sayfa1"
        };

        var result = await CreateProvider().GetRowsAsync(source);

        Assert.False(result.IsSuccess);
        Assert.Contains("veri aralığı", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkingData_RemainsAuthoritativeWhenOriginalSourceIsMissing()
    {
        var source = CreateSource(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx"));
        var worksheet = source.Workbook.Worksheets.Single();
        worksheet.WorkingData = new WorksheetWorkingData();
        worksheet.WorkingData.Columns.Add(new WorkingDataColumn
        {
            SourceField = "Parça Numarası",
            Header = "Parça Numarası",
            OriginalSourceColumn = "A"
        });
        var row = new WorkingDataRow { OriginalRowNumber = 2 };
        row.Values.Add("2354");
        worksheet.WorkingData.Rows.Add(row);

        var result = await CreateProvider().GetRowsAsync(source);

        Assert.True(result.IsSuccess, result.Error);
        var record = Assert.Single(result.Value);
        Assert.Equal("2354", record["Parça Numarası"]);
        Assert.Equal("2354", record["A"]);
    }

    [Fact]
    public void MissingReferenceDocx_ReturnsFailureWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".docx");

        var result = new OpenXmlReferenceFormatDocumentService().Import(path);

        Assert.True(result.IsFailure);
        Assert.Contains("bulunamadı", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptReferenceDocx_ReturnsFailureWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".docx");
        File.WriteAllText(path, "not-an-openxml-document");

        try
        {
            var result = new OpenXmlReferenceFormatDocumentService().Import(path);

            Assert.True(result.IsFailure);
            Assert.Contains("açılamadı", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingFrontMatterDocx_ReturnsFailureWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".docx");

        var result = new OpenXmlFrontMatterDocumentService().Import(path);

        Assert.True(result.IsFailure);
        Assert.Contains("bulunamadı", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptFrontMatterDocx_ReturnsFailureWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".docx");
        File.WriteAllText(path, "not-an-openxml-document");

        try
        {
            var result = new OpenXmlFrontMatterDocumentService().Import(path);

            Assert.True(result.IsFailure);
            Assert.Contains("açılamadı", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ExcelDataSource CreateSource(string path)
    {
        var workbook = new Workbook
        {
            FileName = Path.GetFileName(path),
            SourcePath = path
        };
        workbook.Worksheets.Add(new Worksheet
        {
            Name = "Sayfa1",
            SelectedRange = new DataRange
            {
                DataStartRow = 2,
                DataEndRow = 10,
                StartColumn = 1,
                EndColumn = 3
            }
        });

        return new ExcelDataSource
        {
            Name = "Kaynak",
            Workbook = workbook,
            ActiveWorksheetName = "Sayfa1"
        };
    }

    private static ExcelDataProvider CreateProvider() =>
        new(NullLogger<ExcelDataProvider>.Instance);
}
