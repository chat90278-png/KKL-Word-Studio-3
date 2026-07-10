namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.UI.Services;

/// <summary>
/// Drives the Contents tab: projects the real Report tree (Sections ->
/// Container.Children, a flat sequence of Heading/AltHeading/Table
/// elements) into a Heading/Subheading/Table outline. Body/Section/
/// Container are walked through here and never become a
/// ContentsNodeViewModel — this is a pure projection, not a second
/// persisted report structure.
///
/// Also owns the add-element commands (moved here from the old permanent
/// Report Designer panel) and the compact selection summary shown at the
/// bottom of Contents.
/// </summary>
public sealed partial class ContentsViewModel : ViewModelBase
{
    private readonly IWorkspace _workspace;
    private readonly DockViewModel _dock;
    private readonly IReportStructureService _structureService;
    private readonly IDialogService _dialogService;

    /// <summary>Set when the user pressed Escape, so a subsequent focus-loss doesn't re-commit.</summary>
    private bool _renameCancelled;

    public ObservableCollection<ContentsNodeViewModel> RootNodes { get; } = new();

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _selectedTitle = string.Empty;

    [ObservableProperty]
    private string _selectedSubtitle = string.Empty;

    [ObservableProperty]
    private bool _includeTableOfContents;

    /// <summary>True while an inline rename is in progress for the selected node.</summary>
    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    /// <summary>
    /// Compact, non-modal feedback line for the Contents dock: surfaces the
    /// Turkish Result.Error from a rejected structure operation (gap E). Harmless
    /// invalid moves never raise a modal dialog.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ContentsViewModel(IWorkspace workspace, DockViewModel dock, IReportStructureService structureService, IDialogService dialogService)
    {
        _workspace = workspace;
        _dock = dock;
        _structureService = structureService;
        _dialogService = dialogService;
        _workspace.WorkspaceChanged += (_, _) => Rebuild();
        // Sprint 7: content mutations (inline heading edits, direct Excel
        // transfers, Properties applies) announce themselves via the narrower
        // ReportContentChanged event — without this subscription the outline
        // (labels, binding status chips) went stale until the next unrelated
        // workspace change.
        _workspace.ReportContentChanged += (_, _) => Rebuild();
        Rebuild();
    }

    [RelayCommand]
    private void AddHeading() => InsertElement(new TextElement
    {
        Name = "Heading",
        Style = HeadingStylePresets.CreateHeadingStyle(),
        Content = Expression.Literal("Yeni başlık")
    });

    [RelayCommand]
    private void AddAltHeading() => InsertElement(new TextElement
    {
        Name = "Alt Heading",
        Style = HeadingStylePresets.CreateAltHeadingStyle(),
        Content = Expression.Literal("Yeni alt başlık")
    });

    [RelayCommand]
    private void AddTable()
    {
        // "Tablo {n}" numbering matches what the direct Excel transfer
        // creates, so tables from either path share one naming scheme.
        var tableCount = _workspace.ActiveReport is { } activeReport
            ? Domain.Visitors.ReportElementFlattener.Flatten(activeReport).OfType<TableElement>().Count()
            : 0;
        var table = new TableElement { Name = $"Tablo {tableCount + 1}" };
        table.Columns.Add(new TableColumn { Header = "Sütun 1" });
        table.Columns.Add(new TableColumn { Header = "Sütun 2" });
        table.Rows.Add(new TableRow { Kind = TableRowKind.Header });
        table.Rows.Add(new TableRow { Kind = TableRowKind.Detail });
        InsertElement(table);
    }

    [RelayCommand]
    private void AddHeaderText() => InsertElement(
        new TextElement { Name = "Header Text", Content = Expression.Literal("Üst bilgi metni") },
        SectionKind.PageHeader);

    [RelayCommand]
    private void AddFooterText() => InsertElement(
        new TextElement { Name = "Footer Text", Content = Expression.Literal("Alt bilgi metni") },
        SectionKind.PageFooter);

    [RelayCommand]
    private void EditProperties() => _dock.ShowPropertiesCommand.Execute(null);

    // ---------------------------------------------------------------
    // Sprint 12: report structure UX (rename / delete / move / indent /
    // outdent / drag-drop). All product logic lives in
    // IReportStructureService; these commands only route the selected real
    // element Id and re-sync the shared workspace afterwards.
    // ---------------------------------------------------------------

    [RelayCommand]
    private void BeginRename()
    {
        if (_workspace.SelectedReportElementId is not { } id) return;
        RenameText = CurrentDisplayName(id);
        _renameCancelled = false;
        IsRenaming = true;
    }

    [RelayCommand]
    private void CommitRename()
    {
        if (!IsRenaming) return;
        IsRenaming = false;
        // If the user explicitly cancelled (Escape), a trailing focus-loss
        // commit must be ignored.
        if (_renameCancelled) { _renameCancelled = false; return; }

        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;
        ApplyStructureResult(_structureService.Rename(report, id, RenameText), id);
    }

