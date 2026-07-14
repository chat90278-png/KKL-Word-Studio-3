# Sprint 23 Tranche 05 — Responsive Shell, Excel Zoom and Search

## Stacked baseline

- Parent branch: `sprint23/04-toc-pagination-branding`
- Parent exact SHA: `67ffe48a941a26491e2e0cd3175d9a378dd31b34`
- Working branch: `sprint23/05-responsive-shell`
- Parent Windows gate: pending final verification (`592/592` expected)

This tranche is intentionally stacked. Tranche 04 remains a separate PR and must be verified/merged first; Tranche 05 will then be retargeted to `main` without mixing the two review gates.

## Implemented scope

### Responsive report workspace

- Excel remains the primary workspace and consumes all free width.
- Preview and Context Dock now live inside one `ReportPaneShell`.
- A narrow, always-visible right-edge handle opens/closes the whole report workspace.
- Closing uses deterministic `Visibility.Collapsed` plus a zero-width Grid column, preventing internal Preview/Dock minimum widths from keeping the pane visible.
- Preview is not rebuilt; selection, scroll state and rendered pages stay in the existing view instances.
- Wide windows start open, small windows start closed, and medium widths retain the current session preference.
- Report pane width is responsive and clamped to sensible minimum/maximum values.
- On narrow report widths, the Context Dock is physically compacted to 46 px without overwriting its own saved runtime state.
- Successful Excel-to-report transfer reveals the report pane; failures and ordinary Excel cell edits do not.
- Contents double-click reveals the report pane before stable-ID Preview navigation.

### Simplified command placement

- Removed visible `Yeni`, `Aç`, `Kaydet` and `Farklı Kaydet` controls from the global command bar.
- Existing project save commands/services remain in code for a later dedicated cleanup; this tranche changes only the shell surface.
- `Word Dosyası Oluştur` remains the global primary action on the right.
- Loaded-source commands remain in their existing source host.
- Report-specific controls disappear together with the report workspace.
- Cell edit commands remain inside the Excel surface.

### Excel source zoom

- `Ctrl + Mouse Wheel` changes only the Excel source grid zoom.
- Bottom controls provide minus, current percentage/reset, and plus.
- Range is clamped to 50–200%, starting at 100%.
- A `LayoutTransform` scales text, rows, columns and headers together.
- Word Preview zoom is unaffected.

### Excel source search

- `Ctrl+F` opens a compact search strip.
- Search is case-insensitive substring matching across displayed active-sheet values, including the projected header row.
- Enter moves forward; Shift+Enter moves backward; Escape closes.
- Search wraps and displays `current / total`.
- A hit selects the real cell and scrolls it into view.
- No-result and empty-query states are explicit.

### Large-grid continuity

- Existing full-range data loading from Tranche 01 is retained.
- DataGrid row/column virtualization is explicitly enabled in recycling mode.
- No second spreadsheet viewer or alternate data reader was introduced.

## Architecture

- `ReportPaneViewModel` is UI/session state only; it never touches report/domain data or Preview rendering.
- MainWindow translates session state into Grid column width and visibility only.
- Excel transfer continues through the established coordinator and signals pane reveal only after a successful status transition.
- Search is navigation-only and reads the existing `PreviewTable`; it does not mutate working/source data.
- Preview and Context Dock remain singleton view instances resolved by the existing composition root.

## Test delta

Added Architecture tests: `+5`

Expected stacked Windows total:

```text
597 / 597
```

## Pending combined Windows gate

After checking Tranche 04, check this branch:

```bat
git fetch origin
git checkout sprint23/05-responsive-shell
git reset --hard origin/sprint23/05-responsive-shell
git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Manual smoke:

1. Resize below 1180 px: report workspace starts/closes and Excel retains primary width.
2. Click the edge handle: report workspace disappears completely; click again to restore it.
3. Confirm Preview position and selected report item remain unchanged across close/open.
4. Transfer successfully: report pane opens; cancel/failure does not open it.
5. Confirm Preview + Contents/Properties/Warnings close together.
6. Confirm the top bar has no New/Open/Save/Save As controls and still shows loaded sources plus Word export.
7. Use Ctrl+wheel and bottom controls from 50% to 200%; Preview zoom does not change.
8. Use Ctrl+F, Enter, Shift+Enter and Escape; verify wrap, counter and scroll-to-cell.
9. Confirm large worksheets remain scrollable and responsive.
