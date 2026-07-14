namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint23TableCaptionNormalizationTests
{
    [Fact]
    public void UpdateExistingTable_UsesDocumentOrderFallbackAndRemovesRepeatedTablePrefixes()
    {
        var cases = new (string Input, string Expected)[]
        {
            (string.Empty, "Tablo 2"),
            ("Tablo 2", "Tablo 2"),
            ("Deneme", "Deneme"),
            ("Tablo 99: Deneme", "Deneme"),
            ("Tablo 2: Tablo 2: Deneme", "Deneme"),
            ("Tablo 2: Tablo 2", "Tablo 2")
        };

        foreach (var (input, expected) in cases)
        {
            var (project, report, body) = CreateProjectAndReport();
            body.Root.Children.Add(new TableElement { Name = "İlk tablo", Caption = "İlk tablo" });

            var target = new TableElement
            {
                Name = "Tablo 2",
                Binding = new Binding { DataSourceName = "Old", WorksheetName = "OldSheet" }
            };
            target.Columns.Add(new TableColumn { Header = "Old", SourceField = "Old" });
            body.Root.Children.Add(target);

            var placement = new ExcelTransferPlacementRequest
            {
                Transfer = BuildRequest(target.Id),
                DestinationMode = ExcelTransferDestinationMode.UpdateExistingTable,
                ExistingTableId = target.Id,
                TableName = input,
                Columns =
                [
                    new TransferColumnSelection
                    {
                        ProviderField = "A",
                        LogicalField = "ItemNumber",
                        Header = "No",
                        SemanticRole = ExcelSemanticFieldRole.ItemNumber,
                        SourceOrder = 0,
                        IsIncluded = true
                    }
                ]
            };

            var result = ExcelTransferPlacementCoordinator.Transfer(
                new ExcelReportTransferService(),
                project,
                report,
                placement);

            Assert.Equal(TransferOutcome.Success, result.TransferResult.Outcome);
            Assert.Equal(expected, target.Name);
            Assert.Equal(expected, target.Caption);
        }
    }

    private static (Project Project, Report Report, Section Body) CreateProjectAndReport()
    {
        var project = new Project { Name = "Project" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body, AutoHeight = true };
        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report, body);
    }

    private static ExcelTransferRequest BuildRequest(Guid targetElementId) => new()
    {
        WorkbookFilePath = "/tmp/source.xlsx",
        WorkbookFileName = "source.xlsx",
        WorksheetName = "Sheet1",
        Range = new DataRange
        {
            HeaderRowIndex = 1,
            DataStartRow = 2,
            DataEndRow = 10,
            StartColumn = 1,
            EndColumn = 1
        },
        HeaderTexts = ["No"],
        AppliedColumnMappings =
        [
            new TransferColumnMapping
            {
                SourceColumn = "A",
                FieldName = "ItemNumber",
                DataType = "string"
            }
        ],
        TargetElementId = targetElementId
    };
}
