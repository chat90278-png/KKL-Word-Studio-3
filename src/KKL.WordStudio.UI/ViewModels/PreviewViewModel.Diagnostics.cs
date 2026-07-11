namespace KKL.WordStudio.UI.ViewModels;

public sealed partial class PreviewViewModel
{
    public event Action<Guid>? NavigateToElementRequested;

    public void NavigateToElement(Guid elementId)
    {
        _workspace.SetSelectedReportElement(elementId);
        NavigateToElementRequested?.Invoke(elementId);
    }
}
