namespace KKL.WordStudio.UI.Views;

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using KKL.WordStudio.UI.ViewModels;

public partial class LoadedSourcesView : UserControl
{
    public LoadedSourcesView(
        ExcelWorkspaceViewModel excelWorkspaceViewModel,
        QuickAssemblyViewModel quickAssemblyViewModel,
        QuickAssemblyView quickAssemblyView)
    {
        InitializeComponent();
        DataContext = excelWorkspaceViewModel;

        QuickAssemblyHost.Content = quickAssemblyView;
        QuickAssemblyButton.Command = quickAssemblyViewModel.TogglePanelCommand;
        QuickAssemblyPopup.SetBinding(
            Popup.IsOpenProperty,
            new Binding(nameof(QuickAssemblyViewModel.IsOpen))
            {
                Source = quickAssemblyViewModel,
                Mode = BindingMode.TwoWay
            });
    }
}
