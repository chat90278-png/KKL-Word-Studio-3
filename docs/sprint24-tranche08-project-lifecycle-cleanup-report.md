# Sprint 24 Tranche 08 — Legacy Project Lifecycle Cleanup

## Baseline

- Base: `main@78f8f1cc9a5cb2130bafd5031bbbe30a89664ed8`
- Branch: `sprint24/08-project-lifecycle-cleanup`
- Previous supplied Windows gate: build `0 warnings / 0 errors`, tests `660/660`.
- Tranche 07 Word export preflight is already merged.

## Goal

Remove the unused native `.kws` open/save lifecycle from production while retaining the existing Project aggregate as the one process-lifetime report/transfer workspace.

This is a lifecycle cleanup, not a report-model rewrite. Excel transfer, working data, Preview, diagnostics, layout, pagination and Word export continue to consume the same in-memory Project/Report graph.

## Production removal

Removed from production:

- `IProjectService`;
- `KwsProjectRepository`;
- `KwsProjectManifest`;
- Infrastructure DI registration for native project persistence;
- `.kws` extension/format constants;
- `MainViewModel` dependency on a repository-shaped project service.

No native project open, save, save-as, ZIP package, manifest, recent-project or materialization path remains in the application runtime.

## In-memory workspace bootstrap

`WorkspaceSessionFactory.CreateDefault()` now owns creation of the one process-lifetime session aggregate:

- one independent `Project`;
- one `Report`;
- one Page;
- one Body Section.

`MainViewModel` installs this aggregate into the established singleton `IWorkspace` and continues using it for transfer, Preview, diagnostics and Word export.

The application deliberately does not claim persistence: closing the process ends the session.

## Imported Word sources

Front matter and reference-format DOCX files remain read-only session sources:

- validation still uses OpenXML read-only mode;
- sources are never rewritten;
- the selected source path remains available during the process lifetime;
- Word front-matter composition and reference-format resolution remain unchanged.

Two embedded-entry members remain hidden with `EditorBrowsable(Never)` only for historical test/data-contract compatibility. No production repository or DI path consumes them.

## Historical regression compatibility

Sprint 15 baseline inventory requires historical test file and method identities to remain present. Their identities are preserved, while obsolete persistence assertions are retargeted to current behavior:

- table captions, working-data edits and multi-source order remain reachable on the authoritative in-memory aggregate;
- read-only front-matter import and Word composition remain covered;
- production assemblies are verified not to contain `IProjectService`, `KwsProjectRepository` or `KwsProjectManifest`.

Post-Sprint-15 reference-format tests still exercise old portable-asset packages through `LegacyKwsProjectRepositoryFixture`, which exists only in the Infrastructure test assembly. It is not a production service and is never registered in DI.

## Architecture guard

`NativeProjectPersistence_IsAbsentFromProductionComposition` scans every production `.cs` file and rejects:

- `IProjectService`;
- `KwsProjectRepository`;
- `KwsProjectManifest`;
- `ProjectFileExtension`;
- `ProjectFileFormatVersion`;
- a `.kws` string literal.

It also verifies that the removed production files do not exist.

## Documentation

The active README now describes the current Excel-first process-lifetime session and explicitly states that native project open/save is not a product capability. Historical ADRs and sprint reports remain as decision history rather than current runtime documentation.

## Boundaries

Unchanged:

- Domain Project/Report/DataSource/WorkingData aggregate structure;
- one Excel reader;
- one normal/Quick Report transfer engine;
- one report-content builder;
- one Preview renderer;
- one deterministic layout/pagination engine;
- one Word exporter;
- structured diagnostic and Word preflight behavior.

No automatic serialization replacement, database, autosave, hidden cache, or second workspace was introduced.

## Expected test inventory

- Domain: `20`
- Application: `299`
- Engine: `68`
- Architecture: `128`
- Infrastructure: `146`

Expected total:

```text
661 / 661
```

## Required Windows gate

```bat
git fetch origin
git checkout sprint24/08-project-lifecycle-cleanup
git reset --hard origin/sprint24/08-project-lifecycle-cleanup

git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

- exact branch head reported by the PR;
- empty `git status --short`;
- build `0 warnings / 0 errors`;
- tests `661/661`.

## Manual smoke

1. Application starts with one usable default report workspace.
2. No New/Open/Save/Save As native-project action or `.kws` dialog exists.
3. Open Excel, edit range/data, and perform normal `Word'e Aktar`.
4. Build a Hızlı Rapor and confirm it uses the same report session.
5. Add a front-matter DOCX and a reference-format DOCX; confirm both remain usable while their source files exist.
6. Confirm structured warning navigation and Word export preflight still work.
7. Export a valid DOCX and reopen it in Microsoft Word/OpenXML.
8. Close/restart the application and confirm a fresh session is created; no hidden project restore is attempted.

## Gate status

- Static source review: complete.
- Exact-head Windows Release build/test: pending.
- Manual session/transfer/import/export smoke: pending.
- PR must remain draft until both gates are supplied.
