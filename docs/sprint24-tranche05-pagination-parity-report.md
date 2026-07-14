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
- `WordContentWriter` translates the same policy to native OpenXML properties.
- Existing Word table writer and DOCX exporter remain authoritative.
- No second paginator, Preview renderer, Word exporter or warning implementation was added.
- UI contains no pagination calculation.
- Warning/Control UX is untouched.

## Test delta

Baseline: `629` tests.

Added cases:

- Application semantic policy: `+9`;
- Engine pagination: `+6`;
- Infrastructure OpenXML pagination: `+4`;
- Architecture: `+4`.

Expected current total:

```text
652 / 652
```

Coverage includes:

- shared semantic policy and up-to-three-row table start;
- complete trailing heading-chain carry;
- no empty page when headings already begin a fresh page;
- caption `KeepNext`;
- leading table-start `KeepNext` chain;
- row `CantSplit`;
- native repeated header row;
- short and long deterministic table fragment continuity;
- caption only on the first table fragment;
- exact row-count/order preservation;
- stable repeated layout plan;
- no second paginator/export fragmenter and no UI pagination ownership.

## Windows stabilization findings

The first supplied Windows run did not include `git rev-parse HEAD`, so it cannot be accepted as an exact-head gate. It exposed the following real failures in the pre-fix branch snapshot:

- `ReportFlowPaginationPolicyTests` constructed `ResolvedTextFormat` without its required fields: `12 × CS9035`.
- `Sprint24HeadingChainPaginationTests` directly referenced internal `LayoutPageFlow`: `1 × CS0122`.
- The architecture test used a broad text search that counted non-implementation/generated source matches as a second layout engine.
- Adding `CantSplit` through OpenXML schema-order normalization removed the existing `TableHeader` marker in the in-memory row properties, breaking the new repeat-header test and the Sprint 18 canonical DOCX regression.

Corrections:

- Test format fixtures now initialize the complete resolved text format contract.
- Heading-chain tests now exercise the public `DeterministicDocumentLayoutEngine` path.
- Architecture source scans exclude `bin/obj` and match concrete class/record inheritance declarations only.
- Word row pagination preserves and restores the native `TableHeader` marker while adding `CantSplit`.

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
652 / 652 tests
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
- First supplied Windows run: RED on a pre-fix snapshot; failures diagnosed and corrected.
- GitHub CI status: no checks are configured/reported for the current head.
- Current exact-head Windows build/test/manual smoke: pending user evidence.
- PR must remain draft and must not be merged until the Windows gate is GREEN.
