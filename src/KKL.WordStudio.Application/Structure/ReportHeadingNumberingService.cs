namespace KKL.WordStudio.Application.Structure;

using System.Text.RegularExpressions;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// Applies the visible document-outline numbering used by Contents, Preview and
/// Word. Heading identity remains the existing HeadingStylePresets convention;
/// the service only normalizes the visible literal text and never creates a
/// second report tree.
/// </summary>
public static partial class ReportHeadingNumberingService
{
    public static void Renumber(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var headingNumber = 0;
        var altHeadingNumber = 0;

        foreach (var text in EnumerateBodyElements(report).OfType<TextElement>())
        {
            var isRoot = string.Equals(text.Name, "Document Root", StringComparison.Ordinal);
            var isHeading = HeadingStylePresets.IsHeading(text.Style);
            var isAltHeading = HeadingStylePresets.IsAltHeading(text.Style);
            if (!isRoot && !isHeading && !isAltHeading)
                continue;

            var title = StripVisibleNumber(text.Content.Text);
            if (isRoot)
            {
                text.Style = HeadingStylePresets.CreateHeadingStyle();
                text.Content = Expression.Literal($"1. {title}");
                headingNumber = 0;
                altHeadingNumber = 0;
                continue;
            }

            if (isHeading)
            {
                headingNumber++;
                altHeadingNumber = 0;
                text.Content = Expression.Literal($"1.{headingNumber} {title}");
                continue;
            }

            if (headingNumber == 0)
                headingNumber = 1;
            altHeadingNumber++;
            text.Content = Expression.Literal($"1.{headingNumber}.{altHeadingNumber} {title}");
        }
    }

    public static string StripVisibleNumber(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return VisibleNumberPrefix().Replace(text, string.Empty).Trim();
    }

    private static IEnumerable<ReportElement> EnumerateBodyElements(Report report)
    {
        foreach (var section in report.Pages.SelectMany(page => page.Sections))
        {
            if (section.Kind is SectionKind.PageHeader or SectionKind.PageFooter)
                continue;

            foreach (var element in Enumerate(section.Root))
                yield return element;
        }
    }

    private static IEnumerable<ReportElement> Enumerate(Container container)
    {
        foreach (var child in container.Children)
        {
            yield return child;
            if (child is not Container nested)
                continue;

            foreach (var descendant in Enumerate(nested))
                yield return descendant;
        }
    }

    [GeneratedRegex(@"^\s*\d+(?:\.\d+)*(?:\.)?\s+")]
    private static partial Regex VisibleNumberPrefix();
}
