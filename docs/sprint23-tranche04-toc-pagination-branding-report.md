# Sprint 23 Tranche 04 — TOC Navigation, Pagination Parity and Branding

## Baseline

- Base branch: `main`
- Exact base SHA: `bb58715253e934b4bdecb64e79c64e88c58afcd9`
- Working branch: `sprint23/04-toc-pagination-branding`
- Previous Windows gate: `576/576`

## Implemented scope

### Contents to Preview navigation

- Double-clicking a Heading, Alt Heading or Table in Contents routes its real `ElementId` to `PreviewViewModel.NavigateToElement`.
- Navigation never searches by display text, heading caption or table name.
- The current Preview projection resolves the first page/block fragment with that stable ID.
- When a structure/edit operation is still rebuilding Preview, the ID request remains pending and is resolved after the new layout is published.
- A newer navigation request cancels the older pending target.
- Workspace selection still drives the existing Preview highlight and Contents selection synchronization.

### Table-to-heading page transition

One Application policy defines the rule:

```text
Table -> Heading / Alt Heading = start on the next page
```

Preview:

- The deterministic layout flow observes semantic block kinds.
- A heading after a short table starts on the next page even when space remains.
- A heading after a multi-page table starts on the page immediately following the table's last fragment.
- A page already opened naturally at the table boundary is reused; no empty intermediate page is created.

Word:

- `WordExporter` consumes the same policy.
- The heading paragraph receives native OpenXML `PageBreakBefore`.
- No blank paragraphs or manual page-break runs are inserted.
- Heading styles, numbering and TOC detection remain unchanged.

### Selected application branding

The approved Excel/Word combined logo set replaces the previous binary assets behind the existing centralized paths:

- `Assets/Brand/BrandMark.png`: 256 px transparent in-app mark.
- `Assets/Brand/BrandMarkSmall.png`: 128 px transparent title/taskbar runtime mark.
- `Assets/Brand/AppIcon.ico`: seven Windows resolutions (`16, 24, 32, 48, 64, 128, 256 px`).

The existing safe runtime behavior remains: WPF loads the PNG for `Window.Icon`, while the ICO is the build-time EXE/shortcut/Alt+Tab/installer icon.

## Architecture

- Existing Preview event/navigation path is reused.
- Existing `PositionedPageBlock.ElementId` remains the navigation index.
- Existing deterministic paginator and Word exporter are extended; no second renderer or exporter is introduced.
- Existing `ReportContentKind` semantic classification feeds both outputs.
- Branding references stay centralized in the UI project and MainWindow.

## Test delta

Added tests:

- Application: `+6`
- Engine: `+2`
- Infrastructure: `+3`
- Architecture: `+3`

Expected Windows total:

```text
590 / 590
```

## Pending Windows gate

```bat
git fetch origin
git checkout sprint23/04-toc-pagination-branding
git reset --hard origin/sprint23/04-toc-pagination-branding
git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

## Manual smoke

1. Confirm the new Excel/Word mark appears in the title area and taskbar.
2. Confirm the rebuilt executable shows the new icon in Explorer/Alt+Tab.
3. Double-click a heading in Contents; Preview scrolls to and selects that exact heading.
4. Double-click a table with a duplicate/similar name elsewhere; Preview reaches the selected table by ID.
5. Trigger navigation immediately after a Preview-changing edit; it waits for the refreshed layout and reaches the target.
6. Place a short table halfway down a page and a heading after it; the heading begins on the next Preview page.
7. Use a table spanning several pages; the next heading begins directly after its final page.
8. Export Word and verify the same page transition without blank paragraphs/pages.
9. Confirm Contents numbering and Word heading styles remain intact.
