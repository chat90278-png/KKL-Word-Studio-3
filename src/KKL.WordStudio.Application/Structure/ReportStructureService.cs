namespace KKL.WordStudio.Application.Structure;

using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Shared.Results;

public enum StructureDropMode
{
    Before,
    After,
    Into
}

/// <summary>
/// Owns report-outline structure mutations. Contents is only a UI projection
/// of the real flat document sequence (Section.Root.Children); this service
/// mutates that real ordered sequence and never introduces a second outline
/// tree, a ParentId, or any persisted ContentsNode. Hierarchy stays derived
/// from element order plus <see cref="HeadingStylePresets"/> heading style.
///
/// Scope guard (this sprint): every operation stays inside a single owning
/// Body Section.Root. Header/footer furniture is never moved through here, and
/// cross-Section moves are rejected with a friendly message rather than faking
/// page-layout movement.
/// </summary>
public interface IReportStructureService
{
    Result Rename(Report report, Guid elementId, string newName);
    Result Delete(Report report, Guid elementId);
    Result MoveUp(Report report, Guid elementId);
    Result MoveDown(Report report, Guid elementId);
    Result Indent(Report report, Guid elementId);
    Result Outdent(Report report, Guid elementId);
    Result Move(Report report, Guid sourceElementId, Guid targetElementId, StructureDropMode mode);
}

public sealed class ReportStructureService : IReportStructureService
{
    // Derived level of an element within the outline projection.
    private enum OutlineLevel { Heading1 = 1, Heading2 = 2, Content = 3 }

    // ---------------------------------------------------------------
    // 1 — Rename
    // ---------------------------------------------------------------

    public Result Rename(Report report, Guid elementId, string newName)
    {
        if (!TryLocate(report, elementId, out var located))
            return Result.Failure("Yeniden adlandırılacak öğe bulunamadı.");

        if (string.IsNullOrWhiteSpace(newName))
            return Result.Failure("Ad boş olamaz.");

        switch (located.Element)
        {
            case TextElement text when IsHeadingLike(text):
                // Heading/Alt Heading rename changes the literal displayed text
                // via the same semantic content path exporters/preview read.
                text.Content = Expression.Literal(newName.Trim());
                return Result.Success();

            case TableElement table:
                // Table rename touches Name only — Caption, columns, Sources and
                // Binding are deliberately left untouched.
                table.Name = newName.Trim();
                return Result.Success();

            default:
                return Result.Failure("Bu öğe Contents üzerinden yeniden adlandırılamaz.");
        }
    }

    // ---------------------------------------------------------------
    // 2 — Delete
    // ---------------------------------------------------------------

    public Result Delete(Report report, Guid elementId)
    {
        if (!TryLocate(report, elementId, out var located))
            return Result.Failure("Silinecek öğe bulunamadı.");

        // Delete only the single selected element from its owning Container —
        // never cascade into visually nested headings/tables. Remaining
        // elements keep their document order, and Contents is rebuilt from that
        // order by the caller. Deleting a heading does not touch any table's
        // caption (caption was a value copy, not a live reference).
        located.Owner.Children.Remove(located.Element);
        return Result.Success();
    }

    // ---------------------------------------------------------------
    // 3 — Move up / down (logical blocks)
    // ---------------------------------------------------------------

    public Result MoveUp(Report report, Guid elementId) => MoveBlock(report, elementId, up: true);
    public Result MoveDown(Report report, Guid elementId) => MoveBlock(report, elementId, up: false);

