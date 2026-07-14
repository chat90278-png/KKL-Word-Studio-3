namespace KKL.WordStudio.Application.Structure;

using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Sprint 23 document-outline policy layered over the existing flat
/// Section.Root.Children structure. It guarantees one fixed document root,
/// routes contextual insertions, protects the root from destructive operations
/// and re-applies the shared visible numbering after every successful mutation.
/// No second tree or persisted parent relation is introduced.
/// </summary>
public static class ReportDocumentStructurePolicy
{
    public const string RootElementName = "Document Root";
    public const string DefaultRootText = "System Test Procedure Configuration List";

    public static TextElement EnsureRootAndRenumber(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var body = EnsureBodySection(report);
        var roots = body.Root.Children
            .OfType<TextElement>()
            .Where(IsRoot)
            .ToList();

        var root = roots.FirstOrDefault();
        if (root is null)
        {
            root = new TextElement
            {
                Name = RootElementName,
                Style = HeadingStylePresets.CreateHeadingStyle(),
                Content = Expression.Literal(DefaultRootText)
            };
            body.Root.Children.Insert(0, root);
        }

        // Preserve duplicate legacy content rather than deleting it. Only the
        // first root keeps the protected identity; the rest become ordinary H1s.
        foreach (var duplicate in roots.Skip(1))
        {
            duplicate.Name = "Heading";
            duplicate.Style = HeadingStylePresets.CreateHeadingStyle();
        }

        var rootIndex = body.Root.Children.IndexOf(root);
        if (rootIndex > 0)
        {
            body.Root.Children.RemoveAt(rootIndex);
            body.Root.Children.Insert(0, root);
        }

        root.Name = RootElementName;
        root.Style = HeadingStylePresets.CreateHeadingStyle();
        var rootText = ReportHeadingNumberingService.StripVisibleNumber(root.Content.Text);
        root.Content = Expression.Literal(string.IsNullOrWhiteSpace(rootText) ? DefaultRootText : rootText);

        ReportHeadingNumberingService.Renumber(report);
        return root;
    }

    public static bool IsRoot(ReportElement? element) =>
        element is TextElement text
        && string.Equals(text.Name, RootElementName, StringComparison.Ordinal);

    public static Result Rename(IReportStructureService service, Report report, Guid elementId, string newName) =>
        ApplyAndRenumber(report, () => service.Rename(report, elementId, newName));

    public static Result Delete(IReportStructureService service, Report report, Guid elementId)
    {
        EnsureRootAndRenumber(report);
        if (FindTopLevel(report, elementId) is { Element: var element } && IsRoot(element))
            return Result.Failure("Ana başlık silinemez; yalnızca adı değiştirilebilir.");

        return ApplyAndRenumber(report, () => service.Delete(report, elementId));
    }

    public static Result MoveUp(IReportStructureService service, Report report, Guid elementId)
    {
        EnsureRootAndRenumber(report);
        var located = FindTopLevel(report, elementId);
        if (located is null)
            return Result.Failure("Taşınacak öğe bulunamadı.");
        if (IsRoot(located.Value.Element))
            return Result.Failure("Ana başlık taşınamaz.");

        // The first user H1 has no previous H1 sibling. Prevent the legacy
        // service from temporarily moving it before the protected root.
        if (located.Value.Element is TextElement heading
            && HeadingStylePresets.IsHeading(heading.Style)
            && !located.Value.Owner.Children.Take(located.Value.Index)
                .OfType<TextElement>()
                .Any(candidate => HeadingStylePresets.IsHeading(candidate.Style) && !IsRoot(candidate)))
        {
            return Result.Failure("Başlık zaten bu kapsamda en üstte.");
        }

        return ApplyAndRenumber(report, () => service.MoveUp(report, elementId));
    }

    public static Result MoveDown(IReportStructureService service, Report report, Guid elementId)
    {
        EnsureRootAndRenumber(report);
        if (FindTopLevel(report, elementId) is { Element: var element } && IsRoot(element))
            return Result.Failure("Ana başlık taşınamaz.");

        return ApplyAndRenumber(report, () => service.MoveDown(report, elementId));
    }

