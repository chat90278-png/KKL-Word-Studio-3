namespace KKL.WordStudio.Application.QuickAssembly;

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

/// <summary>One unique workbook/worksheet target in visible deterministic order.</summary>
public sealed class QuickAssemblyTarget
{
    public required string SourcePath { get; init; }
    public required string WorkbookDisplayName { get; init; }
    public required string WorksheetName { get; init; }
    public required int WorkbookOrder { get; init; }
    public required int WorksheetOrder { get; init; }
    public bool IsSelected { get; set; }
    public string? Caption { get; set; }

    public string Key => QuickAssemblyTargetKey.Create(SourcePath, WorksheetName);
}

/// <summary>
/// Maintains temporary quick-assembly choices while loaded workbook snapshots
/// change. Existing choices survive refreshes; stale targets disappear.
/// </summary>
public sealed class QuickAssemblySelection
{
    private readonly List<QuickAssemblyTarget> _targets = [];

    public IReadOnlyList<QuickAssemblyTarget> Targets => _targets;

    public IReadOnlyList<QuickAssemblyTarget> SelectedTargets => _targets
        .Where(target => target.IsSelected)
        .OrderBy(target => target.WorkbookOrder)
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
                    Caption = previous?.Caption
                });
            }

            workbookOrder++;
        }

        _targets.Clear();
        _targets.AddRange(rebuilt);
    }

    public void SetWorkbookSelected(string sourcePath, bool isSelected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        foreach (var target in _targets.Where(target =>
                     string.Equals(target.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)))
        {
            target.IsSelected = isSelected;
        }
    }

    public bool SetSheetSelected(string sourcePath, string worksheetName, bool isSelected)
    {
        var target = Find(sourcePath, worksheetName);
        if (target is null)
            return false;

        target.IsSelected = isSelected;
        return true;
    }

    public bool SetCaption(string sourcePath, string worksheetName, string? caption)
    {
        var target = Find(sourcePath, worksheetName);
        if (target is null)
            return false;

        target.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        return true;
    }

    private QuickAssemblyTarget? Find(string sourcePath, string worksheetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        var key = QuickAssemblyTargetKey.Create(sourcePath, worksheetName);
        return _targets.FirstOrDefault(target =>
            string.Equals(target.Key, key, StringComparison.OrdinalIgnoreCase));
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
