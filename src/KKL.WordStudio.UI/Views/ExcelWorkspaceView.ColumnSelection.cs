namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

public partial class ExcelWorkspaceView
{
    private bool _columnSelectionUiInstalled;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Loaded -= ColumnSelectionUi_Loaded;
        Loaded += ColumnSelectionUi_Loaded;
    }

    private void ColumnSelectionUi_Loaded(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(InstallColumnSelectionUi));

    private void InstallColumnSelectionUi()
    {
        if (_columnSelectionUiInstalled)
            return;

        var descendants = EnumerateVisualDescendants(this).ToList();
        var mappingGrid = descendants.OfType<DataGrid>().FirstOrDefault(grid =>
            string.Equals(
                BindingOperations.GetBinding(grid, ItemsControl.ItemsSourceProperty)?.Path.Path,
                nameof(ViewModels.ExcelWorkspaceViewModel.ColumnMappings),
                StringComparison.Ordinal));
        if (mappingGrid is null)
            return;

        _columnSelectionUiInstalled = true;
        mappingGrid.Columns.Insert(0, new DataGridCheckBoxColumn
        {
            Header = "Aktar",
            Width = new DataGridLength(58),
            Binding = new Binding(nameof(ViewModels.ColumnMappingRowViewModel.IsIncluded))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        });

        if (mappingGrid.Columns.Count >= 4)
        {
            mappingGrid.Columns[1].Width = new DataGridLength(72);
            mappingGrid.Columns[3].Width = new DataGridLength(64);
        }

        ConfigureMappingButtons(descendants);
        ConfigureMappingHeader(descendants);
    }

    private void ConfigureMappingButtons(IReadOnlyList<DependencyObject> descendants)
    {
        var openButton = descendants.OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content as string, "Sütunları Eşle", StringComparison.Ordinal));
        if (openButton is not null)
            openButton.Command = _viewModel.OpenColumnSelectionMappingDrawerCommand;

        var applyButton = descendants.OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content as string, "Eşlemeyi Uygula", StringComparison.Ordinal));
        if (applyButton is null)
            return;

        applyButton.Content = "Seçimi ve Eşlemeyi Uygula";
        applyButton.Command = _viewModel.ApplyColumnSelectionMappingCommand;
        AddBulkSelectionActions(applyButton.Parent as StackPanel);
    }

    private void AddBulkSelectionActions(StackPanel? actionPanel)
    {
        if (actionPanel is null || actionPanel.Children.OfType<Button>().Any(button => button.Tag as string == "ColumnSelectAll"))
            return;

        var miniStyle = TryFindResource("Btn.Mini") as Style;
        actionPanel.Children.Insert(0, new Button
        {
            Tag = "ColumnSelectAll",
            Content = "Tümünü seç",
            Style = miniStyle,
            Command = _viewModel.SelectAllMappingColumnsCommand,
            Margin = new Thickness(0, 0, 6, 0)
        });
        actionPanel.Children.Insert(1, new Button
        {
            Tag = "ColumnSelectNone",
            Content = "Hiçbirini seçme",
            Style = miniStyle,
            Command = _viewModel.ClearMappingColumnSelectionCommand,
            Margin = new Thickness(0, 0, 14, 0)
        });
    }

    private void ConfigureMappingHeader(IReadOnlyList<DependencyObject> descendants)
    {
        var title = descendants.OfType<TextBlock>()
            .FirstOrDefault(text => string.Equals(text.Text, "SÜTUN EŞLEME (İSTEĞE BAĞLI)", StringComparison.Ordinal));
        if (title is null)
            return;

        title.Text = "SÜTUNLARI SEÇ VE EŞLE";
        if (title.Parent is not StackPanel header || header.Children.OfType<TextBlock>().Any(text => text.Tag as string == "ColumnSelectionHelp"))
            return;

        header.Children.Add(new TextBlock
        {
            Tag = "ColumnSelectionHelp",
            Text = "Yalnızca rapora aktarılacak sütunları işaretleyin. Kaynak Excel değişmez.",
            FontSize = 10.5,
            Foreground = TryFindResource("Brush.Text.Secondary") as Brush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
    }

    private static IEnumerable<DependencyObject> EnumerateVisualDescendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in EnumerateVisualDescendants(child))
                yield return descendant;
        }
    }
}
