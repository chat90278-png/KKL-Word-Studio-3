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

- Preview keeps caption + column header + first meaningful table row together through its existing deterministic minimum-fragment measurement.
- Short tables remain one fragment.
- Long tables retain every source row exactly once and repeat column headers on continuation fragments.
- Word captions receive native `KeepNext` so a caption cannot be stranded below a page boundary.
- Every Word table row receives native `CantSplit`, matching Preview's atomic-row model while still allowing a long table to continue on later pages.
- Existing Word `TableHeader` behavior remains authoritative for repeated continuation headers.

### Empty-page safety

- The Preview flow only carries trailing heading blocks when earlier body content exists on the current page.
- No blank paragraph or manual page-break run is introduced in Word.
- Existing native `PageBreakBefore` remains the only explicit table-to-heading transition.

## Architecture

- `ReportFlowPaginationPolicy` remains the single Application semantic policy.
- `LayoutPageFlow` remains the existing Preview page-flow owner.
- `WordContentWriter` translates the same policy to native OpenXML properties.
- Existing deterministic table paginator and Word table writer remain authoritative.
- No second paginator, Preview renderer, Word exporter or warning implementation was added.
- Warning/Control UX is untouched.

## Test delta

Expected current total:

```text
637 / 637
```

Coverage includes:

- shared semantic policy;
- complete trailing heading-chain carry;
- no empty page when headings already begin a fresh page;
- caption `KeepNext`;
- row `CantSplit`;
- native repeated header row;
- short and long deterministic table fragment continuity;
- stable repeated layout plan.

## Windows gate

```bat
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
637 / 637 tests
```

## Manual smoke

1. Fill a page close to the bottom, then place Heading → Alt Heading → captioned table.
2. Confirm all three begin together on the next Preview page.
3. Confirm no blank page appears between the previous content and the moved structure.
4. Use a short table and confirm caption/header/data stay together.
5. Use a long table and confirm continuation pages repeat column headers and no row disappears or duplicates.
6. Export Word and confirm caption is not left alone, rows are not split across pages, and continuation headers repeat.
7. Confirm the existing `Kontrol` area is unchanged by this tranche.
