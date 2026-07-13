namespace KKL.WordStudio.UI.ViewModels;

/// <summary>
/// Session-only presentation policy for the full-shell loading overlay.
/// Heavy work continues to use its existing background/cancellation paths; this
/// class only decides when the blocking visual shield is worth showing.
/// </summary>
public sealed class LongOperationDisplayPolicy
{
    public static LongOperationDisplayPolicy Shared { get; } = new();

    private readonly object _sync = new();
    private bool _initialExcelLoadCompleted;
    private bool _initialWorkbookOpenSeen;
    private int _initialSheetPreviewCount;
    private bool _firstWordTransferInProgress;
    private bool _firstWordTransferCompleted;
    private bool _firstWordTransferPreviewPending;

    private LongOperationDisplayPolicy()
    {
    }

    public bool ShouldShowExcelOverlay(ExcelOverlayStage stage)
    {
        lock (_sync)
        {
            if (_initialExcelLoadCompleted)
                return false;

            switch (stage)
            {
                case ExcelOverlayStage.OpenWorkbook:
                    if (_initialWorkbookOpenSeen)
                    {
                        _initialExcelLoadCompleted = true;
                        return false;
                    }

                    _initialWorkbookOpenSeen = true;
                    return true;

                case ExcelOverlayStage.SheetPreview:
                    _initialSheetPreviewCount++;
                    if (_initialSheetPreviewCount > 1)
                    {
                        _initialExcelLoadCompleted = true;
                        return false;
                    }

                    return true;

                case ExcelOverlayStage.DetectRange:
                    // The first open -> preview -> AutoRange sequence is the
                    // logical initial load. This call remains visible, while all
                    // later sheet changes and edits become non-blocking refreshes.
                    _initialExcelLoadCompleted = true;
                    return true;

                case ExcelOverlayStage.WorkingData:
                    // WorkingData is normally created after the initial range is
                    // known. Never reopen the full-screen shield for later edits.
                    _initialExcelLoadCompleted = true;
                    return false;

                default:
                    return false;
            }
        }
    }

    public bool TryBeginFirstWordTransfer()
    {
        lock (_sync)
        {
            if (_firstWordTransferCompleted || _firstWordTransferInProgress)
                return false;

            _firstWordTransferInProgress = true;
            return true;
        }
    }

    public void CompleteFirstWordTransfer(bool succeeded)
    {
        lock (_sync)
        {
            _firstWordTransferInProgress = false;
            if (!succeeded)
                return;

            _firstWordTransferCompleted = true;
            _firstWordTransferPreviewPending = true;
        }
    }

    public bool TryConsumeFirstWordTransferPreview()
    {
        lock (_sync)
        {
            if (!_firstWordTransferPreviewPending)
                return false;

            _firstWordTransferPreviewPending = false;
            return true;
        }
    }
}

public enum ExcelOverlayStage
{
    OpenWorkbook,
    SheetPreview,
    DetectRange,
    WorkingData
}
