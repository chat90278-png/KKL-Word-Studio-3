# Sprint 24 Tranche 10 — Preview / Word Measurement Edge Cases

## Stacked baseline

- Base branch: `sprint24/09-real-docx-large-table-matrix`
- Base head: `4ed2a7d03a4f6973448234f3e02db309e4605972`
- Branch: `sprint24/10-preview-word-measurement-edge-cases`
- PR #22 and PR #23 remain draft and unmerged.
- Tranche 10 must not be retargeted or merged before the lower stacked PRs are validated.

## Windows finding

The first supplied Debug run built with `0 warnings / 0 errors`, but seven historical Sprint 15 Engine pagination tests failed. The attempted fallback-row shrink changed grouped-row, continuation-header, caption and cell-span fragment boundaries. That production behavior change was reverted rather than weakening the established pagination contracts.

The existing compatibility fallback is therefore preserved:

- total horizontal cell inset: `3 mm`;
- total vertical cell inset: `2.5 mm`;
- physical equivalent: `1.5 mm` left/right and `1.25 mm` top/bottom;
- Segoe UI-compatible 10 pt fallback text geometry;
- existing grouped-row and semantic-span pagination behavior unchanged.

No second measurement engine, paginator, Preview renderer, Word writer or exporter is introduced.

## Regression coverage

Three public `DeterministicDocumentLayoutEngine` tests remain:

1. The compatibility fallback and an explicit resolved format with equivalent physical insets must produce the same fragment plan.
2. Larger configured vertical margins must reduce the number of rows fitting on the first Preview page.
3. A `NoWrap` column must not create extra Preview fragments merely because its token is long.

The tests exercise complete public layout results rather than calling internal measurement helpers directly.

## Expected integrated test inventory

```text
Domain           20
Application     299
Engine           71
Architecture    128
Infrastructure  150
-------------------
Total           668 / 668
```

## Combined Windows gate

```bat
git fetch origin
git checkout sprint24/10-preview-word-measurement-edge-cases
git reset --hard origin/sprint24/10-preview-word-measurement-edge-cases

git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

## Manual smoke

1. Compare a compatibility/default table against an explicitly formatted table using `1.5 mm` left/right and `1.25 mm` top/bottom margins.
2. Apply larger top/bottom cell margins and confirm Preview rows grow.
3. Use a narrow `NoWrap` identifier column and confirm long identifiers remain one logical line without causing extra Preview pages.
4. Confirm grouped serial rows, repeated headers and merged identity cells still paginate correctly.
5. Confirm the 50–100 row Word smoke from Tranche 09 remains correct.

No GREEN, ready or merge claim is valid until the exact-head Windows Release gate and manual smoke are supplied.
