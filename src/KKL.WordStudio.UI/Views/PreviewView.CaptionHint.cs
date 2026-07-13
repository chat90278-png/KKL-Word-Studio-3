namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KKL.WordStudio.UI.ViewModels;

public partial class PreviewView
{
    private const string EmptyCaptionHintText = "+ Tablo başlığı";
    private const string EmptyCaptionHintToolTip = "Tablo başlığı eklemek için tıklayın";

    private EmptyCaptionHintAdorner? _emptyCaptionHintAdorner;
    private AdornerLayer? _emptyCaptionHintLayer;
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

        var originalSource = e.OriginalSource as DependencyObject;
        if (FindAncestor<EmptyCaptionHintAdorner>(originalSource) is { } adorner
            && ReferenceEquals(adorner, _emptyCaptionHintAdorner))
        {
            return;
        }

        var host = FindCaptionHintTableHost(originalSource);
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

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (e.Handled)
            return;

        var host = FindCaptionHintTableHost(e.OriginalSource as DependencyObject);
        if (host?.DataContext is not PreviewTablePageBlockViewModel
            {
                CanEditCaption: true,
                ShowCaptionArea: true,
                HasCaption: true
            } block)
        {
            return;
        }

        var pointerY = e.GetPosition(host).Y;
        var captionInteractionHeight = Math.Max(24d, block.CaptionLineHeight);
        if (pointerY > captionInteractionHeight)
            return;

        CloseEmptyCaptionHint();
        _viewModel.BeginTableCaptionEdit(block);
        e.Handled = true;
    }

    private void ShowEmptyCaptionHint(
        FrameworkElement host,
        PreviewTablePageBlockViewModel block)
    {
        if (ReferenceEquals(_emptyCaptionHintTarget, host)
            && _emptyCaptionHintAdorner is not null)
        {
            return;
        }

        CloseEmptyCaptionHint();

        var layer = AdornerLayer.GetAdornerLayer(host);
        if (layer is null)
            return;

        AttachCaptionHintOwnerWindow();
        var adorner = new EmptyCaptionHintAdorner(host);
        adorner.HintClicked += EmptyCaptionHint_MouseLeftButtonDown;

        _emptyCaptionHintTarget = host;
        _emptyCaptionHintBlock = block;
        _emptyCaptionHintLayer = layer;
        _emptyCaptionHintAdorner = adorner;
        layer.Add(adorner);
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
        if (_emptyCaptionHintAdorner is not null)
            _emptyCaptionHintAdorner.HintClicked -= EmptyCaptionHint_MouseLeftButtonDown;

        if (_emptyCaptionHintLayer is not null && _emptyCaptionHintAdorner is not null)
            _emptyCaptionHintLayer.Remove(_emptyCaptionHintAdorner);

        _emptyCaptionHintAdorner = null;
        _emptyCaptionHintLayer = null;
        _emptyCaptionHintTarget = null;
        _emptyCaptionHintBlock = null;
    }

    private static FrameworkElement? FindCaptionHintTableHost(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is FrameworkElement
                {
                    Name: "TableBlockHost",
                    DataContext: PreviewTablePageBlockViewModel
                } element)
            {
                return element;
            }

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

    private sealed class EmptyCaptionHintAdorner : Adorner
    {
        private static readonly Point HintOrigin = new(4d, 2d);
        private readonly VisualCollection _visuals;
        private readonly Border _hint;

        public event MouseButtonEventHandler? HintClicked;

        public EmptyCaptionHintAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            var text = new TextBlock
            {
                Text = EmptyCaptionHintText,
                FontSize = 8.5d,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x9F, 0xE8)),
                IsHitTestVisible = false
            };

            _hint = new Border
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
            _hint.MouseLeftButtonDown += (_, e) => HintClicked?.Invoke(this, e);

            _visuals = new VisualCollection(this) { _hint };
        }

        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index) => _visuals[index];

        protected override Size MeasureOverride(Size constraint)
        {
            var size = AdornedElement.RenderSize;
            _hint.Measure(size);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var availableWidth = Math.Max(0d, finalSize.Width - HintOrigin.X);
            var availableHeight = Math.Max(0d, finalSize.Height - HintOrigin.Y);
            var width = Math.Min(_hint.DesiredSize.Width, availableWidth);
            var height = Math.Min(_hint.DesiredSize.Height, availableHeight);
            _hint.Arrange(new Rect(HintOrigin, new Size(width, height)));

            // The hint is now a real child of the table's in-window adorner
            // layer. Explicit clipping guarantees it cannot float over another
            // page, panel, console window or application after the table leaves
            // the visible preview surface.
            Clip = new RectangleGeometry(new Rect(new Point(), finalSize));
            return finalSize;
        }
    }
}
