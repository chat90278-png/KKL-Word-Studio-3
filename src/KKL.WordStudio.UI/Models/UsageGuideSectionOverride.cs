namespace KKL.WordStudio.UI.Models;

public sealed record UsageGuideSectionOverride(
    string Title,
    string Purpose,
    IReadOnlyList<string> Actions,
    string Tip,
    string? CustomImageFileName);

internal sealed record UsageGuideOverrideDocument(
    int Version,
    Dictionary<string, UsageGuideSectionOverride> Sections);
