namespace KKL.WordStudio.UI.Models;

/// <summary>
/// Immutable user-facing content for one page of the in-application guide.
/// The guide is UI-only and does not participate in report/project state.
/// </summary>
public sealed record UsageGuideSection(
    string Title,
    string Icon,
    string ImageAssetName,
    string Purpose,
    IReadOnlyList<string> Actions,
    string Tip);