    [RelayCommand]
    private void CancelRename()
    {
        _renameCancelled = true;
        IsRenaming = false;
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;

        // Short Turkish destructive confirmation via the existing UI dialog
        // abstraction — never MessageBox from Application, never product logic
        // in code-behind.
        if (!_dialogService.ShowConfirmation("Seçili öğe silinsin mi?", "Sil"))
            return;

        // Choose a sensible nearby element to select after deletion.
        var nextSelection = NeighborElementId(report, id);
        var result = _structureService.Delete(report, id);
        if (result.IsFailure) { StatusMessage = result.Error!; return; }

        _workspace.SetSelectedReportElement(nextSelection);
        _workspace.NotifyReportContentChanged();
        StatusMessage = string.Empty;
        Rebuild();
    }

    [RelayCommand]
    private void MoveUp()
    {
        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;
        ApplyStructureResult(_structureService.MoveUp(report, id), id);
    }

    [RelayCommand]
    private void MoveDown()
    {
        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;
        ApplyStructureResult(_structureService.MoveDown(report, id), id);
    }

    [RelayCommand]
    private void Indent()
    {
        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;
        ApplyStructureResult(_structureService.Indent(report, id), id);
    }

    [RelayCommand]
    private void Outdent()
    {
        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;
        ApplyStructureResult(_structureService.Outdent(report, id), id);
    }

    /// <summary>
    /// Drag-drop entry point called by ContentsView code-behind, which only
    /// computes source Id, target Id and drop mode from the pointer. All move
    /// semantics live in the structure service.
    /// </summary>
    public void MoveByDragDrop(Guid sourceId, Guid targetId, StructureDropMode mode)
    {
        var report = _workspace.ActiveReport;
        if (report is null) return;
        ApplyStructureResult(_structureService.Move(report, sourceId, targetId, mode), sourceId);
    }

    /// <summary>
    /// Shared post-mutation sync: on success keep the element selected, rebuild
    /// Contents and notify Preview/Word; on failure surface the short Turkish
    /// message non-modally (gap E) and mutate nothing.
    /// </summary>
    private void ApplyStructureResult(KKL.WordStudio.Shared.Results.Result result, Guid elementId)
    {
        if (result.IsFailure) { StatusMessage = result.Error!; return; }
        _workspace.SetSelectedReportElement(elementId);
        _workspace.NotifyReportContentChanged();
        StatusMessage = string.Empty;
        Rebuild();
    }

