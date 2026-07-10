namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Editing;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Importing;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.UI.Services;

/// <summary>
/// Sprint 14 WPF document-surface ViewModel. It consumes Layout.Pages as-is:
/// page flow, page breaks and table fragmentation remain Engine-owned. This
/// layer only projects millimeter geometry to WPF DIPs and routes gestures to
/// the existing shared Workspace/editing/structure services.
/// </summary>
public sealed partial class PreviewViewModel : ViewModelBase
{
    private readonly IWorkspace _workspace;
    private readonly IReportPreviewRenderer _renderer;
    private readonly IReportEditingService _editingService;
    private readonly IFrontMatterDocumentService _frontMatterService;
    private readonly IReferenceFormatDocumentService _referenceFormatService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IReportStructureService _structureService;
    private readonly IDialogService _dialogService;

    private PreviewPageBlockViewModel? _dropIndicatorBlock;
    private int _refreshGeneration;

    public ObservableCollection<PreviewPageViewModel> Pages { get; } = new();

    [ObservableProperty] private bool _hasPages;
    [ObservableProperty] private double _widestPageWidth = 210 * PreviewPageProjection.MillimetersToDips;
    [ObservableProperty] private double _tallestPageHeight = 297 * PreviewPageProjection.MillimetersToDips;
    [ObservableProperty] private string _layoutWarningsText = string.Empty;
    [ObservableProperty] private string _interactionStatusText = string.Empty;

    // ---- Sprint 8 front matter source workflow, now rendered as real layout pages ----
    [ObservableProperty] private bool _hasFrontMatter;
    [ObservableProperty] private bool _isFrontMatterMissing;
    [ObservableProperty] private bool _isFrontMatterDropActive;
    [ObservableProperty] private string _frontMatterFileName = string.Empty;
    [ObservableProperty] private string _frontMatterStatusText = string.Empty;

    // ---- Sprint 16 reference-format source workflow ----
    [ObservableProperty] private bool _hasReferenceFormat;
    [ObservableProperty] private bool _isReferenceFormatMissing;
    [ObservableProperty] private string _referenceFormatFileName = string.Empty;
    [ObservableProperty] private string _referenceFormatStatusText = string.Empty;

    // ---- Zoom ----
    [ObservableProperty] private PreviewZoomOption _zoomOption = PreviewZoomOption.FitWidth;
    [ObservableProperty] private double _zoomFactor = 1.0;
    [ObservableProperty] private double _viewportWidth = 600;
    [ObservableProperty] private double _viewportHeight = 800;

    public string FrontMatterActionText => HasFrontMatter ? "Ön Belgeyi Değiştir" : "Ön Belge Ekle";
    public string ReferenceFormatActionText => HasReferenceFormat ? "Biçim Şablonunu Değiştir" : "Biçim Şablonu Ekle";
    public string PageCountText => HasPages ? $"{Pages.Count} sayfa" : "Sayfa yok";
    public string SurfaceStatusText => !string.IsNullOrWhiteSpace(InteractionStatusText)
        ? InteractionStatusText
        : !string.IsNullOrWhiteSpace(LayoutWarningsText)
            ? $"Önizleme uyarısı: {LayoutWarningsText}"
            : !string.IsNullOrWhiteSpace(ReferenceFormatStatusText)
                ? ReferenceFormatStatusText
                : FrontMatterStatusText;

    public PreviewViewModel(
        IWorkspace workspace,
        IReportPreviewRenderer renderer,
        IReportEditingService editingService,
        IFrontMatterDocumentService frontMatterService,
        IReferenceFormatDocumentService referenceFormatService,
        IFileDialogService fileDialogService,
        IReportStructureService structureService,
        IDialogService dialogService)
    {
        _workspace = workspace;
        _renderer = renderer;
        _editingService = editingService;
        _frontMatterService = frontMatterService;
        _referenceFormatService = referenceFormatService;
        _fileDialogService = fileDialogService;
        _structureService = structureService;
        _dialogService = dialogService;

        _workspace.ReportContentChanged += (_, _) => _ = RefreshAsync();
        _workspace.WorkspaceChanged += (_, _) => SyncSelection();
        _ = RefreshAsync();
    }

