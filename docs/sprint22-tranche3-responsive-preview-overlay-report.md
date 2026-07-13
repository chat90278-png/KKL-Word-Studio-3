# Sprint 22 — Tranche 3: Responsive Preview Loading Overlay

## Trigger

A real Windows smoke with a roughly 10,000-row Excel source completed successfully and produced a 1,455-page Preview, but the user observed UI stalls and a risk that repeated clicks during the long projection could destabilize the application.

This is treated as a responsiveness and re-entry problem, not a data-correctness failure.

## Implemented

- Added one session-only `LongOperationViewModel` shared by the shell and long-running UI workflows.
- Added a full-window, in-process loading overlay to `MainWindow`.
- The overlay blocks hit testing against all underlying commands while active.
- Added operation title, detail text, indeterminate progress and a real cancel command.
- Kept the loading surface inside the main window rather than introducing a second popup/window.
- Wired Preview rendering to its existing `CancellationToken` seam.
- Gave WPF one dispatcher turn before rendering so the overlay is visible before synchronous setup can begin.
- Moved page-to-view-model projection to a cancellable background step.
- Added projected pages to the UI collection in batches of 25.
- Yielded to the dispatcher between batches so window messages, painting and cancellation can continue.
- Preserved generation checks so stale Preview results cannot overwrite a newer refresh.
- Preserved the existing `IReportPreviewRenderer`, content builder, layout engine and WPF Preview surface.

## Interaction safety

While the overlay is visible:

- Excel commands, report commands, source switching and repeated transfer clicks are shielded by the top-level overlay;
- the latest active operation supplies the visible title/detail;
- multiple overlapping internal operations remain independently tracked;
- `İptal` cancels every active operation that declared a safe cancellation token;
- the overlay closes only after all active operation leases finish.

## Coverage

Architecture guards verify:

- the loading surface is an in-window overlay, not a `Popup`;
- the overlay covers the shell with a high Z-index;
- progress and cancel bindings are present;
- one shared session state is used;
- Preview passes the operation token to `IReportPreviewRenderer`;
- projection is cancellable;
- UI collection updates are batched at 25 pages and yield between batches;
- no second Preview renderer, report content builder or Word path is introduced.

## Current validation status

The implementation head is **not GREEN yet**. Windows execution remains final truth.

```bat
git checkout sprint22/tranche3-excel-read-hardening
git pull
git rev-parse HEAD

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected totals if no additional tests land:

- Domain: 18
- Application: 209
- Engine: 60
- Architecture: 86
- Infrastructure: 131
- Total: 504

## Required UI smoke

1. Load the same approximately 10,000-row workbook.
2. Transfer it so the long Preview is rebuilt.
3. Confirm the centered loading overlay appears before the long wait.
4. Confirm clicks cannot reach Excel, Preview or Context Dock controls behind the overlay.
5. Confirm the detail text advances while pages are loaded.
6. Move or resize the window during page loading and confirm Windows still paints/responds.
7. Run once to completion and verify the expected page count and content.
8. Run again and click `İptal`; confirm the application remains alive and usable.
9. Re-run Preview after cancellation and confirm a complete result can still be produced.
10. Confirm the source XLSX hash remains unchanged.
