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
- The frozen Sprint 15 `TableRowCompositionResult` public shape remains exactly `Rows`, `CellSpans`, `RowGroups`, `Warnings`.
- `TableContentNode.CompositionDiagnostics` exposes the structured Application projection without expanding the frozen composer result.
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
- Unrelated unknown legacy messages remain separate rather than collapsing into one card.
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
- Frozen Sprint 15 table-composition contracts remain unchanged.

## Regression coverage

New Application tests cover:

1. stable composition code/key/affected-column extraction;
2. structured projection while retaining the frozen result and legacy messages;
3. factory code and grouping identity propagation;
4. semantic grouping across different localized messages;
5. separation of different diagnostic codes on the same table;
6. separation of unrelated unknown legacy messages on the same table.

The existing Sprint 20 architecture guard requires structured factory ownership and forbids title/key regex classification inside `PreviewDiagnosticFactory`.

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

## Supplied Windows evidence

The supplied Windows run used Debug commands and did not include `git rev-parse HEAD` or `git status --short`.

Results before the frozen-contract correction:

- Build: `0 warnings / 0 errors`
- Domain: `20/20`
- Application: `294/294`
- Engine: `68/68`
- Architecture: `125/126`
- Infrastructure: `146/146`
- Total: `653 passed / 1 failed / 654 total`

The sole failure was:

```text
Sprint15FrozenContractGuardTests.ApplicationTableContracts_MatchFrozenSprint15Shape
```

Root cause: a public `Diagnostics` property had been added to frozen `TableRowCompositionResult`.

Correction:

- Removed `Diagnostics` from `TableRowCompositionResult`.
- Restored its exact Sprint 15 public property set.
- Kept structured projection at `TableContentNode.CompositionDiagnostics`.
- Updated the new regression test to require that the frozen result exposes no `Diagnostics` property.

## Manual smoke evidence

The supplied UI screenshot confirms:

1. missing-quantity findings are grouped into one card with repeat/key counts;
2. `Tr İsim` and `NSN` merge conflicts remain separate cards on the same table;
3. warning-card navigation reaches the report element and Excel source;
4. the accepted Warning Center visual layout remains unchanged.

Manual structured grouping/navigation smoke: GREEN.

## Exact-head Windows gate

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

## Remaining manual smoke

1. Confirm Error/Warning/Information filters and badge counts remain correct.
2. Export a healthy document and confirm Word generation is unchanged.
3. Confirm an unusable source still fails through the existing exporter contract.

## Gate status

- Source review: complete.
- Supplied Debug build: GREEN `0/0`.
- Supplied automated test run: blocked only by the now-corrected frozen-contract drift.
- Manual structured grouping/navigation smoke: GREEN.
- Container build/test: unavailable in this environment.
- Exact-head Windows Release build/test: pending.
- Final export smoke: pending.
- PR must remain draft until exact-head Windows evidence is GREEN.
