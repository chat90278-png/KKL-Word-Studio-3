# KKL Word Studio brand assets

Final selected mark: the green Excel source grid and blue Word report page combined inside one rounded application icon.

- `BrandMark.png`: 256 px transparent application mark for larger in-app branding.
- `BrandMarkSmall.png`: 128 px transparent small-size mark for the title area and safe runtime window/taskbar icon.
- `AppIcon.ico`: seven-resolution Windows executable icon (`16, 24, 32, 48, 64, 128, 256 px`).

The repository assets are optimized derivatives of the approved transparent master supplied for Sprint 23. Keep the Excel/Word split, aspect ratio, white internal document surface and transparent outer background.

Do not load the ICO directly through `Window.Icon`; the PNG runtime path avoids the WPF startup TypeConverter failure previously observed. The ICO remains the build-time executable, shortcut, Alt+Tab and installer icon.
