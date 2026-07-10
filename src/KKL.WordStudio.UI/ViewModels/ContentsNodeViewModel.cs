namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// A single row in the Contents outline. Deliberately only ever represents
/// a Heading, an Alt Heading ("Subheading"), or a Table — Section/
/// Container/Body are walked through and never surfaced (see
/// ContentsViewModel, which builds this projection from the real report
/// tree without creating a second persisted structure).
/// </summary>
public sealed partial class ContentsNodeViewModel : ViewModelBase
{
    public required Guid ElementId { get; init; }
    public required string DisplayName { get; init; }
    public required ContentsNodeKind Kind { get; init; }

    /// <summary>Only meaningful for Kind == Table.</summary>
    public TableBindingStatus BindingStatus { get; init; } = TableBindingStatus.NotApplicable;

    /// <summary>Short status line shown under a table row, e.g. "Sheet1 · A3:F47", "Not configured", "Source missing".</summary>
    public string? StatusText { get; init; }

    public ObservableCollection<ContentsNodeViewModel> Children { get; } = new();

    /// <summary>
    /// Mirrors Workspace.SelectedReportElementId (Sprint 7 shared selection).
    /// Bound two-way to TreeViewItem.IsSelected: clicking a row updates the
    /// shared state through OnSelected, and a selection made in the Preview
    /// highlights the matching row here.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    public Action? OnSelected { get; init; }
}

public enum ContentsNodeKind { Heading, AltHeading, Table }

public enum TableBindingStatus { NotApplicable, Bound, NotConfigured, SourceMissing }