    private Result MoveBlock(Report report, Guid elementId, bool up)
    {
        if (!TryLocate(report, elementId, out var located))
            return Result.Failure("Taşınacak öğe bulunamadı.");

        var children = located.Owner.Children;
        var block = ComputeBlock(children, located.Index);

        // The element may only move among adjacent sibling blocks at the SAME
        // derived scope. Its scope is bounded by its enclosing heading (for
        // Heading 2 / content) or the whole section (for Heading 1). Move must
        // never cross that boundary — no implicit promotion/outdent.
        var scope = ComputeScope(children, block.Start);

        if (up)
        {
            if (block.Start <= scope.Start)
                return Result.Failure("Öğe zaten bu kapsamda en üstte.");

            // Locate the immediately preceding sibling block at the SAME derived
            // level within the same scope. Never infer it by walking through
            // Content only — for a Heading 1 whose previous H1 block ends with
            // H2-owned content, that would wrongly return the trailing H2 block.
            if (!TryFindPreviousSiblingBlock(children, block.Start, scope, out var prev))
                return Result.Failure("Öğe zaten bu kapsamda en üstte.");

            ReorderMove(children, block.Start, block.Count, prev.Start);
        }
        else
        {
            var after = block.Start + block.Count;
            if (after >= scope.End)
                return Result.Failure("Öğe zaten bu kapsamda en altta.");

            var next = ComputeBlock(children, after);
            ReorderMove(children, block.Start, block.Count, next.Start + next.Count);
        }

        return Result.Success();
    }

    // ---------------------------------------------------------------
    // 4 — Indent / outdent (Style-based heading level, no new type)
    // ---------------------------------------------------------------

    public Result Indent(Report report, Guid elementId)
    {
        if (!TryLocate(report, elementId, out var located))
            return Result.Failure("Girintilenecek öğe bulunamadı.");
        if (located.Element is not TextElement text)
            return Result.Failure("Yalnızca başlıklar girintilenebilir.");

        if (HeadingStylePresets.IsHeading(text.Style))
        {
            text.Style = HeadingStylePresets.CreateAltHeadingStyle();
            return Result.Success();
        }
        if (HeadingStylePresets.IsAltHeading(text.Style))
            return Result.Failure("Alt başlık daha fazla girintilenemez.");

        return Result.Failure("Yalnızca başlıklar girintilenebilir.");
    }

    public Result Outdent(Report report, Guid elementId)
    {
        if (!TryLocate(report, elementId, out var located))
            return Result.Failure("Girintisi azaltılacak öğe bulunamadı.");
        if (located.Element is not TextElement text)
            return Result.Failure("Yalnızca başlıkların girintisi azaltılabilir.");

        if (HeadingStylePresets.IsAltHeading(text.Style))
        {
            text.Style = HeadingStylePresets.CreateHeadingStyle();
            return Result.Success();
        }
        if (HeadingStylePresets.IsHeading(text.Style))
            return Result.Failure("Başlık daha fazla dışarı alınamaz.");

        return Result.Failure("Yalnızca başlıkların girintisi azaltılabilir.");
    }

    // ---------------------------------------------------------------
    // 5 — Drag & drop move
    // ---------------------------------------------------------------

    public Result Move(Report report, Guid sourceElementId, Guid targetElementId, StructureDropMode mode)
    {
        if (sourceElementId == targetElementId)
            return Result.Failure("Öğe kendi üzerine bırakılamaz.");

        if (!TryLocate(report, sourceElementId, out var source))
            return Result.Failure("Taşınacak öğe bulunamadı.");
        if (!TryLocate(report, targetElementId, out var target))
            return Result.Failure("Hedef öğe bulunamadı.");

        // Multi-page/section rule: only same owning Body Section.Root is
        // supported this sprint; different sections are rejected, not faked.
        if (!ReferenceEquals(source.Owner, target.Owner) || !ReferenceEquals(source.Section, target.Section))
            return Result.Failure("Öğeler yalnızca aynı bölüm içinde taşınabilir.");

        var children = source.Owner.Children;
        var sourceBlock = ComputeBlock(children, source.Index);

        // Never insert a block inside its own logical subtree (cycle/self-drop).
        if (target.Index >= sourceBlock.Start && target.Index < sourceBlock.Start + sourceBlock.Count)
            return Result.Failure("Öğe kendi alt ağacının içine taşınamaz.");

        var targetBlock = ComputeBlock(children, target.Index);

        int insertionIndex;
        switch (mode)
        {
            case StructureDropMode.Before:
                insertionIndex = targetBlock.Start;
                break;

            case StructureDropMode.After:
                insertionIndex = targetBlock.Start + targetBlock.Count;
                break;

            case StructureDropMode.Into:
                if (target.Element is not TextElement targetText || !IsHeadingLike(targetText))
                    return Result.Failure("Yalnızca başlıkların içine bırakılabilir.");

                // A Heading 1 dropped into another Heading 1 becomes Heading 2,
                // applied explicitly as part of this move so classification/TOC/
                // Word semantics stay in agreement.
                if (source.Element is TextElement sourceHeading
                    && HeadingStylePresets.IsHeading(sourceHeading.Style)
                    && HeadingStylePresets.IsHeading(targetText.Style))
                {
                    sourceHeading.Style = HeadingStylePresets.CreateAltHeadingStyle();
                }

                // Drop at the END of the target heading's derived scope so, by
                // real document order, the moved block becomes nested under it.
                var targetScope = ComputeHeadingScope(children, target.Index);
                insertionIndex = targetScope.Start + targetScope.Count;
                break;

            default:
                return Result.Failure("Geçersiz bırakma işlemi.");
        }

        ReorderMove(children, sourceBlock.Start, sourceBlock.Count, insertionIndex);
        return Result.Success();
    }