    partial void OnZoomOptionChanged(PreviewZoomOption value) => RecomputeZoom();
    partial void OnViewportWidthChanged(double value) => RecomputeZoom();
    partial void OnViewportHeightChanged(double value) => RecomputeZoom();
    partial void OnWidestPageWidthChanged(double value) => RecomputeZoom();
    partial void OnTallestPageHeightChanged(double value) => RecomputeZoom();
    partial void OnHasFrontMatterChanged(bool value) => OnPropertyChanged(nameof(FrontMatterActionText));
    partial void OnHasReferenceFormatChanged(bool value) => OnPropertyChanged(nameof(ReferenceFormatActionText));
    partial void OnInteractionStatusTextChanged(string value) => OnPropertyChanged(nameof(SurfaceStatusText));
    partial void OnLayoutWarningsTextChanged(string value) => OnPropertyChanged(nameof(SurfaceStatusText));
    partial void OnFrontMatterStatusTextChanged(string value) => OnPropertyChanged(nameof(SurfaceStatusText));
    partial void OnReferenceFormatStatusTextChanged(string value) => OnPropertyChanged(nameof(SurfaceStatusText));

    private void RecomputeZoom() =>
        ZoomFactor = PreviewInteractionHelpers.CalculateZoom(
            ZoomOption,
            ViewportWidth,
            ViewportHeight,
            WidestPageWidth,
            TallestPageHeight);

    // ---------------------------------------------------------------
    // Shared selection / inline editing
    // ---------------------------------------------------------------

    public void SelectBlock(PreviewPageBlockViewModel block)
    {
        if (!block.CanInteract || block.ElementId is not { } elementId)
            return;

        _workspace.SetSelectedReportElement(elementId);
    }

    public void BeginTextEdit(PreviewTextPageBlockViewModel block)
    {
        if (!block.CanInteract)
            return;

        SelectBlock(block);
        block.EditText = GetSemanticTextForElement(block.ElementId!.Value);
        block.IsEditing = true;
    }

    public void CommitTextEdit(PreviewTextPageBlockViewModel block)
    {
        if (!block.IsEditing)
            return;

        block.IsEditing = false;
        var report = _workspace.ActiveReport;
        if (report is null || block.ElementId is not { } elementId)
            return;

        var semanticText = GetSemanticTextForElement(elementId);
        if (string.Equals(block.EditText, semanticText, StringComparison.Ordinal))
            return;

        var result = _editingService.CommitHeadingText(report, elementId, block.EditText);
        if (result.IsFailure)
        {
            InteractionStatusText = result.Error!;
            return;
        }

        InteractionStatusText = string.Empty;
        _workspace.NotifyReportContentChanged();
    }

    public void CancelTextEdit(PreviewTextPageBlockViewModel block) => block.IsEditing = false;

    private string GetSemanticTextForElement(Guid elementId) =>
        string.Concat(
            Pages
                .SelectMany(page => page.Blocks)
                .OfType<PreviewTextPageBlockViewModel>()
                .Where(block => block.ElementId == elementId)
                .GroupBy(block => block.FragmentIndex)
                .OrderBy(group => group.Key)
                .Select(group => group.First().PlainText));

    public void BeginTableCaptionEdit(PreviewTablePageBlockViewModel block)
    {
        if (!block.CanEditCaption)
            return;

        SelectBlock(block);
        block.CaptionEditText = block.Caption ?? string.Empty;
        block.IsCaptionEditing = true;
    }

    public void CommitTableCaptionEdit(PreviewTablePageBlockViewModel block)
    {
        if (!block.IsCaptionEditing)
            return;

        block.IsCaptionEditing = false;
        var report = _workspace.ActiveReport;
        if (report is null || block.ElementId is not { } elementId
            || string.Equals(block.CaptionEditText, block.Caption ?? string.Empty, StringComparison.Ordinal))
            return;

        var result = _editingService.CommitTableCaption(report, elementId, block.CaptionEditText);
        if (result.IsFailure)
        {
            InteractionStatusText = result.Error!;
            return;
        }

        InteractionStatusText = string.Empty;
        _workspace.NotifyReportContentChanged();
    }

    public void CancelTableCaptionEdit(PreviewTablePageBlockViewModel block) => block.IsCaptionEditing = false;

    public void BeginTableHeaderEdit(PreviewTablePageColumnViewModel column)
    {
        if (!column.IsEditable || column.TableElementId is null)
            return;

        _workspace.SetSelectedReportElement(column.TableElementId.Value);
        column.EditText = column.Header;
        column.IsEditing = true;
    }

    public void CommitTableHeaderEdit(PreviewTablePageColumnViewModel column)
    {
        if (!column.IsEditing)
            return;

        column.IsEditing = false;
        var project = _workspace.ActiveProject;
        var report = _workspace.ActiveReport;
        if (project is null || report is null || column.TableElementId is not { } tableElementId || column.EditText == column.Header)
            return;

        var result = _editingService.RenameDisplayedTableColumn(project, report, tableElementId, column.Index, column.EditText);
        if (result.IsFailure)
        {
            InteractionStatusText = result.Error!;
            return;
        }

        InteractionStatusText = string.Empty;
        _workspace.NotifyReportContentChanged();
    }

    public void CancelTableHeaderEdit(PreviewTablePageColumnViewModel column) => column.IsEditing = false;

