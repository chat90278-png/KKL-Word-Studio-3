namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Visitors;

public sealed partial class ContentsViewModel
{
    private StructuredContentsCollection? _structuredRootNodes;

    /// <summary>
    /// UI-only projection that keeps the fixed document root as the one visible
    /// top-level node. The underlying report and RootNodes remain the existing
    /// real flat-order projection; no second persisted report tree is created.
    /// </summary>
    public ObservableCollection<ContentsNodeViewModel> StructuredRootNodes =>
        _structuredRootNodes ??= new StructuredContentsCollection(RootNodes);

    [RelayCommand]
    private void AddStructuredHeading()
    {
        var report = _workspace.ActiveReport;
        if (report is null) return;

        var heading = new TextElement
        {
            Name = "Heading",
            Content = Expression.Literal("Yeni başlık")
        };
        var result = ReportDocumentStructurePolicy.InsertHeading(
            report,
            _workspace.SelectedReportElementId,
            heading);
        ApplyStructureResult(result, heading.Id);
    }

    [RelayCommand]
    private void AddStructuredAltHeading()
    {
        var report = _workspace.ActiveReport;
        if (report is null) return;

        var heading = new TextElement
        {
            Name = "Alt Heading",
            Content = Expression.Literal("Yeni alt başlık")
        };
        var result = ReportDocumentStructurePolicy.InsertAltHeading(
            report,
            _workspace.SelectedReportElementId,
            heading);
        ApplyStructureResult(result, heading.Id);
    }

    [RelayCommand]
    private void AddStructuredTable()
    {
        var report = _workspace.ActiveReport;
        if (report is null) return;

        var tableCount = ReportElementFlattener.Flatten(report).OfType<TableElement>().Count();
        var table = new TableElement { Name = $"Tablo {tableCount + 1}" };
        table.Columns.Add(new TableColumn { Header = "Sütun 1" });
        table.Columns.Add(new TableColumn { Header = "Sütun 2" });
        table.Rows.Add(new TableRow { Kind = TableRowKind.Header });
        table.Rows.Add(new TableRow { Kind = TableRowKind.Detail });

        var result = ReportDocumentStructurePolicy.InsertTable(
            report,
            _workspace.SelectedReportElementId,
            table);
        ApplyStructureResult(result, table.Id);
    }

    [RelayCommand]
    private void CommitStructuredRename()
    {
        if (!IsRenaming) return;
        IsRenaming = false;
        if (_renameCancelled)
        {
            _renameCancelled = false;
            return;
        }

        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;
        ApplyStructureResult(
            ReportDocumentStructurePolicy.Rename(_structureService, report, id, RenameText),
            id);
    }

    [RelayCommand]
    private void DeleteStructuredSelected()
    {
        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;

        var element = ReportElementFlattener.FindById(report, id);
        if (ReportDocumentStructurePolicy.IsRoot(element))
        {
            StatusMessage = "Ana başlık silinemez; yalnızca adı değiştirilebilir.";
            return;
        }

        if (!_dialogService.ShowConfirmation("Seçili öğe silinsin mi?", "Sil"))
            return;

        var nextSelection = NeighborElementId(report, id);
        var result = ReportDocumentStructurePolicy.Delete(_structureService, report, id);
        if (result.IsFailure)
        {
            StatusMessage = result.Error!;
            return;
        }

        _workspace.SetSelectedReportElement(nextSelection);
        _workspace.NotifyReportContentChanged();
        StatusMessage = string.Empty;
        Rebuild();
    }

    [RelayCommand]
    private void MoveStructuredUp() => ApplySelectedStructureOperation(
        (report, id) => ReportDocumentStructurePolicy.MoveUp(_structureService, report, id));

    [RelayCommand]
    private void MoveStructuredDown() => ApplySelectedStructureOperation(
        (report, id) => ReportDocumentStructurePolicy.MoveDown(_structureService, report, id));

    [RelayCommand]
    private void IndentStructured() => ApplySelectedStructureOperation(
        (report, id) => ReportDocumentStructurePolicy.Indent(_structureService, report, id));

    [RelayCommand]
    private void OutdentStructured() => ApplySelectedStructureOperation(
        (report, id) => ReportDocumentStructurePolicy.Outdent(_structureService, report, id));

    public void MoveByDragDropV23(Guid sourceId, Guid targetId, StructureDropMode mode)
    {
        var report = _workspace.ActiveReport;
        if (report is null) return;

        ApplyStructureResult(
            ReportDocumentStructurePolicy.Move(
                _structureService,
                report,
                sourceId,
                targetId,
                mode),
            sourceId);
    }

    private void ApplySelectedStructureOperation(
        Func<Domain.Reports.Report, Guid, Shared.Results.Result> operation)
    {
        var report = _workspace.ActiveReport;
        if (report is null || _workspace.SelectedReportElementId is not { } id) return;
        ApplyStructureResult(operation(report, id), id);
    }
}

/// <summary>
/// Re-shapes the existing Contents projection for display only: the first node
/// is the fixed root and all other top-level heading blocks are shown beneath it.
/// It listens to the source ObservableCollection and rebuilds lightweight node
/// clones, so ContentsViewModel's established Rebuild lifecycle stays intact.
/// </summary>
internal sealed class StructuredContentsCollection : ObservableCollection<ContentsNodeViewModel>
{
    private readonly ObservableCollection<ContentsNodeViewModel> _source;

    public StructuredContentsCollection(ObservableCollection<ContentsNodeViewModel> source)
    {
        _source = source;
        _source.CollectionChanged += SourceCollectionChanged;
        Rebuild();
    }

    private void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        Clear();
        if (_source.Count == 0) return;

        var root = Clone(_source[0]);
        foreach (var sibling in _source.Skip(1))
            root.Children.Add(Clone(sibling));
        Add(root);
    }

    private static ContentsNodeViewModel Clone(ContentsNodeViewModel source)
    {
        var clone = new ContentsNodeViewModel
        {
            ElementId = source.ElementId,
            DisplayName = source.DisplayName,
            Kind = source.Kind,
            BindingStatus = source.BindingStatus,
            StatusText = source.StatusText,
            IsSelected = source.IsSelected,
            OnSelected = source.OnSelected
        };

        foreach (var child in source.Children)
            clone.Children.Add(Clone(child));
        return clone;
    }
}
