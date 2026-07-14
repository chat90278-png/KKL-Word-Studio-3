namespace KKL.WordStudio.Application.Content;

/// <summary>
/// Shared semantic flow rules consumed by deterministic Preview pagination and
/// the OpenXML Word writer. This class describes document intent only; concrete
/// renderers remain responsible for translating it to measurements or native
/// pagination properties.
/// </summary>
public static class ReportFlowPaginationPolicy
{
    public static bool IsHeading(ReportContentKind? kind) =>
        kind is ReportContentKind.Heading or ReportContentKind.AltHeading;

    public static bool StartsNewPageAfterTable(
        ReportContentKind? previousKind,
        ReportContentKind? currentKind) =>
        previousKind == ReportContentKind.Table && IsHeading(currentKind);

    public static bool StartsNewPageAfterTable(
        ReportContentNode? previous,
        ReportContentNode current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return StartsNewPageAfterTable(previous?.Kind, current.Kind);
    }

    /// <summary>
    /// Headings form one keep-with-next chain until the first non-heading block.
    /// Preview preflights this whole chain; Word expresses the same intent with
    /// native KeepNext paragraph properties.
    /// </summary>
    public static bool ParticipatesInHeadingChain(ReportContentNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return IsHeading(node.Kind);
    }

    /// <summary>Table captions must remain attached to the table that follows.</summary>
    public static bool KeepTableCaptionWithTable(string? caption) =>
        !string.IsNullOrWhiteSpace(caption);

    /// <summary>
    /// Table rows are atomic in deterministic Preview, so Word rows must not be
    /// split across pages either. Whole tables may still continue on later pages.
    /// </summary>
    public static bool KeepTableRowsIntact => true;
}