    // ---------------------------------------------------------------
    // Location + block computation over the REAL ordered sequence
    // ---------------------------------------------------------------

    private readonly record struct Located(ReportElement Element, Container Owner, Section Section, int Index);
    private readonly record struct Block(int Start, int Count);

    private static bool TryLocate(Report report, Guid elementId, out Located located)
    {
        foreach (var page in report.Pages)
        {
            foreach (var section in page.Sections)
            {
                // Structure ops apply only to non-header/footer body sections.
                if (section.Kind is SectionKind.PageHeader or SectionKind.PageFooter)
                    continue;

                var children = section.Root.Children;
                for (var index = 0; index < children.Count; index++)
                {
                    if (children[index].Id == elementId)
                    {
                        located = new Located(children[index], section.Root, section, index);
                        return true;
                    }
                }
            }
        }

        located = default;
        return false;
    }

    /// <summary>
    /// Computes the logical block that starts at <paramref name="startIndex"/>:
    /// a Heading 1 owns everything until the next Heading 1; a Heading 2 owns
    /// following content until the next Heading 1 or Heading 2; anything else
    /// (a table/content element) is a block of one.
    /// </summary>
    private static Block ComputeBlock(IReadOnlyList<ReportElement> children, int startIndex)
    {
        var level = LevelOf(children[startIndex]);
        if (level == OutlineLevel.Content)
            return new Block(startIndex, 1);

        var count = 1;
        for (var i = startIndex + 1; i < children.Count; i++)
        {
            var next = LevelOf(children[i]);
            if (level == OutlineLevel.Heading1 && next == OutlineLevel.Heading1) break;
            if (level == OutlineLevel.Heading2 && (next == OutlineLevel.Heading1 || next == OutlineLevel.Heading2)) break;
            count++;
        }
        return new Block(startIndex, count);
    }

    /// <summary>
    /// The derived heading scope for a heading at <paramref name="headingIndex"/>
    /// — identical to its logical block. Used by Into-drops to append at scope end.
    /// </summary>
    private static Block ComputeHeadingScope(IReadOnlyList<ReportElement> children, int headingIndex) =>
        ComputeBlock(children, headingIndex);

