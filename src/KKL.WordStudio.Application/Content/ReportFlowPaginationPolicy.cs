namespace KKL.WordStudio.Application.Content;

/// <summary>
/// Shared semantic flow rules consumed by deterministic Preview pagination and
/// the OpenXML Word writer. This class describes document intent only; concrete
/// renderers remain responsible for translating it to measurements or native
/// pagination properties.
/// </summary>
public static class ReportFlowPaginationPolicy
{
    /// <summary>
    /// A table should begin with its header and up to this many meaningful data
    /// rows when the rows fit together on a fresh page.
    /// </summary>
    public const int MinimumTableStartDataRows = 3;

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
    /// Heading semantics and explicitly resolved formatting use one keep-next
    /// decision in Preview and Word.
    /// </summary>
    public static bool KeepsWithNext(TextContentNode text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Format.KeepWithNext || IsHeading(text.Kind);
    }

    /// <summary>
    /// Resolves the final node that must participate in the current block-start
    /// decision. A heading can therefore retain an alt heading and the following
    /// table start as one semantic chain, while ordinary keep-next paragraphs
    /// retain only the immediately following block unless that block also keeps.
    /// </summary>
    public static int ResolveKeepWithNextChainEndIndex(
        IReadOnlyList<ReportContentNode> nodes,
        int startIndex)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (startIndex < 0 || startIndex >= nodes.Count)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        if (nodes[startIndex] is not TextContentNode startText || !KeepsWithNext(startText))
            return startIndex;

        var endIndex = startIndex;
        while (endIndex + 1 < nodes.Count)
        {
            endIndex++;
            var included = nodes[endIndex];
            if (included is TableContentNode)
                break;

            if (included is not TextContentNode includedText || !KeepsWithNext(includedText))
                break;
        }

        return endIndex;
    }

    /// <summary>
    /// Returns the number of data rows required at the start of the remaining
    /// table, naturally shrinking for short tables.
    /// </summary>
    public static int ResolveMinimumTableStartDataRowCount(int remainingRowCount) =>
        Math.Clamp(remainingRowCount, 0, MinimumTableStartDataRows);

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
