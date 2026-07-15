# KKL Word Studio v1.0.0

First production release of the Excel-first Word report composition workflow.

## Highlights

- Import Excel workbooks and configure worksheet data ranges.
- Edit project-owned working data without modifying the original Excel file.
- Build quick reports from one or more worksheet sources.
- Preview reports through the authoritative pagination and layout engine.
- Import read-only front-matter and reference-format DOCX files.
- Navigate structured diagnostics and run Word export preflight checks.
- Export real DOCX files with repeated table headers, non-splitting rows, stable ordering and large-table pagination.
- Recover safely from missing or corrupt Excel and DOCX source files.

## Installation

1. Download `KKL-Word-Studio-v1.0.0-win-x64.zip`.
2. Extract the complete archive to a writable folder.
3. Run `KKL.WordStudio.exe`.

The package is self-contained for 64-bit Windows and does not require a separate .NET installation.

## Important session behavior

KKL Word Studio v1.0.0 uses an in-memory working session. Closing the application ends that session. Native `.kws` project open/save commands are intentionally not part of this release.

## Verification baseline

The pre-release Windows run completed with zero build warnings, zero build errors and 677 passing tests across Domain, Application, Engine, Architecture and Infrastructure suites. The release workflow repeats the complete test suite in Release configuration before publishing the downloadable package.
