namespace KKL.WordStudio.Application.Content;

/// <summary>
/// Shared flow rule consumed by both deterministic Preview pagination and the
/// OpenXML Word writer. A heading that immediately follows a table begins on a
/// new page; no blank paragraph or UI-only spacer is introduced.
/// </summary>
public static class ReportFlowPaginationPolicy
{
    public static bool StartsNewPageAfterTable(
        ReportContentKind? previousKind,
        ReportContentKind? currentKind) =>
        previousKind == ReportContentKind.Table
        && currentKind is ReportContentKind.Heading or ReportContentKind.AltHeading;

    public static bool StartsNewPageAfterTable(
        ReportContentNode? previous,
        ReportContentNode current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return StartsNewPageAfterTable(previous?.Kind, current.Kind);
    }
}