    /// <summary>
    /// Computes the [Start, End) index range of the derived scope that the
    /// block starting at <paramref name="blockStart"/> lives in — i.e. the set
    /// of sibling blocks it may move among:
    /// - Heading 1: the whole section (siblings = other Heading 1 blocks).
    /// - Heading 2: from just after its enclosing Heading 1 to the end of that
    ///   Heading 1's scope (siblings = other Heading 2 blocks in the same H1).
    /// - Content/table: the run of content owned by its nearest enclosing
    ///   heading, bounded by the next heading of any level.
    /// The boundary is never crossed by Move Up/Down, so no implicit promotion.
    /// </summary>
    private static (int Start, int End) ComputeScope(IReadOnlyList<ReportElement> children, int blockStart)
    {
        var level = LevelOf(children[blockStart]);

        if (level == OutlineLevel.Heading1)
            return (0, children.Count);

        if (level == OutlineLevel.Heading2)
        {
            // Scope start: just after the enclosing Heading 1 (or 0 if none).
            var start = 0;
            for (var i = blockStart - 1; i >= 0; i--)
            {
                if (LevelOf(children[i]) == OutlineLevel.Heading1) { start = i + 1; break; }
            }
            // Scope end: the next Heading 1 at or after blockStart.
            var end = children.Count;
            for (var i = blockStart + 1; i < children.Count; i++)
            {
                if (LevelOf(children[i]) == OutlineLevel.Heading1) { end = i; break; }
            }
            return (start, end);
        }

        // Content/table: bounded by the nearest heading (any level) on each side.
        var contentStart = 0;
        for (var i = blockStart - 1; i >= 0; i--)
        {
            if (LevelOf(children[i]) != OutlineLevel.Content) { contentStart = i + 1; break; }
        }
        var contentEnd = children.Count;
        for (var i = blockStart + 1; i < children.Count; i++)
        {
            if (LevelOf(children[i]) != OutlineLevel.Content) { contentEnd = i; break; }
        }
        return (contentStart, contentEnd);
    }

    /// <summary>
    /// Finds the immediately preceding sibling block at the SAME derived level
    /// as the block starting at <paramref name="blockStart"/>, within
    /// <paramref name="scope"/>. Sibling blocks at a given level partition the
    /// scope contiguously, so the previous sibling is the block whose start is
    /// the nearest same-level "block start" strictly before blockStart and at or
    /// after scope.Start. Returns false when there is no such previous sibling.
    /// </summary>
    private static bool TryFindPreviousSiblingBlock(
        IReadOnlyList<ReportElement> children, int blockStart, (int Start, int End) scope, out Block previous)
    {
        var level = LevelOf(children[blockStart]);

        for (var i = blockStart - 1; i >= scope.Start; i--)
        {
            if (IsSiblingBlockStart(children, i, level, scope))
            {
                previous = ComputeBlock(children, i);
                return true;
            }
        }

        previous = default;
        return false;
    }

    /// <summary>
    /// True when <paramref name="index"/> begins a sibling block of
    /// <paramref name="level"/> within <paramref name="scope"/>:
    /// - Heading 1 / Heading 2: the element is exactly that heading level.
    /// - Content: every content element is its OWN block (ComputeBlock returns a
    ///   block of one for content), so any content element in scope is a sibling
    ///   start. This lets Move Up swap with the immediately adjacent table only,
    ///   not jump to the start of the content run.
    /// </summary>
    private static bool IsSiblingBlockStart(
        IReadOnlyList<ReportElement> children, int index, OutlineLevel level, (int Start, int End) scope)
    {
        var here = LevelOf(children[index]);
        if (level == OutlineLevel.Content)
            return here == OutlineLevel.Content;

        // Heading levels: a sibling block starts exactly at a heading of the
        // same level.
        return here == level;
    }

    private static OutlineLevel LevelOf(ReportElement element) => element switch
    {
        TextElement text when HeadingStylePresets.IsHeading(text.Style) => OutlineLevel.Heading1,
        TextElement text when HeadingStylePresets.IsAltHeading(text.Style) => OutlineLevel.Heading2,
        _ => OutlineLevel.Content
    };

    private static bool IsHeadingLike(TextElement text) =>
        HeadingStylePresets.IsHeading(text.Style) || HeadingStylePresets.IsAltHeading(text.Style);

    /// <summary>
    /// Moves a contiguous run of <paramref name="count"/> elements starting at
    /// <paramref name="from"/> so that the run begins at <paramref name="to"/>
    /// in the post-removal index space. Order within the run is preserved.
    /// </summary>
    private static void ReorderMove(List<ReportElement> children, int from, int count, int to)
    {
        var slice = children.GetRange(from, count);
        children.RemoveRange(from, count);

        // Adjust the destination for the removal when it sat after the run.
        var insertAt = to > from ? to - count : to;
        insertAt = Math.Clamp(insertAt, 0, children.Count);
        children.InsertRange(insertAt, slice);
    }
}
