namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.Structure;

/// <summary>Pure UI gesture/zoom helpers kept independent from WPF event code.</summary>
public static class PreviewInteractionHelpers
{
    public static double CalculateZoom(
        PreviewZoomOption option,
        double viewportWidth,
        double viewportHeight,
        double pageWidth,
        double pageHeight)
    {
        const double workspacePadding = 64.0;
        var availableWidth = Math.Max(1.0, viewportWidth - workspacePadding);
        var availableHeight = Math.Max(1.0, viewportHeight - workspacePadding);
        var safePageWidth = Math.Max(1.0, pageWidth);
        var safePageHeight = Math.Max(1.0, pageHeight);

        return option switch
        {
            PreviewZoomOption.FitWidth => Math.Max(0.1, availableWidth / safePageWidth),
            PreviewZoomOption.FitPage => Math.Max(0.1, Math.Min(
                availableWidth / safePageWidth,
                availableHeight / safePageHeight)),
            PreviewZoomOption.Percent75 => 0.75,
            PreviewZoomOption.Percent100 => 1.0,
            PreviewZoomOption.Percent125 => 1.25,
            PreviewZoomOption.Percent150 => 1.5,
            _ => 1.0
        };
    }

    public static StructureDropMode ResolveDropMode(double pointerY, double targetHeight, bool targetIsHeading)
    {
        var height = Math.Max(1.0, targetHeight);
        var ratio = Math.Clamp(pointerY / height, 0.0, 1.0);

        if (ratio <= 0.25)
            return StructureDropMode.Before;
        if (ratio >= 0.75)
            return StructureDropMode.After;
        if (targetIsHeading)
            return StructureDropMode.Into;

        return ratio < 0.5 ? StructureDropMode.Before : StructureDropMode.After;
    }

    public static PreviewDropIndicator ToIndicator(StructureDropMode mode) => mode switch
    {
        StructureDropMode.Before => PreviewDropIndicator.Before,
        StructureDropMode.Into => PreviewDropIndicator.Into,
        StructureDropMode.After => PreviewDropIndicator.After,
        _ => PreviewDropIndicator.None
    };
}
