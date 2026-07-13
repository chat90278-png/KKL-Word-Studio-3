# KKL Word Studio brand assets

Final selected mark: a vertically merged green spreadsheet and blue document icon, simplified for title-bar and taskbar readability.

- `BrandMark.png`: 256 px transparent master used for larger UI branding.
- `BrandMarkSmall.png`: 128 px small-size optimized mark used in the 24 px title area and as the safe runtime window/taskbar icon.
- `AppIcon.ico`: multi-resolution Windows executable icon (16–256 px).

Keep the green/blue split, aspect ratio and transparent background. Do not load the ICO directly through `Window.Icon`; the PNG runtime path avoids the WPF startup TypeConverter failure previously observed.
