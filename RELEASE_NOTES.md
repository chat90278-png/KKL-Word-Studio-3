# KKL Word Studio v1.0.2

Patch release for deterministic Windows branding and cache-safe single-file distribution.

## Fixed

- Removed the legacy checked-in `AppIcon.ico` so old icon artwork cannot be selected by manual publish commands or stale project metadata.
- The approved master artwork now generates a real multi-resolution ICO with independent 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256px frames.
- Explorer list views, desktop shortcuts and large-icon views use the same KKL Word Studio artwork.
- Release packaging removes old `*-single-exe` folders and the temporary generic `KKL.WordStudio.exe` publish directory.
- Release output uses the cache-safe versioned name `KKL-Word-Studio-v1.0.2-win-x64.exe`.
- Debug symbols are disabled across the full project graph during publish so the validated release output contains exactly one executable.

## Download

Use `KKL-Word-Studio-v1.0.2-win-x64.exe` for the direct self-contained application.

The ZIP contains the same executable. The `.sha256` file can be used to verify the download.

## Requirements

- 64-bit Windows 10 or later.
- No separate .NET installation is required.

## Important session behavior

KKL Word Studio uses an in-memory working session. Closing the application ends that session. Native `.kws` project open/save commands are intentionally not part of this release.

## Verification

The release workflow runs the complete Release build and all 677 automated tests before publishing the executable.
