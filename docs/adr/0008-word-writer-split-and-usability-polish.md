# ADR 0008: Word Writer Decomposition, Preview Table Grids, and Usability Polish

## Status
Accepted

## Context
Sprint 6's goal was hardening the first usable version, not new architecture
— but two of its requests (real table grids in Preview, splitting
`WordExporter`) are genuine structural decisions worth recording.

## Decisions

### 1. Tables render as real grids in Preview
`PreviewBlock` (Application) and its UI counterpart `PreviewBlockViewModel`
are now split into `Text`/`Table` variants — mirroring `ReportContentNode`'s
own `TextContentNode`/`TableContentNode` split exactly. Previously,
`TableContentNode` was flattened into "`|`"-joined text lines for Preview;
that flattening was the reason tables didn't visually resemble the tables
`WordExporter` actually produces. `TablePreviewBlockViewModel` builds a
`System.Data.DataTable` (reusing the same technique
`ExcelWorkspaceViewModel`'s preview grid already used, rather than
inventing a second approach), and the Preview view picks a DataGrid or a
styled TextBlock purely via WPF's implicit per-type `DataTemplate`
resolution — no manual type-switch in XAML or code-behind.

### 2. `WordExporter` split into small writer classes, no interfaces
`WordExporter` had grown to handle styles, header/footer, page layout,
paragraphs, and tables all in one class. Split into
`WordStyleWriter` / `WordHeaderFooterWriter` / `WordPageLayoutWriter` /
`WordParagraphWriter` / `WordTableWriter` / `WordContentWriter` (the
shared per-node dispatch, reused by Body/Header/Footer so they can't
diverge). `WordExporter` itself is now a thin orchestrator.

**Explicitly not done:** no `IWordStyleWriter`/`IWordTableWriter`
interfaces, no DI registration per writer. There is exactly one way each
of these needs to work today, and no second implementation is anticipated
— adding interfaces now would be unearned abstraction (Strategy without a
second strategy). The classes are `internal`, with a test-only
`InternalsVisibleTo` so they can be unit-tested directly without forcing
them into a public API surface.

### 3. User-friendly error messages
Several `Result.Failure` messages interpolated raw `ex.Message` directly
into user-facing text (e.g. "Could not save project: {ex.Message}"),
which could surface .NET exception internals. All such messages were
rewritten to be actionable and non-technical (e.g. "Couldn't save the
project — make sure you have permission to write there and enough disk
space, then try again."). The original exception is still logged via
`ILogger` in every case — nothing about the technical detail was lost,
only what reaches the status bar changed.

### 4. Open file / Open containing folder after export
Added `IShellLauncher` (UI), wrapping `Process.Start` for opening the
exported file with its default app or revealing it in Explorer
(`explorer.exe /select,`). `MainViewModel.LastExportedFilePath` gates two
new commands via `CanExecute`, so the buttons are simply disabled until an
export actually succeeds rather than needing separate visibility logic.

### 5. Header/Footer/Body visual separation
Preview's header and footer bands now carry a faint background tint
(distinct from the body's plain white) rather than an explicit "HEADER"/
"FOOTER" label drawn on the page — a label would have made the preview
look less like the real Word output it's meant to approximate. The tint
is deliberately subtle for the same reason.

### 6. `ExcelDataProvider` streaming — performance note only, no code change
Confirmed again this sprint: no real large-dataset problem has been
observed. Per the sprint's own instruction, only a documented limitation
was added (README), not a streaming implementation — see Consequences.

## Consequences
- Preview and the exported `.docx` now render the *same* table structure
  through two different but structurally parallel paths (DataGrid vs
  OpenXML `Table`), rather than Preview showing a lossy text summary.
- The Word writer classes are independently unit-tested (paragraph style
  IDs, outline levels, twips conversion, TOC/PAGE field instructions) —
  this was awkward to do against the previous monolithic class without
  spinning up a full `WordprocessingDocument` for every assertion.
- `ExcelDataProvider`'s DOM-based OpenXML reading remains a known
  limitation for very large worksheets. If a real large-file performance
  problem is reported, the fix is the OpenXML SDK's streaming
  `OpenXmlReader`/`OpenXmlWriter` API — not attempted here, deliberately,
  per YAGNI and the sprint's explicit instruction to leave only a note.
