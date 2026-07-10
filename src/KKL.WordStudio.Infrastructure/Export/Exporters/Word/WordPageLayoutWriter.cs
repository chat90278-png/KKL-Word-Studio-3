namespace KKL.WordStudio.Infrastructure.Export.Exporters.Word;

using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;

/// <summary>Converts the shared millimeter page layout into Word section geometry, including explicit orientation and header/footer distances.</summary>
internal static class WordPageLayoutWriter
{
    private const double TwipsPerMillimeter = 1440.0 / 25.4;

    public static void AppendPageLayout(SectionProperties sectionProperties, PageLayout layout)
    {
        var widthTwips = (uint)Math.Round(layout.WidthMillimeters * TwipsPerMillimeter);
        var heightTwips = (uint)Math.Round(layout.HeightMillimeters * TwipsPerMillimeter);
        var orientation = layout.WidthMillimeters > layout.HeightMillimeters
            ? PageOrientationValues.Landscape
            : PageOrientationValues.Portrait;

        sectionProperties.Append(new PageSize
        {
            Width = widthTwips,
            Height = heightTwips,
            Orient = orientation
        });
        sectionProperties.Append(new PageMargin
        {
            Top = (int)Math.Round(layout.MarginTopMillimeters * TwipsPerMillimeter),
            Bottom = (int)Math.Round(layout.MarginBottomMillimeters * TwipsPerMillimeter),
            Left = (uint)Math.Round(layout.MarginLeftMillimeters * TwipsPerMillimeter),
            Right = (uint)Math.Round(layout.MarginRightMillimeters * TwipsPerMillimeter),
            Header = (uint)Math.Round(layout.HeaderDistanceMillimeters * TwipsPerMillimeter),
            Footer = (uint)Math.Round(layout.FooterDistanceMillimeters * TwipsPerMillimeter)
        });
    }
}
