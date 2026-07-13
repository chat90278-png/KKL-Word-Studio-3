namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint21ColumnSelectionTransferTests
{
    [Fact]
    public void SourceRangeSelection_TransfersOnlyCheckedNonContiguousColumnsInExcelOrder()
    {
        var (project, report) = CreateProjectAndReport();
        var path = Path.GetFullPath("Aircraft.xlsx");
        var selections = new ColumnTransferSelectionSession();
        selections.SetSelection(path, "Sheet1", ["D", "B"]);
        var service = new ColumnSelectionExcelReportTransferService(selections);

        var result = service.Transfer(project, report, new ExcelTransferRequest
        {
            WorkbookFilePath = path,
            WorkbookFileName = "Aircraft.xlsx",
            WorksheetName = "Sheet1",
            Range = new DataRange
            {
                HeaderRowIndex = 1,
                DataStartRow = 2,
                DataEndRow = 8,
                StartColumn = 1,
                EndColumn = 4
            },
            HeaderTexts = ["No", "Parça Adı", "Gereksiz", "NSN"],
            AppliedColumnMappings =
            [
                new TransferColumnMapping { SourceColumn = "A", FieldName = "No" },
                new TransferColumnMapping { SourceColumn = "B", FieldName = "Parça Adı (Türkçe)" },
                new TransferColumnMapping { SourceColumn = "C", FieldName = "Gereksiz" },
                new TransferColumnMapping { SourceColumn = "D", FieldName = "NSN" }
            ]
        });

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.True(result.CreatedNewTable);
        Assert.NotNull(result.Table);
        Assert.Equal(["Parça Adı (Türkçe)", "NSN"], result.Table.Columns.Select(column => column.Header));
        Assert.Equal(["Parça Adı (Türkçe)", "NSN"], result.Table.Columns.Select(column => column.SourceField));
    }

    [Fact]
    public void WorkingDataSelection_UsesOriginalColumnIdentityAndKeepsStableProviderFields()
    {
        var (project, report) = CreateProjectAndReport();
        var path = Path.GetFullPath("Edited.xlsx");
        var selections = new ColumnTransferSelectionSession();
        selections.SetSelection(path, "Data", ["C", "N"]);
        var service = new ColumnSelectionExcelReportTransferService(selections);

        var result = service.Transfer(project, report, new ExcelTransferRequest
        {
            WorkbookFilePath = path,
            WorkbookFileName = "Edited.xlsx",
            WorksheetName = "Data",
            Range = new DataRange
            {
                HeaderRowIndex = 1,
                DataStartRow = 2,
                DataEndRow = 5,
                StartColumn = 3,
                EndColumn = 14
            },
            WorkingDataColumns =
            [
                new TransferWorkingColumn { SourceField = "part_tr", Header = "Parça Adı", OriginalSourceColumn = "C" },
                new TransferWorkingColumn { SourceField = "unused", Header = "Kullanılmayacak", OriginalSourceColumn = "D" },
                new TransferWorkingColumn { SourceField = "nsn", Header = "NSN", OriginalSourceColumn = "N" }
            ],
            AppliedColumnMappings =
            [
                new TransferColumnMapping { SourceColumn = "C", FieldName = "Parça Adı (Türkçe)" },
                new TransferColumnMapping { SourceColumn = "D", FieldName = "Kullanılmayacak" },
                new TransferColumnMapping { SourceColumn = "N", FieldName = "NSN" }
            ]
        });

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.NotNull(result.Table);
        Assert.Equal(2, result.Table.Columns.Count);
        Assert.Equal(["Parça Adı (Türkçe)", "NSN"], result.Table.Columns.Select(column => column.Header));
        Assert.Equal(["Parça Adı (Türkçe)", "NSN"], result.Table.Columns.Select(column => column.SourceField));
    }

    [Fact]
    public void NoExplicitSelection_PreservesLegacyAllColumnBehavior()
    {
        var (project, report) = CreateProjectAndReport();
        var service = new ColumnSelectionExcelReportTransferService(new ColumnTransferSelectionSession());

        var result = service.Transfer(project, report, BasicRequest(Path.GetFullPath("All.xlsx")));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Equal(3, result.Table?.Columns.Count);
    }

    [Fact]
    public void EmptyExplicitSelection_FailsWithoutCreatingTable()
    {
        var (project, report) = CreateProjectAndReport();
        var path = Path.GetFullPath("None.xlsx");
        var selections = new ColumnTransferSelectionSession();
        selections.SetSelection(path, "Sheet1", []);
        var service = new ColumnSelectionExcelReportTransferService(selections);

        var result = service.Transfer(project, report, BasicRequest(path));

        Assert.Equal(TransferOutcome.Failed, result.Outcome);
        Assert.Contains("en az bir sütun", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(report.Pages[0].Sections[0].Root.Children.OfType<TableElement>());
    }

    private static ExcelTransferRequest BasicRequest(string path) => new()
    {
        WorkbookFilePath = path,
        WorkbookFileName = Path.GetFileName(path),
        WorksheetName = "Sheet1",
        Range = new DataRange
        {
            HeaderRowIndex = 1,
            DataStartRow = 2,
            DataEndRow = 4,
            StartColumn = 1,
            EndColumn = 3
        },
        HeaderTexts = ["A Başlığı", "B Başlığı", "C Başlığı"]
    };

    private static (Project Project, Report Report) CreateProjectAndReport()
    {
        var project = new Project { Name = "Column Selection" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        page.Sections.Add(new Section { Name = "Body", Kind = SectionKind.Body });
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report);
    }
}