    public static Result Indent(IReportStructureService service, Report report, Guid elementId)
    {
        EnsureRootAndRenumber(report);
        if (FindTopLevel(report, elementId) is { Element: var element } && IsRoot(element))
            return Result.Failure("Ana başlığın seviyesi değiştirilemez.");

        return ApplyAndRenumber(report, () => service.Indent(report, elementId));
    }

    public static Result Outdent(IReportStructureService service, Report report, Guid elementId)
    {
        EnsureRootAndRenumber(report);
        if (FindTopLevel(report, elementId) is { Element: var element } && IsRoot(element))
            return Result.Failure("Ana başlığın seviyesi değiştirilemez.");

        return ApplyAndRenumber(report, () => service.Outdent(report, elementId));
    }

    public static Result Move(
        IReportStructureService service,
        Report report,
        Guid sourceElementId,
        Guid targetElementId,
        StructureDropMode mode)
    {
        EnsureRootAndRenumber(report);
        var source = FindTopLevel(report, sourceElementId);
        var target = FindTopLevel(report, targetElementId);
        if (source is null || target is null)
            return Result.Failure("Taşınacak öğe veya hedef bulunamadı.");
        if (IsRoot(source.Value.Element))
            return Result.Failure("Ana başlık taşınamaz.");

        if (IsRoot(target.Value.Element))
        {
            if (mode == StructureDropMode.Before)
                return Result.Failure("Ana başlığın önüne öğe bırakılamaz.");

            // Into the fixed root means a normal top-level child, not an H2.
            if (source.Value.Element is TextElement sourceText
                && HeadingStylePresets.IsAltHeading(sourceText.Style))
            {
                sourceText.Style = HeadingStylePresets.CreateHeadingStyle();
            }
            mode = StructureDropMode.After;
        }

        return ApplyAndRenumber(report, () => service.Move(report, sourceElementId, targetElementId, mode));
    }

    public static Result InsertHeading(Report report, Guid? anchorElementId, TextElement heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        var root = EnsureRootAndRenumber(report);
        var body = EnsureBodySection(report);
        heading.Name = "Heading";
        heading.Style = HeadingStylePresets.CreateHeadingStyle();
        heading.Content = Expression.Literal(NormalizeHeadingText(heading.Content.Text, "Yeni başlık"));

        var index = ResolveHeadingInsertIndex(body.Root.Children, anchorElementId, root);
        body.Root.Children.Insert(index, heading);
        ReportHeadingNumberingService.Renumber(report);
        return Result.Success();
    }

    public static Result InsertAltHeading(Report report, Guid? anchorElementId, TextElement heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        var root = EnsureRootAndRenumber(report);
        var body = EnsureBodySection(report);
        var children = body.Root.Children;
        var anchorIndex = anchorElementId is { } id ? children.FindIndex(element => element.Id == id) : -1;

        var useAltStyle = anchorIndex >= 0 && HasOwningUserHeading(children, anchorIndex);
        var index = ResolveAltHeadingInsertIndex(children, anchorIndex, root, useAltStyle);

        heading.Name = useAltStyle ? "Alt Heading" : "Heading";
        heading.Style = useAltStyle
            ? HeadingStylePresets.CreateAltHeadingStyle()
            : HeadingStylePresets.CreateHeadingStyle();
        heading.Content = Expression.Literal(NormalizeHeadingText(heading.Content.Text, "Yeni alt başlık"));
        children.Insert(index, heading);
        ReportHeadingNumberingService.Renumber(report);
        return Result.Success();
    }

    public static Result InsertTable(Report report, Guid? anchorElementId, TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var root = EnsureRootAndRenumber(report);
        var body = EnsureBodySection(report);
        var children = body.Root.Children;
        var index = ResolveContentInsertIndex(children, anchorElementId, root);
        children.Insert(index, table);
        ReportHeadingNumberingService.Renumber(report);
        return Result.Success();
    }

    private static Result ApplyAndRenumber(Report report, Func<Result> mutation)
    {
        EnsureRootAndRenumber(report);
        var result = mutation();
        if (result.IsSuccess)
            EnsureRootAndRenumber(report);
        return result;
    }

