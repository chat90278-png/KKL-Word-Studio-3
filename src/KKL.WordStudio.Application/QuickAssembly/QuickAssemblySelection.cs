namespace KKL.WordStudio.Application.QuickAssembly;

using System.IO;

/// <summary>
/// Session-only projection of one loaded workbook. This contract is never
/// persisted into the Domain project model.
/// </summary>
public sealed class QuickAssemblySourceSnapshot
{
    public required string SourcePath { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> WorksheetNames { get; init; }
}

public enum QuickAssemblyAnchorKind
{
    Heading,
    AltHeading
}

/// <summary>
/// One unique workbook/worksheet target. SelectionOrder is assigned when the
/// user clicks the target and becomes the authoritative report block order.
/// Heading metadata and anchor references are session-only and feed the existing
/// placement coordinator.
/// </summary>
public sealed class QuickAssemblyTarget
{
    public required string SourcePath { get; init; }
    public required string WorkbookDisplayName { get; init; }
    public required string WorksheetName { get; init; }
    public required int WorkbookOrder { get; init; }
    public required int WorksheetOrder { get; init; }

    public bool IsSelected { get; set; }
    public int? SelectionOrder { get; set; }

    public bool IncludeHeading { get; set; } = true;
    public string HeadingText { get; set; } = string.Empty;
    public bool IncludeAltHeading { get; set; } = true;
    public string AltHeadingText { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// When no new heading is created, the user must deliberately choose the
    /// existing/earlier quick-report heading level that owns this block.
    /// </summary>
    public QuickAssemblyAnchorKind? PlacementAnchorKind { get; set; }
    public Guid? ExistingPlacementAnchorId { get; set; }
    public string? SourcePlacementTargetKey { get; set; }

    /// <summary>Resolved immediately before transfer; never persisted or displayed.</summary>
    public Guid? ResolvedPlacementAnchorId { get; set; }

    /// <summary>Runtime identities published after this target succeeds so later targets may attach to it.</summary>
    public Guid? CreatedHeadingElementId { get; set; }
    public Guid? CreatedAltHeadingElementId { get; set; }

    public bool RequiresPlacementAnchor => !IncludeHeading;

    public QuickAssemblyAnchorKind? RequiredPlacementAnchorKind => IncludeHeading
        ? null
        : IncludeAltHeading
            ? QuickAssemblyAnchorKind.Heading
            : QuickAssemblyAnchorKind.AltHeading;

    public bool HasPlacementAnchorReference =>
        ExistingPlacementAnchorId.HasValue || !string.IsNullOrWhiteSpace(SourcePlacementTargetKey);

    /// <summary>Backward-compatible alias used by older quick-assembly tests and reports.</summary>
    public string? Caption
    {
        get => string.IsNullOrWhiteSpace(TableName) ? null : TableName;
        set => TableName = value?.Trim() ?? string.Empty;
    }

    public string Key => QuickAssemblyTargetKey.Create(SourcePath, WorksheetName);
}

/// <summary>
/// Maintains temporary quick-report choices while loaded workbook snapshots
/// change. Existing choices, click order, structure text and target references
/// survive refreshes; stale targets disappear.
/// </summary>
public sealed class QuickAssemblySelection
{
    private readonly List<QuickAssemblyTarget> _targets = [];

    public IReadOnlyList<QuickAssemblyTarget> Targets => _targets;

    public IReadOnlyList<QuickAssemblyTarget> SelectedTargets => _targets
        .Where(target => target.IsSelected)
        .OrderBy(target => target.SelectionOrder ?? int.MaxValue)
        .ThenBy(target => target.WorkbookOrder)
        .ThenBy(target => target.WorksheetOrder)
        .ToList();

    public void Synchronize(IEnumerable<QuickAssemblySourceSnapshot> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var existing = _targets.ToDictionary(target => target.Key, StringComparer.OrdinalIgnoreCase);
        var rebuilt = new List<QuickAssemblyTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workbookOrder = 0;

        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (string.IsNullOrWhiteSpace(source.SourcePath))
                throw new ArgumentException("Quick-assembly source path cannot be blank.", nameof(sources));

            var defaultHeading = ResolveDefaultHeading(source.DisplayName);
            for (var worksheetOrder = 0; worksheetOrder < source.WorksheetNames.Count; worksheetOrder++)
            {
                var worksheetName = source.WorksheetNames[worksheetOrder];
                if (string.IsNullOrWhiteSpace(worksheetName))
                    continue;

                var key = QuickAssemblyTargetKey.Create(source.SourcePath, worksheetName);
                if (!seen.Add(key))
                    continue;

                existing.TryGetValue(key, out var previous);
                rebuilt.Add(new QuickAssemblyTarget
                {
                    SourcePath = source.SourcePath,
                    WorkbookDisplayName = source.DisplayName,
                    WorksheetName = worksheetName,
                    WorkbookOrder = workbookOrder,
                    WorksheetOrder = worksheetOrder,
                    IsSelected = previous?.IsSelected ?? false,
                    SelectionOrder = previous?.SelectionOrder,
                    IncludeHeading = previous?.IncludeHeading ?? true,
                    HeadingText = previous?.HeadingText ?? defaultHeading,
                    IncludeAltHeading = previous?.IncludeAltHeading ?? true,
                    AltHeadingText = previous?.AltHeadingText ?? worksheetName,
                    TableName = previous?.TableName ?? worksheetName,
                    PlacementAnchorKind = previous?.PlacementAnchorKind,
                    ExistingPlacementAnchorId = previous?.ExistingPlacementAnchorId,
                    SourcePlacementTargetKey = previous?.SourcePlacementTargetKey
                });
            }

            workbookOrder++;
        }

