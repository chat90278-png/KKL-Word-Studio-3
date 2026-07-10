namespace KKL.WordStudio.UI.Preview;

using System.Windows;
using System.Windows.Controls;
using KKL.WordStudio.Application.Formatting;

/// <summary>
/// ItemsPanel for the editable preview header. It keeps the existing item
/// templates and routed edit gestures while arranging them with the same
/// resolved WidthWeight ratios used by the read-only data grid.
/// </summary>
public sealed class PreviewTableColumnsPanel : Panel
{
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format),
        typeof(ResolvedTableFormat),
        typeof(PreviewTableColumnsPanel),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public ResolvedTableFormat? Format
    {
        get => (ResolvedTableFormat?)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count == 0)
            return new Size();

        var weights = ResolveWeights(InternalChildren.Count);
        var totalWeight = weights.Sum();
        var isFiniteWidth = !double.IsInfinity(availableSize.Width) && !double.IsNaN(availableSize.Width);
        var desiredWidth = 0d;
        var desiredHeight = 0d;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var childWidth = isFiniteWidth
                ? Math.Max(0d, availableSize.Width * weights[index] / totalWeight)
                : double.PositiveInfinity;
            InternalChildren[index].Measure(new Size(childWidth, availableSize.Height));
            desiredWidth += InternalChildren[index].DesiredSize.Width;
            desiredHeight = Math.Max(desiredHeight, InternalChildren[index].DesiredSize.Height);
        }

        return new Size(isFiniteWidth ? availableSize.Width : desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0)
            return finalSize;

        var weights = ResolveWeights(InternalChildren.Count);
        var totalWeight = weights.Sum();
        var x = 0d;
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var width = index == InternalChildren.Count - 1
                ? Math.Max(0d, finalSize.Width - x)
                : Math.Max(0d, finalSize.Width * weights[index] / totalWeight);
            InternalChildren[index].Arrange(new Rect(x, 0d, width, finalSize.Height));
            x += width;
        }

        return finalSize;
    }

    private IReadOnlyList<double> ResolveWeights(int columnCount) =>
        Enumerable.Range(0, columnCount)
            .Select(index => Format is not null
                             && index < Format.Columns.Count
                             && Format.Columns[index].WidthWeight > 0d
                ? Format.Columns[index].WidthWeight
                : 1d)
            .ToArray();
}
