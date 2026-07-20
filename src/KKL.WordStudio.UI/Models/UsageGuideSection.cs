namespace KKL.WordStudio.UI.Models;

/// <summary>
/// Immutable default content for one page of the in-application guide.
/// The guide is UI-only and does not participate in report/project state.
/// A stable Id keeps user-authored overrides compatible with future releases.
/// </summary>
public sealed record UsageGuideSection(
    string Id,
    string Title,
    string Icon,
    string ImageAssetName,
    string Purpose,
    IReadOnlyList<string> Actions,
    string Tip);
