namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint21ColumnSelectionArchitectureTests
{
    [Fact]
    public void MappingSurface_ProvidesSingleClickCheckboxAndBulkSelectionWithoutDeletingSourceColumns()
    {
        var root = SolutionRootLocator.Find();
        var xaml = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.xaml");
        var row = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ColumnMappingRowViewModel.Selection.cs");
        var viewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.ColumnSelection.cs");

        Assert.Contains("DataGridTemplateColumn Header=\"Aktar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsIncluded, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ClickMode=\"Press\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tümünü seç", xaml, StringComparison.Ordinal);
        Assert.Contains("Hiçbirini seçme", xaml, StringComparison.Ordinal);
        Assert.Contains("ApplyColumnSelectionMappingCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenColumnSelectionMappingDrawerCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("SetProperty(ref _isIncluded", row, StringComparison.Ordinal);
        Assert.Contains("ColumnTransferSelectionSession.Shared", viewModel, StringComparison.Ordinal);
        Assert.Contains("selectedColumns.Count == 0", viewModel, StringComparison.Ordinal);
        Assert.Contains("SetSelection", viewModel, StringComparison.Ordinal);

        Assert.DoesNotContain("Veri kaynağı adı", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding DataSourceName", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteColumns", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingData.Columns.Remove", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Write", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void TransferDecorator_FiltersBothSourceRangeAndWorkingDataThenDelegatesToExistingService()
    {
        var root = SolutionRootLocator.Find();
        var decorator = Read(root, "src", "KKL.WordStudio.Application", "Transfer", "ColumnSelectionExcelReportTransferService.cs");
        var session = Read(root, "src", "KKL.WordStudio.Application", "Transfer", "ColumnTransferSelectionSession.cs");
        var app = Read(root, "src", "KKL.WordStudio.UI", "App.xaml.cs");

        Assert.Contains("request.WorkingDataColumns is { Count: > 0 }", decorator, StringComparison.Ordinal);
        Assert.Contains("BuildSourceRangeProjection", decorator, StringComparison.Ordinal);
        Assert.Contains("FilterWorkingDataColumns", decorator, StringComparison.Ordinal);
        Assert.Contains("return inner.Transfer", decorator, StringComparison.Ordinal);
        Assert.Contains("OriginalSourceColumn", decorator, StringComparison.Ordinal);
        Assert.Contains("ConcurrentDictionary", session, StringComparison.Ordinal);
        Assert.Contains("Runtime-only", session, StringComparison.Ordinal);
        Assert.Contains("ColumnSelectionExcelReportTransferService", app, StringComparison.Ordinal);
        Assert.Contains("IColumnTransferSelectionSession", app, StringComparison.Ordinal);

        Assert.DoesNotContain("Domain.Elements.TableElement", decorator, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", decorator, StringComparison.Ordinal);
        Assert.DoesNotContain("IExcelWorkbookReader", decorator, StringComparison.Ordinal);
    }

    [Fact]
    public void ColumnSelection_DoesNotAddPersistenceToDomain()
    {
        var root = SolutionRootLocator.Find();
        var domainRoot = Path.Combine(root, "src", "KKL.WordStudio.Domain");
        var domainText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("ColumnTransferSelection", domainText, StringComparison.Ordinal);
        Assert.DoesNotContain("IsIncluded", domainText, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