        _targets.Clear();
        _targets.AddRange(rebuilt);
        ReindexSelectionOrder();
        RemoveStaleQuickTargetReferences();
    }

    public void SetWorkbookSelected(string sourcePath, bool isSelected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        foreach (var target in _targets
                     .Where(target => string.Equals(target.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(target => target.WorksheetOrder))
        {
            SetTargetSelected(target, isSelected, reindexAfterChange: false);
        }

        ReindexSelectionOrder();
    }

    public bool SetSheetSelected(string sourcePath, string worksheetName, bool isSelected)
    {
        var target = Find(sourcePath, worksheetName);
        if (target is null)
            return false;

        SetTargetSelected(target, isSelected);
        return true;
    }

    public void SetTargetSelected(QuickAssemblyTarget target, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!_targets.Contains(target))
            throw new ArgumentException("Quick-report target does not belong to this selection.", nameof(target));

        SetTargetSelected(target, isSelected, reindexAfterChange: true);
    }

    public bool MoveSelected(QuickAssemblyTarget target, int offset)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (offset == 0 || !target.IsSelected)
            return false;

        var ordered = SelectedTargets.ToList();
        var currentIndex = ordered.IndexOf(target);
        if (currentIndex < 0)
            return false;

        var targetIndex = Math.Clamp(currentIndex + Math.Sign(offset), 0, ordered.Count - 1);
        if (targetIndex == currentIndex)
            return false;

        (ordered[currentIndex], ordered[targetIndex]) = (ordered[targetIndex], ordered[currentIndex]);
        for (var index = 0; index < ordered.Count; index++)
            ordered[index].SelectionOrder = index + 1;
        return true;
    }

    public bool SetCaption(string sourcePath, string worksheetName, string? caption)
    {
        var target = Find(sourcePath, worksheetName);
        if (target is null)
            return false;

        target.Caption = caption;
        return true;
    }

    private void SetTargetSelected(QuickAssemblyTarget target, bool isSelected, bool reindexAfterChange)
    {
        if (target.IsSelected == isSelected)
            return;

        target.IsSelected = isSelected;
        target.SelectionOrder = isSelected
            ? _targets.Where(candidate => candidate.IsSelected && candidate.SelectionOrder.HasValue)
                .Select(candidate => candidate.SelectionOrder!.Value)
                .DefaultIfEmpty(0)
                .Max() + 1
            : null;

        target.ResolvedPlacementAnchorId = null;
        target.CreatedHeadingElementId = null;
        target.CreatedAltHeadingElementId = null;

        if (reindexAfterChange)
            ReindexSelectionOrder();
    }

    private void ReindexSelectionOrder()
    {
        var selected = _targets
            .Where(target => target.IsSelected)
            .OrderBy(target => target.SelectionOrder ?? int.MaxValue)
            .ThenBy(target => target.WorkbookOrder)
            .ThenBy(target => target.WorksheetOrder)
            .ToList();

        for (var index = 0; index < selected.Count; index++)
            selected[index].SelectionOrder = index + 1;

        foreach (var target in _targets.Where(target => !target.IsSelected))
            target.SelectionOrder = null;
    }

    private void RemoveStaleQuickTargetReferences()
    {
        var keys = _targets.Select(target => target.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var target in _targets.Where(target =>
                     !string.IsNullOrWhiteSpace(target.SourcePlacementTargetKey)
                     && !keys.Contains(target.SourcePlacementTargetKey!)))
        {
            target.SourcePlacementTargetKey = null;
            target.PlacementAnchorKind = null;
        }
    }

    private QuickAssemblyTarget? Find(string sourcePath, string worksheetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        var key = QuickAssemblyTargetKey.Create(sourcePath, worksheetName);
        return _targets.FirstOrDefault(target =>
            string.Equals(target.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveDefaultHeading(string displayName)
    {
        var fileName = Path.GetFileNameWithoutExtension(displayName);
        return string.IsNullOrWhiteSpace(fileName) ? displayName.Trim() : fileName;
    }
}

public static class QuickAssemblyTargetKey
{
    public static string Create(string sourcePath, string worksheetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        return $"{sourcePath.Trim()}\u001f{worksheetName.Trim()}";
    }
}