    private string CurrentDisplayName(Guid id)
    {
        var report = _workspace.ActiveReport;
        if (report is null) return string.Empty;
        return Domain.Visitors.ReportElementFlattener.FindById(report, id) switch
        {
            TextElement text => text.Content.Text,
            TableElement table => table.Name,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Picks a nearby remaining element (previous sibling, else next) in the
    /// owning Body section so selection lands somewhere sensible after delete.
    /// </summary>
    private static Guid? NeighborElementId(Report report, Guid id)
    {
        foreach (var page in report.Pages)
        {
            foreach (var section in page.Sections)
            {
                if (section.Kind is SectionKind.PageHeader or SectionKind.PageFooter) continue;
                var children = section.Root.Children;
                var index = children.FindIndex(e => e.Id == id);
                if (index < 0) continue;
                if (index > 0) return children[index - 1].Id;
                if (index + 1 < children.Count) return children[index + 1].Id;
                return null;
            }
        }
        return null;
    }

    partial void OnIncludeTableOfContentsChanged(bool value)
    {
        var report = _workspace.ActiveReport;
        if (report is null) return;
        report.IncludeTableOfContents = value;
        _workspace.NotifyReportContentChanged();
    }

    /// <summary>The one place that inserts a new element — buttons call this today, a future DnD handler would call it too.</summary>
    private void InsertElement(ReportElement element, SectionKind targetKind = SectionKind.Body)
    {
        var report = _workspace.ActiveReport;
        if (report is null) return;

        var targetSection = report.Pages.SelectMany(p => p.Sections).FirstOrDefault(s => s.Kind == targetKind);
        if (targetSection is null)
        {
            var page = report.Pages.FirstOrDefault();
            if (page is null) return;
            targetSection = new Section { Name = targetKind.ToString(), Kind = targetKind, AutoHeight = targetKind == SectionKind.Body };
            page.Sections.Add(targetSection);
        }

        targetSection.Root.Children.Add(element);
        _workspace.SetSelectedReportElement(element.Id);
        _workspace.NotifyReportContentChanged();
        Rebuild();
    }

    private void Rebuild()
    {
        RootNodes.Clear();

        var project = _workspace.ActiveProject;
        var report = _workspace.ActiveReport;
        UpdateSelectionSummary(project, report);
        if (report is null || project is null) return;

        IncludeTableOfContents = report.IncludeTableOfContents;

        var roots = new List<ContentsNodeViewModel>();
        var stack = new Stack<(int Level, ContentsNodeViewModel Node)>();

        foreach (var page in report.Pages)
        {
            foreach (var section in page.Sections)
            {
                // Contents is the *document* outline — header/footer furniture
                // intentionally doesn't appear here (it's not part of the
                // document body a reader would see as "structure").
                if (section.Kind is SectionKind.PageHeader or SectionKind.PageFooter) continue;

                foreach (var element in section.Root.Children)
                {
                    ContentsNodeViewModel? node = element switch
                    {
                        TextElement text when HeadingStylePresets.IsHeading(text.Style) =>
                            BuildHeadingNode(text, ContentsNodeKind.Heading),
                        TextElement text when HeadingStylePresets.IsAltHeading(text.Style) =>
                            BuildHeadingNode(text, ContentsNodeKind.AltHeading),
                        TableElement table => BuildTableNode(project, table),
                        _ => null
                    };

                    if (node is null) continue;

                    var level = node.Kind switch { ContentsNodeKind.Heading => 1, ContentsNodeKind.AltHeading => 2, _ => 3 };

                    if (level < 3)
                    {
                        while (stack.Count > 0 && stack.Peek().Level >= level) stack.Pop();

                        if (stack.Count == 0) roots.Add(node);
                        else stack.Peek().Node.Children.Add(node);

                        stack.Push((level, node));
                    }
                    else
                    {
                        if (stack.Count == 0) roots.Add(node);
                        else stack.Peek().Node.Children.Add(node);
                    }
                }
            }
        }

        foreach (var root in roots) RootNodes.Add(root);
    }

    private ContentsNodeViewModel BuildHeadingNode(TextElement text, ContentsNodeKind kind) => new()
    {
        ElementId = text.Id,
        DisplayName = text.Content.Text,
        Kind = kind,
        IsSelected = _workspace.SelectedReportElementId == text.Id,
        OnSelected = () => Select(text.Id, text.Content.Text, kind == ContentsNodeKind.Heading ? "Başlık" : "Alt Başlık")
    };

    private ContentsNodeViewModel BuildTableNode(Project project, TableElement table)
    {
        var (status, text) = ResolveBindingStatus(project, table.Binding);
        return new ContentsNodeViewModel
        {
            ElementId = table.Id,
            DisplayName = table.Name,
            Kind = ContentsNodeKind.Table,
            BindingStatus = status,
            StatusText = text,
            IsSelected = _workspace.SelectedReportElementId == table.Id,
            OnSelected = () => Select(table.Id, table.Name, status == TableBindingStatus.Bound ? $"Bağlı: {text}" : text)
        };
    }

    /// <summary>
    /// Resolves whether a table's binding actually works right now — never
    /// just "is DataSourceName non-empty". Checks the DataSource exists,
    /// (for Excel) that its bound worksheet resolves and has a configured
    /// range, and that the backing file is still reachable.
    /// </summary>
    private static (TableBindingStatus Status, string Text) ResolveBindingStatus(Project project, Binding? binding)
    {
        if (binding is null) return (TableBindingStatus.NotConfigured, "Yapılandırılmadı");

        var dataSource = project.DataSources.FirstOrDefault(ds => ds.Name == binding.DataSourceName);
        if (dataSource is null) return (TableBindingStatus.SourceMissing, "Kaynak bulunamadı");

        if (dataSource is ExcelDataSource excelDataSource)
        {
            if (string.IsNullOrWhiteSpace(excelDataSource.Workbook.SourcePath) || !File.Exists(excelDataSource.Workbook.SourcePath))
                return (TableBindingStatus.SourceMissing, "Kaynak bulunamadı");

            var worksheetName = binding.WorksheetName ?? excelDataSource.ActiveWorksheetName;
            var worksheet = excelDataSource.Workbook.Worksheets.FirstOrDefault(w => w.Name == worksheetName);
            if (worksheet?.SelectedRange is null)
                return (TableBindingStatus.SourceMissing, "Kaynak bulunamadı");

            return (TableBindingStatus.Bound, $"{worksheetName} · {worksheet.SelectedRange.RangeReference}");
        }

        return (TableBindingStatus.Bound, dataSource.Name);
    }

    private void Select(Guid elementId, string title, string subtitle)
    {
        _workspace.SetSelectedReportElement(elementId);
        SelectedTitle = title;
        SelectedSubtitle = subtitle;
        HasSelection = true;
    }

    private void UpdateSelectionSummary(Project? project, Report? report)
    {
        var elementId = _workspace.SelectedReportElementId;
        if (project is null || report is null || elementId is null)
        {
            HasSelection = false;
            return;
        }

        var element = Domain.Visitors.ReportElementFlattener.FindById(report, elementId.Value);
        switch (element)
        {
            case TableElement table:
                var (status, text) = ResolveBindingStatus(project, table.Binding);
                SelectedTitle = table.Name;
                SelectedSubtitle = status == TableBindingStatus.Bound ? $"Bağlı: {text}" : text;
                HasSelection = true;
                break;
            case TextElement text2:
                SelectedTitle = text2.Content.Text;
                SelectedSubtitle = HeadingStylePresets.IsHeading(text2.Style) ? "Başlık" : "Alt Başlık";
                HasSelection = true;
                break;
            default:
                HasSelection = false;
                break;
        }
    }
}
