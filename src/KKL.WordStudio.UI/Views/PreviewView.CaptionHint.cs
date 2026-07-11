namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KKL.WordStudio.UI.ViewModels;

public partial class PreviewView
{
    private const string EmptyCaptionHintText = "+ Tablo başlığı";
    private const string EmptyCaptionHintToolTip = "Tablo başlığı eklemek için çift tıklayın";

    private Popup? _emptyCaptionHintPopup;
    private FrameworkElement? _emptyCaptionHintTarget;
    private PreviewTablePageBlockViewModel? _emptyCaptionHintBlock;
    private Window? _emptyCaptionHintOwnerWindow;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Unloaded += CaptionHint_Unloaded;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        var host = FindCaptionHintTableHost(e.OriginalSource as DependencyObject);
        if (host?.DataContext is not PreviewTablePageBlockViewModel
            {
                CanEditCaption: true,
                ShowCaptionArea: true,
                HasCaption: false
            } block)
        {
            CloseEmptyCaptionHint();
            return;
        }

        ShowEmptyCaptionHint(host, block);
    }

    private void ShowEmptyCaptionHint(
        FrameworkElement host,
        PreviewTablePageBlockViewModel block)
    {
        var popup = _emptyCaptionHintPopup ??= CreateEmptyCaptionHintPopup();
        if (ReferenceEquals(_emptyCaptionHintTarget, host) && popup.IsOpen)
            return;

        AttachCaptionHintOwnerWindow();
        _emptyCaptionHintTarget = host;
        _emptyCaptionHintBlock = block;
        popup.PlacementTarget = host;
        popup.IsOpen = true;
    }

    private Popup CreateEmptyCaptionHintPopup()
    {
        var text = new TextBlock
        {
            Text = EmptyCaptionHintText,
            FontSize = 8.5d,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x9F, 0xE8)),
            IsHitTestVisible = false
        };
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x7E, 0xA6, 0xE8)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(3d),
            Padding = new Thickness(4d, 1d, 4d, 1d),
            Cursor = Cursors.IBeam,
            ToolTip = EmptyCaptionHintToolTip,
            Child = text
        };
        border.MouseLeftButtonDown += EmptyCaptionHint_MouseLeftButtonDown;

        return new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.Top,
            HorizontalOffset = 4d,
            VerticalOffset = -2d,
            StaysOpen = false,
            Child = border
        };
    }

    private void EmptyCaptionHint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_emptyCaptionHintBlock is not { } block)
            return;

        CloseEmptyCaptionHint();
        _viewModel.BeginTableCaptionEdit(block);
        e.Handled = true;
    }

    private void AttachCaptionHintOwnerWindow()
    {
        var owner = Window.GetWindow(this);
        if (ReferenceEquals(owner, _emptyCaptionHintOwnerWindow))
            return;

        if (_emptyCaptionHintOwnerWindow is not null)
            _emptyCaptionHintOwnerWindow.Deactivated -= CaptionHintOwnerWindow_Deactivated;

        _emptyCaptionHintOwnerWindow = owner;
        if (_emptyCaptionHintOwnerWindow is not null)
            _emptyCaptionHintOwnerWindow.Deactivated += CaptionHintOwnerWindow_Deactivated;
    }

    private void CaptionHintOwnerWindow_Deactivated(object? sender, EventArgs e) =>
        CloseEmptyCaptionHint();

    private void CaptionHint_Unloaded(object sender, RoutedEventArgs e)
    {
        CloseEmptyCaptionHint();
        if (_emptyCaptionHintOwnerWindow is not null)
            _emptyCaptionHintOwnerWindow.Deactivated -= CaptionHintOwnerWindow_Deactivated;
        _emptyCaptionHintOwnerWindow = null;
    }

    private void CloseEmptyCaptionHint()
    {
        if (_emptyCaptionHintPopup is not null)
            _emptyCaptionHintPopup.IsOpen = false;
        _emptyCaptionHintTarget = null;
        _emptyCaptionHintBlock = null;
    }

    private static FrameworkElement? FindCaptionHintTableHost(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is FrameworkElement
                {
                    DataContext: PreviewTablePageBlockViewModel
                } element)
                return element;

            current = current switch
            {
                ContentElement contentElement => ContentOperations.GetParent(contentElement)
                    ?? (contentElement as FrameworkContentElement)?.Parent,
                Visual or Visual3D => VisualTreeHelper.GetParent(current),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }
}
