namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;

internal sealed class LayoutPageFlow
{
    private const double BlockGapMillimeters = 2d;
    private readonly List<DocumentPageLayout> _completedPages = [];
    private readonly Action<MutableLayoutPage>? _pageDecorator;
    private readonly DocumentPageOrigin _origin;
    private readonly PageLayout _pageLayout;
    private int _nextPageNumber;
    private MutableLayoutPage _currentPage;
    private ReportContentKind? _lastBodyContentKind;

    public LayoutPageFlow(
        int firstPageNumber,
        DocumentPageOrigin origin,
        PageLayout pageLayout,
        Action<MutableLayoutPage>? pageDecorator = null)
    {
        _nextPageNumber = firstPageNumber;
        _origin = origin;
        _pageLayout = pageLayout;
        _pageDecorator = pageDecorator;
        _currentPage = CreatePage();
    }

    public MutableLayoutPage CurrentPage => _currentPage;
    public double BodyTopMillimeters => Math.Max(0d, _pageLayout.MarginTopMillimeters);
    public double BodyBottomMillimeters => Math.Max(
        BodyTopMillimeters,
        _pageLayout.HeightMillimeters - Math.Max(0d, _pageLayout.MarginBottomMillimeters));
    public double BodyHeightMillimeters => Math.Max(0d, BodyBottomMillimeters - BodyTopMillimeters);
    public double ContentWidthMillimeters => Math.Max(
        1d,
        _pageLayout.WidthMillimeters
        - Math.Max(0d, _pageLayout.MarginLeftMillimeters)
        - Math.Max(0d, _pageLayout.MarginRightMillimeters));
    public double BodyYMillimeters => _currentPage.BodyYMillimeters;
    public double RemainingBodyHeightMillimeters => Math.Max(0d, BodyBottomMillimeters - BodyYMillimeters);
    public bool IsAtBodyTop => Math.Abs(BodyYMillimeters - BodyTopMillimeters) < 0.0001d;

    public void AddBodyBlock(PositionedPageBlock block, bool addGapAfter = true)
    {
        ArgumentNullException.ThrowIfNull(block);

        var currentKind = ResolveContentKind(block);
        var currentPageAlreadyHasBodyContent = _currentPage.Blocks.Any(existing =>
            existing.Region == DocumentPageRegion.Body);
        if (block.FragmentIndex == 0
            && ReportFlowPaginationPolicy.StartsNewPageAfterTable(_lastBodyContentKind, currentKind)
            && currentPageAlreadyHasBodyContent)
        {
            NewPage();
            block = RebaseAtCurrentBodyTop(block);
        }

        _currentPage.Blocks.Add(block);
        _currentPage.BodyYMillimeters = block.YMillimeters + block.HeightMillimeters;
        if (addGapAfter)
            _currentPage.BodyYMillimeters += BlockGapMillimeters;

        if (currentKind is not null)
            _lastBodyContentKind = currentKind;
    }

    public void AdvanceBody(double millimeters)
    {
        if (millimeters > 0d)
            _currentPage.BodyYMillimeters += millimeters;
    }

    public void NewPage()
    {
        CompleteCurrentPage();
        _currentPage = CreatePage();
    }

    public IReadOnlyList<DocumentPageLayout> Complete()
    {
        CompleteCurrentPage();
        return _completedPages;
    }

    private PositionedPageBlock RebaseAtCurrentBodyTop(PositionedPageBlock block) => new()
    {
        ElementId = block.ElementId,
        Region = block.Region,
        Kind = block.Kind,
        XMillimeters = block.XMillimeters,
        YMillimeters = BodyYMillimeters,
        WidthMillimeters = block.WidthMillimeters,
        HeightMillimeters = block.HeightMillimeters,
        FragmentIndex = block.FragmentIndex,
        IsContinuation = block.IsContinuation,
        IsEditableReportElement = block.IsEditableReportElement,
        Payload = block.Payload
    };

    private static ReportContentKind? ResolveContentKind(PositionedPageBlock block) => block switch
    {
        { Kind: PageBlockKind.Table } => ReportContentKind.Table,
        { Kind: PageBlockKind.Text, Payload: TextPageBlockPayload text } => text.SemanticKind,
        { Kind: PageBlockKind.Image } => ReportContentKind.Image,
        _ => null
    };

    private MutableLayoutPage CreatePage()
    {
        var page = new MutableLayoutPage(
            _nextPageNumber++,
            _origin,
            _pageLayout,
            BodyTopMillimeters);
        _pageDecorator?.Invoke(page);
        return page;
    }

    private void CompleteCurrentPage()
    {
        _completedPages.Add(new DocumentPageLayout
        {
            PageNumber = _currentPage.PageNumber,
            Origin = _currentPage.Origin,
            PageLayout = _currentPage.PageLayout,
            Blocks = _currentPage.Blocks.ToList()
        });
    }
}

internal sealed class MutableLayoutPage
{
    public MutableLayoutPage(
        int pageNumber,
        DocumentPageOrigin origin,
        PageLayout pageLayout,
        double bodyYMillimeters)
    {
        PageNumber = pageNumber;
        Origin = origin;
        PageLayout = pageLayout;
        BodyYMillimeters = bodyYMillimeters;
    }

    public int PageNumber { get; }
    public DocumentPageOrigin Origin { get; }
    public PageLayout PageLayout { get; }
    public List<PositionedPageBlock> Blocks { get; } = [];
    public double BodyYMillimeters { get; set; }
}
