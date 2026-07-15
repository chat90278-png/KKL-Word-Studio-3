# Sprint 24 Tranche 06 — Structured Diagnostics Contract

## Baseline

- Base: `main@c2fb360b1071c099aaeceab8993b4e480cb01020`
- Branch: `sprint24/06-structured-diagnostics-contract`
- Closed/unmerged PR #18 is not reused or cherry-picked.
- Previous merged-main Windows gate: build `0/0`, tests `648/648`, pagination smoke GREEN.

## Goal

Remove message-text classification and grouping from Preview/UI consumers without changing the accepted Warning Center appearance or introducing a second validator.

## Scope

### Composition boundary

- Added stable table-composition diagnostic codes for configuration, quantity, merge-conflict and serial findings.
- Added `TableCompositionDiagnostic` with original technical message, record key and affected-column metadata.
- Existing `Warnings` / `CompositionWarnings` string collections remain available for frozen compatibility and support logs.
- `TableRowCompositionResult.Diagnostics` and `TableContentNode.CompositionDiagnostics` expose the structured Application projection.
- Legacy text interpretation is isolated in one compatibility classifier rather than repeated by Preview or UI code.

### Preview diagnostic factory

- Added a single Application-owned diagnostic catalog for code → severity/title mapping.
- `PreviewDiagnostic` now carries stable `Code`, factory-owned `GroupingKey` and optional `AffectedColumn`.
- `PreviewDiagnosticFactory` consumes structured composition findings directly.
- The factory no longer contains title-resolution or key-extraction regex logic.
- Raw technical messages remain intact for existing cards and support/debugging.
- Report element, Excel source, worksheet, range and key-column metadata remain unchanged.

### Grouping

- Production diagnostics group by factory-owned semantic identity instead of normalized localized messages.
- Different localized/raw messages can represent one actionable semantic problem.
- Different problem codes on the same table remain separate actions.
- Unknown legacy messages retain message-specific identity so unrelated findings cannot collapse into one card.
- A narrow legacy fallback preserves historical manually-created diagnostic tests/callers that do not yet provide a grouping key.
- Error → Warning → Information ordering, source deduplication, distinct key collection and occurrence counts remain intact.

### UX and export boundaries

- Existing Warning Center visuals, filters, badge and navigation are unchanged.
- Existing Preview element navigation and Excel source/key navigation remain authoritative.
- No automatic repair, warning suppression, persisted warning history or replacement validation engine is added.
- No export confirmation dialog or rejected PR #18 control-center direction is reintroduced.
- Word generation and current exporter failure contracts remain unchanged.

## Architecture

- Structured diagnostic contracts live in Application.
- Domain and persistence remain untouched.
- `PreviewDiagnosticsStore` remains the only runtime diagnostic store.
- `PreviewDiagnosticFactory` remains the single report-content → runtime diagnostic projection.
- `PreviewDiagnosticSummaryService` remains the single actionable grouping projection.
- UI does not classify warning text or own diagnostic business rules.
- Preview renderer, deterministic layout engine and Word exporter remain unchanged.

## Regression coverage

New Application tests cover:

1. stable composition code/key/affected-column extraction;
2. structured composition result while retaining legacy messages;
3. factory code and grouping identity propagation;
4. semantic grouping across different localized messages;
5. separation of different diagnostic codes on the same table;
6. separation of unrelated unknown legacy messages on the same table.

The existing Sprint 20 architecture guard now requires structured factory ownership and forbids title/key regex classification inside `PreviewDiagnosticFactory`.

## Expected test inventory

- Domain: `20`
- Application: `294` (`288 + 6`)
- Engine: `68`
- Architecture: `126`
- Infrastructure: `146`

Expected total:

```text
654 / 654
```

## Windows gate

```bat
git fetch origin
git checkout sprint24/06-structured-diagnostics-contract
git reset --hard origin/sprint24/06-structured-diagnostics-contract

git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

```text
0 warnings
0 errors
654 / 654 tests
```

## Manual smoke

1. Produce two missing-quantity warnings for different PN/key values on the same table; confirm one grouped card with two keys.
2. Produce a quantity warning and a conflicting-column warning on the same table; confirm two separate cards.
3. Produce two unrelated, unclassified table warnings; confirm they remain two separate cards.
4. Click each card and confirm Preview navigation still reaches the stable report element.
5. Confirm Excel source/sheet/range/key navigation remains available.
6. Confirm Error/Warning/Information filters and badge counts remain correct.
7. Confirm the accepted Warning Center layout has not changed.
8. Export a healthy document and confirm Word generation is unchanged.
9. Confirm an unusable source still fails through the existing exporter contract.

## Gate status

- Source review: complete.
- Container build/test: unavailable in this environment.
- GitHub Actions: no configured checks observed on the previous merged head.
- Exact-head Windows Release build/test: pending.
- Manual structured-grouping/navigation/export smoke: pending.
- PR must remain draft until exact-head Windows evidence is GREEN.
