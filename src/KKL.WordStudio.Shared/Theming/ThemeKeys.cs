namespace KKL.WordStudio.Shared.Theming;

/// <summary>
/// Central registry of resource-dictionary keys used by the theme system.
/// Defining these as constants (rather than magic strings scattered across
/// XAML + code-behind) means renaming or restructuring the theme later is a
/// single-file change. The UI project's ResourceDictionaries are the only
/// place these keys are actually bound to brushes/values.
///
/// Extended for the Variant 2.5 visual system (restrained Fluent-inspired
/// palette) — see Themes/Colors.xaml for the actual values.
/// </summary>
public static class ThemeKeys
{
    public const string BrushSurface = "Brush.Surface";
    public const string BrushSurfaceAlt = "Brush.Surface.Alt";
    public const string BrushBorder = "Brush.Border";
    public const string BrushAccent = "Brush.Accent";
    public const string BrushTextPrimary = "Brush.Text.Primary";
    public const string BrushTextSecondary = "Brush.Text.Secondary";

    public const string BrushAppBackground = "Brush.App.Background";
    public const string BrushSoft = "Brush.Soft";
    public const string BrushBorderLight = "Brush.Border.Light";
    public const string BrushAccentSoft = "Brush.Accent.Soft";
    public const string BrushOk = "Brush.Status.Ok";
    public const string BrushWarn = "Brush.Status.Warn";
    public const string BrushBad = "Brush.Status.Bad";
    public const string BrushHeaderRowHighlight = "Brush.HeaderRowHighlight";

    public const string FontFamilyDefault = "Font.Family.Default";
    public const string FontSizeDefault = "Font.Size.Default";
}
