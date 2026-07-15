# KKL Word Studio v1.0.1

Patch release for the Windows application icon and single-file distribution.

## Fixed

- Replaced the malformed icon directory behavior with a deterministic valid ICO source.
- The executable, Explorer list views, desktop shortcuts and large-icon views now use the same KKL Word Studio artwork.
- Removed the need to pass `ApplicationIcon` as a global command-line property.
- Windows publishing now produces one self-contained executable and fails if extra publish files remain.

## Download

Use `KKL-Word-Studio-v1.0.1-win-x64.exe` for the direct single-file application.

The ZIP contains the same executable. The `.sha256` file can be used to verify the download.

## Requirements

- 64-bit Windows 10 or later.
- No separate .NET installation is required.

## Important session behavior

KKL Word Studio uses an in-memory working session. Closing the application ends that session. Native `.kws` project open/save commands are intentionally not part of this release.

## Verification

The release workflow runs the complete Release build and all 677 automated tests before publishing the executable.
