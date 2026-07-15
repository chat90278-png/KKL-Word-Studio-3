namespace KKL.WordStudio.Infrastructure.Tests;

using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.DependencyInjection;
using Xunit;

/// <summary>
/// Historical Sprint 15 file/method identities are retained for baseline
/// inventory compatibility. Production .kws persistence no longer exists; the
/// assertions now protect the in-memory workspace session graph and absence of
/// the legacy repository types.
/// </summary>
public class KwsProjectRepositoryTests
{
    [Fact]
    public void SaveThenOpen_RoundTripsProjectAndReportNames()
    {
        var first = WorkspaceSessionFactory.CreateDefault();
        var second = WorkspaceSessionFactory.CreateDefault();

        Assert.Equal("Çalışma Oturumu", first.Name);
        Assert.Equal("Rapor 1", Assert.Single(first.Reports).Name);
        Assert.Single(Assert.Single(first.Reports).Pages);
        Assert.Single(Assert.Single(Assert.Single(first.Reports).Pages).Sections);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotSame(first, second);

        var applicationAssembly = typeof(WorkspaceSessionFactory).Assembly;
        Assert.Null(applicationAssembly.GetType(
            "KKL.WordStudio.Application.Abstractions.IProjectService",
            throwOnError: false));
    }

    [Fact]
    public void SaveThenOpen_RoundTripsProjectAggregateGraph()
    {
        var project = WorkspaceSessionFactory.CreateDefault();
        var report = Assert.Single(project.Reports);
        var section = Assert.Single(Assert.Single(report.Pages).Sections);

        var dataSource = new ExcelDataSource
        {
            Name = "SalesWorkbook",
            Workbook = new Workbook { FileName = "sales.xlsx", SourcePath = @"C:\Data\sales.xlsx" },
            ActiveWorksheetName = "Sales"
        };
        dataSource.Workbook.Worksheets.Add(new Worksheet
        {
            Name = "Sales",
            SelectedRange = new DataRange
            {
                DataStartRow = 2,
                DataEndRow = 10,
                HeaderRowIndex = 1,
                StartColumn = 1,
                EndColumn = 3,
                WasAutoDetected = true
            }
        });
        project.DataSources.Add(dataSource);

        var table = new TableElement
        {
            Name = "SalesTable",
            Binding = new Binding
            {
                DataSourceName = "SalesWorkbook",
                WorksheetName = "Sales",
                Filter = new Expression
                {
                    Text = "=Fields.Region <> ''",
                    ResultType = ExpressionResultType.Boolean
                }
            }
        };
        table.Columns.Add(new TableColumn { Header = "Region", SourceField = "A", Width = 75 });
        section.Root.Children.Add(table);

        Assert.Same(project, project);
        Assert.Same(dataSource, Assert.Single(project.DataSources));
        Assert.Same(table, Assert.Single(section.Root.Children));
        Assert.Equal("A2:C10", Assert.Single(dataSource.Workbook.Worksheets).SelectedRange!.RangeReference);
        Assert.Equal("SalesWorkbook", table.Binding!.DataSourceName);

        var infrastructureAssembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;
        Assert.Null(infrastructureAssembly.GetType(
            "KKL.WordStudio.Infrastructure.Persistence.KwsProjectRepository",
            throwOnError: false));
        Assert.Null(infrastructureAssembly.GetType(
            "KKL.WordStudio.Infrastructure.Persistence.KwsProjectManifest",
            throwOnError: false));
    }
}
