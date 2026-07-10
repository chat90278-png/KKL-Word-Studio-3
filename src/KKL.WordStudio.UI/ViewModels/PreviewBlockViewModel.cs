namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using KKL.WordStudio.Application.Content;

/// <summary>
/// Base type for a rendered preview block. Split into Text/Table (Sprint 6)
/// so WPF's ItemsControl can pick a real DataTemplate per type. Sprint 7
/// turns these blocks into the interactive design surface: every block
/// carries the REAL source ReportElement's Id (preserved end-to-end through
/// ReportContentNode → PreviewBlock — no text matching, no index guessing)
/// plus a selection flag mirrored from the shared Workspace selection state.
/// </summary>
public abstract partial class PreviewBlockViewModel : ViewModelBase
{
    public required Guid ElementId { get; init; }

    /// <summary>Mirrors Workspace.SelectedReportElementId — set by PreviewViewModel.SyncSelection, never a second independent selection state.</summary>
    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class TextPreviewBlockViewModel : PreviewBlockViewModel
{
    public required ReportContentKind Kind { get; init; }
    public required string Text { get; init; }

    /// <summary>True while the inline editor (double-click) is open for this block.</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>The editor's working text; committed to the Domain TextElement on Enter/focus loss, discarded on Escape.</summary>
    [ObservableProperty]
    private string _editText = string.Empty;
}
