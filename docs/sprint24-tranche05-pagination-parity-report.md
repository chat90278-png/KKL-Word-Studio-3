# Sprint 24 Tranche 05 — Preview / Word Pagination Parity

## Baseline

- Base: `main@493ccc0ddbf3a032b90c310fa3743cb475fb17d9`
- Branch: `sprint24/05-pagination-parity`
- Abandoned warning redesign PR #18 is not part of this branch.
- Previous merged-main Windows gate: build `0/0`, tests `629/629`.

## Scope

### Heading-chain cohesion

- A trailing `Heading → Alt Heading` chain is carried to the next Preview page when the following table start cannot fit.
- The complete chain is moved, not only the final alt heading.
- A heading chain already at the top of a fresh page is not moved again; this prevents an empty intermediate page.
- Existing `Table → Heading/Alt Heading` forced page transition remains intact.
- Word headings continue to use native `KeepNext`, so Heading1 → Heading2 → caption/table follows the same semantic chain.

### Table start and continuation

- One semantic policy defines a table-start target of caption + column header + up to three meaningful data rows, naturally shrinking for short tables.
- `DeterministicTablePaginator` consumes this policy for both heading preflight measurement and actual fragment-start placement.
- Caption/header-only Preview fragments are no longer emitted when the first data row must move; caption, header and at least one row stay together.
- Short tables remain one fragment.
- Long tables retain every source row exactly once and repeat column headers on continuation fragments.
- Word captions receive native `KeepNext` so a caption cannot be stranded below a page boundary.
- Word applies native `KeepNext` through the header and leading data rows to express the same table-start target without rebuilding the semantic table.
- Every Word table row receives native `CantSplit`, matching Preview's atomic-row model while still allowing a long table to continue on later pages.
- Existing Word `TableHeader` behavior remains authoritative for repeated continuation headers.

### Empty-page safety

- The Preview flow only carries trailing heading blocks when earlier body content exists on the current page.
- No blank paragraph or manual page-break run is introduced in Word.
- Existing native `PageBreakBefore` remains the only explicit table-to-heading transition.

## Architecture

- `ReportFlowPaginationPolicy` remains the single Application semantic policy.
- `LayoutPageFlow` remains the existing Preview page-flow owner.
- `DeterministicTablePaginator` remains the single Preview table-fragment owner.
- `DeterministicDocumentLayoutEngine` is the only concrete `IDocumentLayoutEngine` implementation.
- The obsolete Sprint 14 bootstrap `FallbackDocumentLayoutEngine` was removed; it was not registered in DI and contradicted the one-engine contract.
- `WordContentWriter` translates the same policy to native OpenXML properties.
- Existing Word table writer and DOCX exporter remain authoritative.
- No second paginator, Preview renderer, Word exporter or warning implementation was added.
- UI contains no pagination calculation.
- Warning/Control UX is untouched.

## Test total

The earlier `652/652` expectation was incorrect. Windows test discovery established these project totals after removing the accidental duplicate contract-test file:

- Domain: `20`
- Application: `288`
- Engine: `68`
- Architecture: `126`
- Infrastructure: `146`

Current exact-suite target:

```text
648 / 648
```

The obsolete production fallback engine is removed while the baseline test file and method identities are preserved. Those legacy-named tests execute the authoritative deterministic engine without adding duplicate test methods.

Coverage includes:

- shared semantic policy and up-to-three-row table start;
- complete trailing heading-chain carry;
- no empty page when headings already begin a fresh page;
- automatic-numbered caption parity with equivalent visible caption;
- caption `KeepNext`;
- leading table-start `KeepNext` chain;
- row `CantSplit`;
- native repeated header row and row-height preservation;
- short and long deterministic table fragment continuity;
- caption only on the first table fragment;
- exact row-count/order preservation;
- stable repeated layout plan;
- exactly one concrete layout engine;
- no second paginator/export fragmenter and no UI pagination ownership.

## Windows stabilization findings

The supplied Windows runs did not include `git rev-parse HEAD`, so they cannot be accepted as exact-head gates. Across the runs they exposed:

- incomplete `ResolvedTextFormat` and `ResolvedTableFormat` test fixtures;
- a test directly referencing internal Preview flow code;
- OpenXML row-property mutation removing `TableHeader` or `TableRowHeight`;
- an older caption-boundary assertion conflicting with the Sprint 24 heading-chain rule;
- brittle source-regex architecture checks;
- a real second `IDocumentLayoutEngine`: the unused Sprint 14 fallback bootstrap;
- a Sprint 15 baseline test still constructing the removed fallback type;
- baseline inventory requiring the historical fallback test file and three method identities to remain present;
- a legacy cell-span test asserting collection reference identity although the deterministic engine preserves equivalent span values;
- an accidental duplicate deterministic contract-test file increasing Engine discovery from `68` to `71`.

Corrections:

- Test format fixtures initialize their complete required contracts.
- Heading-chain tests exercise public `DeterministicDocumentLayoutEngine` behavior.
- Automatic numbered-caption fidelity compares automatic and equivalent visible captions under the current pagination rule.
- Word row pagination adds `CantSplit` without rebuilding row properties, preserving native header and height properties.
- Architecture verification inspects compiled Engine types.
- The unused production fallback engine was removed.
- The Sprint 15 cell-span contract runs through `DeterministicDocumentLayoutEngine` and compares span content by value.
- The baseline fallback test filename and method names remain intact, but their implementation validates deterministic-engine compatibility.
- The duplicate `DocumentLayoutEngineContractTests.cs` file was removed so each compatibility test is discovered once.

The latest supplied run built successfully with `0 warnings / 0 errors`. Its test result was `650 passed / 1 failed / 651 total`; the sole failure was the stale `Assert.Same` cell-span assertion, and the three extra tests came from the duplicate contract-test file. Both issues are corrected, but a new exact-head Windows run is still required.

## Windows gate

```bat
git fetch origin
git checkout sprint24/05-pagination-parity
git reset --hard origin/sprint24/05-pagination-parity
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
648 / 648 tests
```

## Manual smoke

1. Fill a page close to the bottom, then place Heading → Alt Heading → captioned table.
2. Confirm all three begin together on the next Preview page with the header and meaningful starting rows.
3. Confirm no blank page appears between the previous content and the moved structure.
4. Use a short table and confirm caption/header/data stay together without an unnecessary new page.
5. Use a 50–100 row table and confirm continuation pages repeat column headers and no row disappears, duplicates or changes order.
6. Place two tables consecutively and confirm no blank intermediate or trailing page appears.
7. Export Word and compare the same logical decisions: heading carry, caption attachment, table start and repeated header behavior.
8. Confirm no manual/automatic double page break is visible.
9. Confirm the existing `Kontrol` area is unchanged by this tranche.

## Gate status

- Source review: complete.
- Latest supplied Windows build: `0 warnings / 0 errors` on an unverified head.
- Latest supplied Windows tests: `650 passed / 1 failed / 651 total`; the remaining value-vs-reference assertion and duplicate test file are corrected.
- Baseline test inventory compatibility remains intact without reintroducing a production fallback engine.
- GitHub CI status: no checks are configured/reported for the current head.
- Current exact-head Windows build/test/manual smoke: pending user evidence.
- PR must remain draft and must not be merged until the Windows gate is GREEN.
