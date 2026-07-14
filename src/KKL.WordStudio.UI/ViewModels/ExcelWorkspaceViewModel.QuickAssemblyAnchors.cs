namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.QuickAssembly;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Visitors;

public sealed partial class ExcelWorkspaceViewModel
{
    public IReadOnlyList<QuickAssemblyExistingAnchor> GetQuickAssemblyExistingAnchors()
    {
        var report = _workspace.ActiveReport;
        if (report is null)
            return Array.Empty<QuickAssemblyExistingAnchor>();

        return ReportElementFlattener.Flatten(report)
            .OfType<TextElement>()
            .Where(text => !string.Equals(text.Name, "Document Root", StringComparison.Ordinal))
            .Select(text =>
            {
                var kind = HeadingStylePresets.IsAltHeading(text.Style)
                    ? QuickAssemblyAnchorKind.AltHeading
                    : HeadingStylePresets.IsHeading(text.Style)
                        ? QuickAssemblyAnchorKind.Heading
                        : (QuickAssemblyAnchorKind?)null;
                return new { Text = text, Kind = kind };
            })
            .Where(item => item.Kind.HasValue)
            .Select(item => new QuickAssemblyExistingAnchor
            {
                ElementId = item.Text.Id,
                Kind = item.Kind!.Value,
                DisplayText = ReportHeadingNumberingService.StripVisibleNumber(item.Text.Content.Text)
            })
            .ToList();
    }
}

public sealed class QuickAssemblyExistingAnchor
{
    public required Guid ElementId { get; init; }
    public required QuickAssemblyAnchorKind Kind { get; init; }
    public required string DisplayText { get; init; }
}