    private void SyncSelection()
    {
        var selectedId = _workspace.SelectedReportElementId;
        foreach (var block in Pages.SelectMany(page => page.Blocks))
            block.IsSelected = block.CanInteract && block.ElementId == selectedId;
    }

    // ---------------------------------------------------------------
    // Preview delete / report structure move
    // ---------------------------------------------------------------

    public void DeleteSelectedPreviewElement()
    {
        var selectedId = _workspace.SelectedReportElementId;
        var block = Pages.SelectMany(page => page.Blocks)
            .FirstOrDefault(candidate => candidate.CanStructureInteract && candidate.ElementId == selectedId);

        if (block is not null)
            DeletePreviewElement(block);
    }

    public void DeletePreviewElement(PreviewPageBlockViewModel block)
    {
        var report = _workspace.ActiveReport;
        if (report is null || !block.CanStructureInteract || block.ElementId is not { } elementId)
            return;

        if (!_dialogService.ShowConfirmation("Seçili öğe silinsin mi?", "Sil"))
            return;

        var nextSelection = NeighborEditableElementId(elementId);
        var result = _structureService.Delete(report, elementId);
        if (result.IsFailure)
        {
            InteractionStatusText = result.Error!;
            return;
        }

        _workspace.SetSelectedReportElement(nextSelection);
        InteractionStatusText = string.Empty;
        _workspace.NotifyReportContentChanged();
    }

    public void MoveByDragDrop(Guid sourceElementId, Guid targetElementId, StructureDropMode mode)
    {
        var report = _workspace.ActiveReport;
        if (report is null || sourceElementId == targetElementId)
            return;

        var result = _structureService.Move(report, sourceElementId, targetElementId, mode);
        if (result.IsFailure)
        {
            InteractionStatusText = result.Error!;
            return;
        }

        _workspace.SetSelectedReportElement(sourceElementId);
        InteractionStatusText = string.Empty;
        _workspace.NotifyReportContentChanged();
    }

    public void SetDropIndicator(PreviewPageBlockViewModel block, StructureDropMode mode)
    {
        if (!ReferenceEquals(_dropIndicatorBlock, block))
        {
            ClearDropIndicator();
            _dropIndicatorBlock = block;
        }

        block.DropIndicator = PreviewInteractionHelpers.ToIndicator(mode);
    }

    public void ClearDropIndicator()
    {
        if (_dropIndicatorBlock is not null)
            _dropIndicatorBlock.DropIndicator = PreviewDropIndicator.None;

        _dropIndicatorBlock = null;
    }

    private Guid? NeighborEditableElementId(Guid elementId)
    {
        var orderedIds = Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.CanStructureInteract && block.ElementId.HasValue)
            .Select(block => block.ElementId!.Value)
            .Distinct()
            .ToList();

