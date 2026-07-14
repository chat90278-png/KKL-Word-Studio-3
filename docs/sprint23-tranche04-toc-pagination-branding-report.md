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

### Selected-heading table placement

- Selecting an existing root, heading or alt heading before `Word'e Aktar` makes that real element the placement parent.
- The popup shows the selected numbered heading as its parent line.
- Removing both optional proposed heading rows places the new table directly after the selected heading.
- Keeping either proposed row preserves the existing create-heading/create-alt-heading flow.
- The coordinator retains the selected `AnchorElementId`; it no longer falls back to the document root when both optional rows are removed.
- No second report tree or persisted parent model is introduced.

### Selected application branding

The approved Excel/Word combined logo set replaces the previous binary assets behind the existing centralized paths:

- `Assets/Brand/BrandMark.png`: 256 px transparent in-app mark.
- `Assets/Brand/BrandMarkSmall.png`: 128 px transparent title/taskbar runtime mark.
- `Assets/Brand/AppIcon.ico`: seven Windows resolutions (`16, 24, 32, 48, 64, 128, 256 px`).

The transparent outer padding was cropped and each resolution was regenerated from the tighter master, so the symbol occupies substantially more of the Windows icon square without changing the approved artwork.

The existing safe runtime behavior remains: WPF loads the PNG for `Window.Icon`, while the ICO is the build-time EXE/shortcut/Alt+Tab/installer icon.

## Architecture

- Existing Preview event/navigation path is reused.
- Existing `PositionedPageBlock.ElementId` remains the navigation index.
- Existing deterministic paginator and Word exporter are extended; no second renderer or exporter is introduced.
- Existing `ReportContentKind` semantic classification feeds both outputs.
- Branding references stay centralized in the UI project and MainWindow.
- Existing `ExcelTransferPlacementCoordinator` remains authoritative for transfer placement.

## Test delta

Added tests:

- Application: `+7`
- Engine: `+2`
- Infrastructure: `+3`
- Architecture: `+3`

Expected Windows total:

```text
591 / 591
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

1. Confirm the enlarged Excel/Word mark appears in the title area and taskbar.
2. Confirm the rebuilt executable shows the enlarged icon in Explorer/Alt+Tab.
3. Double-click a heading in Contents; Preview scrolls to and selects that exact heading.
4. Double-click a table with a duplicate/similar name elsewhere; Preview reaches the selected table by ID.
5. Trigger navigation immediately after a Preview-changing edit; it waits for the refreshed layout and reaches the target.
6. Place a short table halfway down a page and a heading after it; the heading begins on the next Preview page.
7. Use a table spanning several pages; the next heading begins directly after its final page.
8. Export Word and verify the same page transition without blank paragraphs/pages.
9. Select an existing heading, open `Word'e Aktar`, remove both proposed heading rows and confirm the new table appears directly under the selected heading.
10. Confirm Contents numbering and Word heading styles remain intact.
