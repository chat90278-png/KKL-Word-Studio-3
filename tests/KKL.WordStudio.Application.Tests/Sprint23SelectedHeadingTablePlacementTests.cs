namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint23SelectedHeadingTablePlacementTests
{
    [Fact]
    public void CreateNewTable_WithoutProposalHeadings_UsesSelectedHeadingAsAnchor()
    {
        var project = new Project { Name = "Project" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body, AutoHeight = true };
        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);

        var root = new TextElement
        {
            Name = "Document Root",
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("System Test Procedure Configuration List")
        };
        var selectedHeading = new TextElement
        {
            Name = "Heading",
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("Selected Assembly")
        };
        body.Root.Children.Add(root);
        body.Root.Children.Add(selectedHeading);
        ReportHeadingNumberingService.Renumber(report);

        var placement = new ExcelTransferPlacementRequest
        {
            Transfer = BuildRequest(selectedHeading.Id),
            DestinationMode = ExcelTransferDestinationMode.CreateNewTable,
            AnchorElementId = selectedHeading.Id,
            TableName = "Selected heading table",
            IncludeHeading = false,
            IncludeAltHeading = false,
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
        Assert.Equal(3, body.Root.Children.Count);
        Assert.Same(root, body.Root.Children[0]);
        Assert.Same(selectedHeading, body.Root.Children[1]);
        var table = Assert.IsType<TableElement>(body.Root.Children[2]);
        Assert.Equal("Selected heading table", table.Caption);
        Assert.DoesNotContain(body.Root.Children.OfType<TextElement>(), element =>
            element.Id != root.Id && element.Id != selectedHeading.Id);
    }

    private static ExcelTransferRequest BuildRequest(Guid anchorId) => new()
    {
        WorkbookFilePath = "/tmp/source.xlsx",
        WorkbookFileName = "source.xlsx",
        WorksheetName = "Sheet1",
        Range = new DataRange
        {
            HeaderRowIndex = 1,
            DataStartRow = 2,
            DataEndRow = 5,
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
        TargetElementId = anchorId
    };
}
