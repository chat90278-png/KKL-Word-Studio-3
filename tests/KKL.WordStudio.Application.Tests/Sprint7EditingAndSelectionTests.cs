namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Editing;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

/// <summary>Required test 5: selecting an element from the Preview surface flows through the ONE shared Workspace selection state (the same one Contents and Properties read).</summary>
public class SharedSelectionSynchronizationTests
{
    [Fact]
    public void SelectingPreviewElement_UpdatesSharedWorkspaceSelection()
    {
        var workspace = new Workspace();
        var project = new Project();
        var report = new Report();
        project.Reports.Add(report);
        workspace.SetActiveProject(project);
        workspace.SetActiveReport(report);

        var elementId = Guid.NewGuid();

        // Simulates PreviewViewModel.SelectBlock: the Preview never keeps its
        // own separate "selected" variable, it writes straight into the
        // shared Workspace state that Contents/Properties also read.
        workspace.SetSelectedReportElement(elementId);

        Assert.Equal(elementId, workspace.SelectedReportElementId);
    }

    [Fact]
    public void SelectingSameElementTwice_DoesNotRaiseWorkspaceChangedAgain()
    {
        // Guards the Contents<->Preview echo case: Preview selects an
        // element, Contents mirrors it and its own TreeView selection event
        // reports the SAME id back — that echo must be a no-op, or the two
        // panels would ping-pong WorkspaceChanged forever.
        var workspace = new Workspace();
        var elementId = Guid.NewGuid();
        workspace.SetSelectedReportElement(elementId);

        var changeCount = 0;
        workspace.WorkspaceChanged += (_, _) => changeCount++;

        workspace.SetSelectedReportElement(elementId);

        Assert.Equal(0, changeCount);
    }

    [Fact]
    public void SelectingDifferentElement_RaisesWorkspaceChangedOnce()
    {
        var workspace = new Workspace();
        workspace.SetSelectedReportElement(Guid.NewGuid());

        var changeCount = 0;
        workspace.WorkspaceChanged += (_, _) => changeCount++;

        workspace.SetSelectedReportElement(Guid.NewGuid());

        Assert.Equal(1, changeCount);
    }
}

/// <summary>Required test 6: an inline heading edit on the Preview design surface commits into the real Domain TextElement, which the shared content pipeline then reflects.</summary>
public class ReportEditingServiceTests
{
    private static (Project Project, Report Report) CreateProjectAndReport()
    {
        var project = new Project { Name = "Test Project" };
        var report = new Report { Name = "Test Report" };
        var page = new Page();
        page.Sections.Add(new Section { Name = "Body", Kind = SectionKind.Body });
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report);
    }

    [Fact]
    public void InlineHeadingEdit_CommitsToDomainAndContentPipeline()
    {
        var (_, report) = CreateProjectAndReport();
        var heading = new TextElement { Name = "Heading", Content = Expression.Literal("Eski Başlık") };
        report.Pages[0].Sections[0].Root.Children.Add(heading);

        var service = new ReportEditingService();
        var result = service.CommitHeadingText(report, heading.Id, "Yeni Başlık");

        Assert.True(result.IsSuccess);
        // The commit mutates the REAL TextElement.Content — the same node
        // ReportContentBuilder/WordExporter read — not a preview-only copy.
        Assert.Equal("Yeni Başlık", heading.Content.Text);
    }

    [Fact]
    public void CommitHeadingText_UnknownElementId_ReturnsFailureWithoutThrowing()
    {
        var (_, report) = CreateProjectAndReport();
        var service = new ReportEditingService();

        var result = service.CommitHeadingText(report, Guid.NewGuid(), "Fark etmez");

        Assert.False(result.IsSuccess);
    }

    /// <summary>Locks in the header/data-identity separation: renaming the DISPLAYED header of a bound table must never change which source column its data resolves from.</summary>
    [Fact]
    public void RenamingDisplayedTableHeader_DoesNotChangeSourceDataResolution()
    {
        var project = new Project { Name = "Test Project" };
        var dataSource = new ExcelDataSource
        {
            Name = "Aircraft",
            Workbook = new Workbook { FileName = "Aircraft.xlsx", SourcePath = "/tmp/Aircraft.xlsx" }
        };
        project.DataSources.Add(dataSource);

        var (_, report) = CreateProjectAndReport();
        var table = new TableElement { Name = "Tablo 1" };
        table.Columns.Add(new TableColumn { Header = "Parça Adı", SourceField = "A" });
        table.Columns.Add(new TableColumn { Header = "Değer", SourceField = "B" });
        table.Binding = new Domain.DataBinding.Binding { DataSourceName = "Aircraft", WorksheetName = "Sheet1" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var service = new ReportEditingService();
        var result = service.RenameDisplayedTableColumn(project, report, table.Id, columnIndex: 0, newHeader: "Yeni Parça Adı");

        Assert.True(result.IsSuccess);
        Assert.Equal("Yeni Parça Adı", table.Columns[0].Header); // display changed...
        Assert.Equal("A", table.Columns[0].SourceField);          // ...source identity is untouched
        Assert.Equal("Değer", table.Columns[1].Header);           // the other column is unaffected
        Assert.Equal("B", table.Columns[1].SourceField);
    }

    /// <summary>A legacy bound table with no TableColumns yet (pre-Sprint-7: headers came straight from DataSource.Fields) materializes columns from the DataSource's fields before renaming, preserving the original field as the source identity.</summary>
    [Fact]
    public void RenamingDisplayedHeader_OnLegacyBoundTableWithNoColumns_MaterializesFromDataSourceFields()
    {
        var project = new Project { Name = "Test Project" };
        var dataSource = new ExcelDataSource
        {
            Name = "Aircraft",
            Workbook = new Workbook { FileName = "Aircraft.xlsx", SourcePath = "/tmp/Aircraft.xlsx" }
        };
        dataSource.ColumnMappings.Add(new ColumnMapping { SourceColumn = "A", TargetField = new Domain.DataBinding.DataField { Name = "PartName", DataType = "Text" } });
        project.DataSources.Add(dataSource);

        var (_, report) = CreateProjectAndReport();
        var table = new TableElement { Name = "Tablo 1" };
        table.Binding = new Domain.DataBinding.Binding { DataSourceName = "Aircraft" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var service = new ReportEditingService();
        var result = service.RenameDisplayedTableColumn(project, report, table.Id, columnIndex: 0, newHeader: "Parça");

        Assert.True(result.IsSuccess);
        Assert.Equal("Parça", table.Columns[0].Header);
        Assert.Equal("PartName", table.Columns[0].SourceField); // identity preserved from DataSource.Fields
    }
}