        var index = orderedIds.IndexOf(elementId);
        if (index < 0)
            return null;
        if (index > 0)
            return orderedIds[index - 1];
        return index + 1 < orderedIds.Count ? orderedIds[index + 1] : null;
    }

    // ---------------------------------------------------------------
    // Front matter source workflow
    // ---------------------------------------------------------------

    [RelayCommand]
    private void AddFrontMatter()
    {
        var filePath = _fileDialogService.OpenWordDocument();
        if (filePath is not null)
            ImportFrontMatterFromPath(filePath, null);
    }

    [RelayCommand]
    private void RemoveFrontMatter()
    {
        var project = _workspace.ActiveProject;
        if (project is null)
            return;

        project.FrontMatter = null;
        FrontMatterStatusText = "Ön belge kaldırıldı.";
        _workspace.NotifyReportContentChanged();
    }

    public void SetFrontMatterDropActive(bool isActive) => IsFrontMatterDropActive = isActive;

    public void HandleDroppedFrontMatterFiles(IReadOnlyList<string> filePaths)
    {
        IsFrontMatterDropActive = false;
        var decision = SourceFileDropValidator.EvaluateFrontMatterDrop(filePaths);
        if (!decision.IsAccepted || decision.FilePath is null)
        {
            FrontMatterStatusText = decision.Message ?? "Ön belge bırakılamadı.";
            return;
        }

        ImportFrontMatterFromPath(decision.FilePath, decision.Message);
    }

    private void ImportFrontMatterFromPath(string filePath, string? dropMessage)
    {
        var project = _workspace.ActiveProject;
        if (project is null)
        {
            FrontMatterStatusText = "Etkin proje yok — önce bir proje oluşturun veya açın.";
            return;
        }

        var result = _frontMatterService.Import(filePath);
        if (result.IsFailure)
        {
            FrontMatterStatusText = result.Error!;
            return;
        }

        project.FrontMatter = result.Value;
        FrontMatterStatusText = dropMessage is null
            ? $"'{result.Value.FileName}' ön belge olarak eklendi."
            : $"'{result.Value.FileName}' ön belge olarak eklendi. {dropMessage}";
        _workspace.NotifyReportContentChanged();
    }

    private void RefreshFrontMatterState()
    {
        var frontMatter = _workspace.ActiveProject?.FrontMatter;
        HasFrontMatter = frontMatter is not null;
        FrontMatterFileName = frontMatter?.FileName ?? string.Empty;
        IsFrontMatterMissing = frontMatter is not null && !_frontMatterService.IsAvailable(frontMatter);

        if (frontMatter is not null)
        {
            FrontMatterStatusText = IsFrontMatterMissing
                ? "Ön belge bulunamadı"
                : $"Ön belge: {frontMatter.FileName}";
        }
        else if (FrontMatterStatusText.StartsWith("Ön belge:", StringComparison.Ordinal)
                 || FrontMatterStatusText == "Ön belge bulunamadı")
        {
            FrontMatterStatusText = string.Empty;
        }
    }

    // ---------------------------------------------------------------
    // Reference-format source workflow
    // ---------------------------------------------------------------

    [RelayCommand]
    private void AddReferenceFormat()
    {
        var filePath = ReferenceFormatFilePicker.PickDocx();
        if (filePath is null)
            return;

        var project = _workspace.ActiveProject;
        if (project is null)
        {
            ReferenceFormatStatusText = "Etkin proje yok — önce bir proje oluşturun veya açın.";
            return;
        }

        var result = _referenceFormatService.Import(filePath);
        if (result.IsFailure)
        {
            ReferenceFormatStatusText = result.Error!;
            return;
        }

        project.ReferenceFormat = result.Value;
        ReferenceFormatStatusText = $"Biçim şablonu: {result.Value.FileName}";
        RefreshReferenceFormatState();
        _workspace.NotifyReportContentChanged();
    }

    private void RefreshReferenceFormatState()
    {
        var referenceFormat = _workspace.ActiveProject?.ReferenceFormat;
        HasReferenceFormat = referenceFormat is not null;
        ReferenceFormatFileName = referenceFormat?.FileName ?? string.Empty;
        IsReferenceFormatMissing = referenceFormat is not null && !_referenceFormatService.IsAvailable(referenceFormat);

        if (referenceFormat is not null)
        {
            ReferenceFormatStatusText = IsReferenceFormatMissing
                ? "Biçim şablonu bulunamadı"
                : $"Biçim şablonu: {referenceFormat.FileName}";
        }
        else if (ReferenceFormatStatusText.StartsWith("Biçim şablonu:", StringComparison.Ordinal)
                 || ReferenceFormatStatusText == "Biçim şablonu bulunamadı")
        {
            ReferenceFormatStatusText = string.Empty;
        }
    }

    // ---------------------------------------------------------------
    // Layout projection
    // ---------------------------------------------------------------

    private async Task RefreshAsync()
    {
        var generation = Interlocked.Increment(ref _refreshGeneration);
        var project = _workspace.ActiveProject;
        var report = _workspace.ActiveReport;

        RefreshFrontMatterState();
        RefreshReferenceFormatState();

        if (project is null || report is null)
        {
            Pages.Clear();
            HasPages = false;
            LayoutWarningsText = string.Empty;
            OnPropertyChanged(nameof(PageCountText));
            RecomputeZoom();
            return;
        }

        try
        {
            var snapshot = await _renderer.RenderAsync(project, report);
            if (generation != _refreshGeneration)
                return;

            var projectedPages = snapshot.Layout.Pages
                .Select(PreviewPageProjection.Project)
                .ToList();

            Pages.Clear();
            foreach (var page in projectedPages)
                Pages.Add(page);

            HasPages = Pages.Count > 0;
            WidestPageWidth = Pages.Count == 0
                ? 210 * PreviewPageProjection.MillimetersToDips
                : Pages.Max(page => page.Width);
            TallestPageHeight = Pages.Count == 0
                ? 297 * PreviewPageProjection.MillimetersToDips
                : Pages.Max(page => page.Height);
            LayoutWarningsText = string.Join(" · ", snapshot.Layout.Warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));
            InteractionStatusText = string.Empty;
            OnPropertyChanged(nameof(PageCountText));

            SyncSelection();
            RecomputeZoom();
        }
        catch (Exception ex)
        {
            if (generation != _refreshGeneration)
                return;

            Pages.Clear();
            HasPages = false;
            LayoutWarningsText = string.Empty;
            InteractionStatusText = $"Önizleme oluşturulamadı: {ex.Message}";
            OnPropertyChanged(nameof(PageCountText));
        }
    }
}