    private static int ResolveHeadingInsertIndex(List<ReportElement> children, Guid? anchorId, TextElement root)
    {
        if (anchorId is not { } id)
            return children.Count;

        var index = children.FindIndex(element => element.Id == id);
        if (index < 0 || IsRoot(children[index]))
            return children.Count;

        if (children[index] is TableElement)
            return index;

        if (children[index] is TextElement text && HeadingStylePresets.IsHeading(text.Style))
            return EndOfHeadingBlock(children, index, includeAltBoundary: false);

        if (children[index] is TextElement alt && HeadingStylePresets.IsAltHeading(alt.Style))
        {
            var ownerHeading = FindPreviousUserHeading(children, index);
            return ownerHeading >= 0
                ? EndOfHeadingBlock(children, ownerHeading, includeAltBoundary: false)
                : index;
        }

        return index;
    }

    private static int ResolveAltHeadingInsertIndex(
        List<ReportElement> children,
        int anchorIndex,
        TextElement root,
        bool useAltStyle)
    {
        if (anchorIndex < 0 || IsRoot(children[anchorIndex]))
            return children.Count;

        if (!useAltStyle)
            return children[anchorIndex] is TableElement ? anchorIndex : children.Count;

        if (children[anchorIndex] is TableElement)
            return anchorIndex;

        if (children[anchorIndex] is TextElement text && HeadingStylePresets.IsHeading(text.Style))
            return anchorIndex + 1;

        if (children[anchorIndex] is TextElement alt && HeadingStylePresets.IsAltHeading(alt.Style))
            return EndOfHeadingBlock(children, anchorIndex, includeAltBoundary: true);

        return anchorIndex;
    }

    private static int ResolveContentInsertIndex(List<ReportElement> children, Guid? anchorId, TextElement root)
    {
        if (anchorId is not { } id)
            return children.Count;

        var index = children.FindIndex(element => element.Id == id);
        if (index < 0 || IsRoot(children[index]))
            return children.Count;
        if (children[index] is TableElement)
            return index + 1;
        if (children[index] is TextElement text && HeadingStylePresets.IsHeading(text.Style))
            return EndOfHeadingBlock(children, index, includeAltBoundary: false);
        if (children[index] is TextElement alt && HeadingStylePresets.IsAltHeading(alt.Style))
            return EndOfHeadingBlock(children, index, includeAltBoundary: true);
        return index + 1;
    }

    private static int EndOfHeadingBlock(List<ReportElement> children, int start, bool includeAltBoundary)
    {
        for (var i = start + 1; i < children.Count; i++)
        {
            if (children[i] is not TextElement text)
                continue;
            if (HeadingStylePresets.IsHeading(text.Style))
                return i;
            if (includeAltBoundary && HeadingStylePresets.IsAltHeading(text.Style))
                return i;
        }
        return children.Count;
    }

    private static bool HasOwningUserHeading(List<ReportElement> children, int index) =>
        FindPreviousUserHeading(children, index) >= 0
        || (children[index] is TextElement text && HeadingStylePresets.IsHeading(text.Style) && !IsRoot(text));

    private static int FindPreviousUserHeading(List<ReportElement> children, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (children[i] is TextElement text
                && HeadingStylePresets.IsHeading(text.Style)
                && !IsRoot(text))
                return i;
        }
        return -1;
    }

    private static string NormalizeHeadingText(string? value, string fallback)
    {
        var normalized = ReportHeadingNumberingService.StripVisibleNumber(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static Section EnsureBodySection(Report report)
    {
        var body = report.Pages.SelectMany(page => page.Sections)
            .FirstOrDefault(section => section.Kind == SectionKind.Body);
        if (body is not null)
            return body;

        var page = report.Pages.FirstOrDefault();
        if (page is null)
        {
            page = new Page();
            report.Pages.Add(page);
        }

        body = new Section
        {
            Name = SectionKind.Body.ToString(),
            Kind = SectionKind.Body,
            AutoHeight = true
        };
        page.Sections.Add(body);
        return body;
    }

    private static Located? FindTopLevel(Report report, Guid elementId)
    {
        foreach (var page in report.Pages)
        {
            foreach (var section in page.Sections)
            {
                if (section.Kind is SectionKind.PageHeader or SectionKind.PageFooter)
                    continue;

                var index = section.Root.Children.FindIndex(element => element.Id == elementId);
                if (index >= 0)
                    return new Located(section.Root.Children[index], section.Root, section, index);
            }
        }
        return null;
    }

    private readonly record struct Located(ReportElement Element, Container Owner, Section Section, int Index);
}
