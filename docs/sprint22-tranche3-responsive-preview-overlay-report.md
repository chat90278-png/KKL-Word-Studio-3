# Sprint 22 — Tranche 3: Responsive Preview Loading Overlay

## Trigger

A real Windows smoke with a roughly 10,000-row Excel source completed successfully and produced a 1,455-page Preview, but the user observed UI stalls and a risk that repeated clicks during the long projection could destabilize the application.

The first implementation added the shell overlay and wired Preview projection, but the user correctly reported that no loading surface appeared while opening Excel or pressing `Word'e Aktar`.

## Root cause of the missing overlay

- Excel `OpenWorkbookAsync`, `GetSheetPreviewAsync` and AutoRange methods returned `Task` but still performed OpenXML traversal synchronously before returning.
- `Word'e Aktar` called the synchronous transfer service directly.
- Merely setting `IsBusy` followed by `Task.Yield()` did not guarantee that WPF data binding and rendering completed before the heavy call started.

## Corrected implementation

- Added one session-only `LongOperationViewModel` shared by the shell and long-running UI workflows.
- Added a full-window, in-process loading overlay to `MainWindow`.
- The overlay blocks hit testing against all underlying commands while active.
- Added operation title, detail text, indeterminate progress and a cancel command.
- Kept the loading surface inside the main window rather than introducing a second popup/window.
- Added transparent UI decorators around the existing Excel reader and transfer service.
- The decorators do not read Excel bytes and do not create a second transfer engine.
- Composition still resolves the same `OpenXmlExcelWorkbookReader` and `ColumnSelectionExcelReportTransferService` as the authoritative implementations.
- Excel open, sheet preview, AutoRange and WorkingData OpenXML traversals now execute behind `Task.Run` with cancellation checks.
- The loading state forces one WPF DataBind/Render turn before heavy work begins.
- Dispatcher pumping stops at `Loaded` priority, before a new input click can re-enter.
- Forced presentation is skipped during startup until `MainWindow` is visible.
- Transfer loading remains visible through the current dispatcher turn so the Preview refresh can take over without a clickable gap.
- Preview rendering keeps its existing cancellation token seam.
- Page-to-view-model projection remains cancellable.
- Projected pages are added to the UI collection in batches of 25.
- The dispatcher receives control between batches so painting and cancellation can continue.
- Existing refresh-generation checks still prevent stale Preview results from overwriting newer work.

## Cancellation safety

- Preview rendering is cancellable and catches its own `OperationCanceledException` boundary.
- Excel operations expose the cancel button only when the caller supplied a cancellation token and therefore owns cancellation.
- Existing Excel Workspace calls that use the default token still receive the loading shield, but their cancel button remains disabled.
- Transfer itself remains non-cancellable because it mutates the active report synchronously; cancellation applies to the following Preview rebuild.

## Coverage

Architecture guards verify:

- the loading surface is an in-window overlay, not a `Popup`;
- the overlay covers the shell with a high Z-index;
- progress and cancel bindings are present;
- one shared session state is used;
- Excel and transfer decorators are registered at the composition root;
- decorators delegate to the existing OpenXML reader and transfer service;
- decorators contain no OpenXML or Word writer logic;
- WPF presentation is flushed only for a visible main window;
- the flush processes render work but stops before new input;
- Preview passes the operation token to `IReportPreviewRenderer`;
- projection is cancellable;
- UI collection updates are batched at 25 pages and yield between batches;
- no second Preview renderer, report content builder or Word path is introduced.

Infrastructure tests additionally require pre-cancelled workbook-open, sheet-preview, range-detection and WorkingData reads to surface real cancellation.

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
- Architecture: 88
- Infrastructure: 134
- Total: 509

## Required UI smoke

1. Load the same approximately 10,000-row workbook.
2. Confirm `Excel dosyası açılıyor` appears immediately after the file picker closes.
3. Confirm the title advances through Excel preview and data-range detection phases.
4. Press `Word'e Aktar` and confirm `Word'e aktarılıyor` appears before report mutation.
5. Confirm the Preview loading phase takes over and page counts advance in batches.
6. Confirm clicks cannot reach Excel, Preview or Context Dock controls behind the overlay.
7. Move or resize the window during page loading and confirm Windows still paints/responds.
8. Run once to completion and verify the expected page count and content.
9. Run again and click `İptal` during the cancellable Preview phase; confirm the application remains alive and usable.
10. Re-run Preview after cancellation and confirm a complete result can still be produced.
11. Confirm the source XLSX hash remains unchanged.
