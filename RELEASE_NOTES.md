# KKL Word Studio v1.0.2

Windows icon consistency and cache-safety patch release.

## Fixed

- Removed the legacy checked-in `AppIcon.ico` so old artwork cannot be selected by manual publish commands or stale project settings.
- The approved master artwork is converted at build time into a real multi-resolution Windows ICO with independent 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256px PNG frames.
- Explorer list views, large-icon views and desktop shortcuts now receive the same artwork at the native size Windows requests.
- Release packaging deletes legacy `*-single-exe` output folders before publishing.
- The temporary generic `KKL.WordStudio.exe` publish directory is removed after packaging.
- The distributable has a unique versioned filename to bypass Windows icon cache collisions.

## Download

Use `KKL-Word-Studio-v1.0.2-win-x64.exe` for the direct self-contained single-file application.

The ZIP contains the same executable. The `.sha256` file can be used to verify the download.

## Requirements

- 64-bit Windows 10 or later.
- No separate .NET installation is required.

## Important session behavior

KKL Word Studio uses an in-memory working session. Closing the application ends that session. Native `.kws` project open/save commands are intentionally not part of this release.

## Verification

The release workflow runs the complete Release build and all 677 automated tests before publishing the executable.
