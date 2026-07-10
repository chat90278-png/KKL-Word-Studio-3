namespace KKL.WordStudio.UI.ViewModels;

using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;

/// <summary>
/// UI-only projection of one physical page returned by the Sprint 14 layout
/// contract. Geometry is already resolved by Engine; WPF only converts the
/// millimeter coordinates to device-independent pixels and presents them.
/// </summary>
public sealed class PreviewPageViewModel
{
    public required int PageNumber { get; init; }
    public required DocumentPageOrigin Origin { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required IReadOnlyList<PreviewPageBlockViewModel> Blocks { get; init; }

    public string OriginLabel => Origin == DocumentPageOrigin.FrontMatter ? "Ön Belge" : "Rapor";
}

public enum PreviewDropIndicator
{
    None,
    Before,
    Into,
    After
}

/// <summary>
/// Base projection for a positioned layout block. ElementId is deliberately
/// nullable because imported and derived blocks are not ReportElements.
/// </summary>
public abstract partial class PreviewPageBlockViewModel : ViewModelBase
{
    public required Guid? ElementId { get; init; }
    public required DocumentPageRegion Region { get; init; }
    public required PageBlockKind Kind { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required int FragmentIndex { get; init; }
    public required bool IsContinuation { get; init; }
    public required bool IsEditableReportElement { get; init; }

    public bool CanInteract => IsEditableReportElement && ElementId.HasValue;

    public bool CanStructureInteract => CanInteract && Region == DocumentPageRegion.Body;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private PreviewDropIndicator _dropIndicator;
}

public sealed partial class PreviewTextPageBlockViewModel : PreviewPageBlockViewModel
{
    public required IReadOnlyList<PreviewTextRunViewModel> Runs { get; init; }
    public required ReportContentKind? SemanticKind { get; init; }
    public required TextAlignment Alignment { get; init; }
    public required FontFamily? FontFamily { get; init; }
    public required double FontSize { get; init; }
    public required FontWeight FontWeight { get; init; }
    public required FontStyle FontStyle { get; init; }
    public required TextDecorationCollection? TextDecorations { get; init; }
    public required Brush? Foreground { get; init; }
    public required double LineHeight { get; init; }
    public required double FirstLineIndent { get; init; }
    public required string PlainText { get; init; }

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;
}

public sealed class PreviewTextRunViewModel
{
    public required string Text { get; init; }
    public required FontWeight FontWeight { get; init; }
    public required FontStyle FontStyle { get; init; }
    public required TextDecorationCollection? TextDecorations { get; init; }
    public required double FontSize { get; init; }
    public required FontFamily? FontFamily { get; init; }
}

public sealed partial class PreviewTablePageBlockViewModel : PreviewPageBlockViewModel
{
    public required string Name { get; init; }
    public string? Caption { get; init; }
    public ResolvedTextFormat? CaptionFormat { get; init; }
    public required IReadOnlyList<PreviewTextRunViewModel> CaptionRuns { get; init; }
    public required TextAlignment CaptionAlignment { get; init; }
    public required FontFamily? CaptionFontFamily { get; init; }
    public required double CaptionFontSize { get; init; }
    public required FontWeight CaptionFontWeight { get; init; }
    public required FontStyle CaptionFontStyle { get; init; }
    public required TextDecorationCollection? CaptionTextDecorations { get; init; }
    public required Brush? CaptionForeground { get; init; }
    public required double CaptionLineHeight { get; init; }
    public required double CaptionFirstLineIndent { get; init; }
    public required Thickness CaptionAreaMargin { get; init; }
    public required IReadOnlyList<PreviewTablePageColumnViewModel> Columns { get; init; }
    public required IReadOnlyList<PreviewTablePageRowViewModel> Rows { get; init; }
    public required IReadOnlyList<TableCellSpan> CellSpans { get; init; }
    public required ResolvedTableFormat Format { get; init; }
    public required Thickness TableBorderThickness { get; init; }
    public required int StartRowIndex { get; init; }
    public required bool HasHeader { get; init; }
    public required bool IsHeaderRepeated { get; init; }
    public string? SourceError { get; init; }

    public bool HasSourceError => !string.IsNullOrWhiteSpace(SourceError);
    public bool ShowCaptionArea => FragmentIndex == 0;
    public bool CanEditCaption => CanInteract && FragmentIndex == 0;
    public bool HasCaption => !string.IsNullOrWhiteSpace(Caption);
    public string CaptionDisplayText => HasCaption ? Caption! : "Tablo başlığı eklemek için çift tıklayın";
    public string ContinuationText => IsContinuation ? "devam" : string.Empty;

    [ObservableProperty]
    private bool _isCaptionEditing;

    [ObservableProperty]
    private string _captionEditText = string.Empty;
}

public sealed partial class PreviewTablePageColumnViewModel : ViewModelBase
{
    public required Guid? TableElementId { get; init; }
    public required int Index { get; init; }
    public required bool IsEditable { get; init; }
    public required double WidthWeight { get; init; }
    public required TextAlignment HeaderAlignment { get; init; }
    public required FontFamily? HeaderFontFamily { get; init; }
    public required double HeaderFontSize { get; init; }
    public required FontWeight HeaderFontWeight { get; init; }
    public required VerticalAlignment VerticalAlignment { get; init; }
    public required TextWrapping TextWrapping { get; init; }
    public required Thickness CellMargin { get; init; }
    public required double PreferredRowHeight { get; init; }
    public required Thickness CellBorderThickness { get; init; }

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;
}

public sealed class PreviewTablePageRowViewModel
{
    public required IReadOnlyList<string> Cells { get; init; }
}

public sealed class PreviewTocPageBlockViewModel : PreviewPageBlockViewModel
{
    public required IReadOnlyList<PreviewTocEntryViewModel> Entries { get; init; }
}

public sealed class PreviewTocEntryViewModel
{
    public required Guid ElementId { get; init; }
    public required string Text { get; init; }
    public required int Level { get; init; }
    public required int PageNumber { get; init; }
    public double Indent => Math.Max(0, Level - 1) * 16.0;
}

public sealed class PreviewImagePageBlockViewModel : PreviewPageBlockViewModel
{
    public required string Name { get; init; }
    public required ImageSource? ImageSource { get; init; }
    public bool HasImage => ImageSource is not null;
}

public sealed class PreviewPageNumberBlockViewModel : PreviewPageBlockViewModel
{
    public required int PageNumber { get; init; }
}

public sealed class PreviewUnsupportedPageBlockViewModel : PreviewPageBlockViewModel
{
    public required string Description { get; init; }
}
