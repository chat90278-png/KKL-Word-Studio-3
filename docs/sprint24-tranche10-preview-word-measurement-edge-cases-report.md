# Sprint 24 Tranche 10 — Preview / Word Measurement Edge Cases

## Stacked baseline

- Base branch: `sprint24/09-real-docx-large-table-matrix`
- Base head: `4ed2a7d03a4f6973448234f3e02db309e4605972`
- Branch: `sprint24/10-preview-word-measurement-edge-cases`
- PR #22 and PR #23 remain draft and unmerged.
- Tranche 10 must not be retargeted or merged before the lower stacked PRs are validated.

## Problem

The deterministic Preview paginator and the authoritative Word writer already share semantic pagination policy, table formats and column profiles. One historical fallback remained inconsistent:

- When `ResolvedTableFormat.Columns` was empty and every configured cell margin was zero, Preview called `EstimateTableRowHeight`.
- That method silently subtracted `3 mm` from every cell width and added `2.5 mm` to every row height.
- The Word writer did not emit those hidden margins. It emitted the resolved zero margins and its standard Segoe UI fallback column format.
- Preview could therefore calculate rows as taller than the corresponding Word rows, especially with narrow columns or long text, and move page boundaries earlier than Word.

## Correction

`DeterministicTextMeasurement.EstimateTableRowHeight` now follows the same fallback geometry as the Word table writer:

- Use the complete resolved fallback cell width.
- Use Segoe UI 10 pt, matching the existing fallback column profile.
- Preserve header bold behavior.
- Do not inject hidden horizontal or vertical padding.
- Keep the existing minimum line-height safety floor.

No second measurement engine, paginator, Preview renderer, Word writer or exporter is introduced.

## Regression coverage

Three public `DeterministicDocumentLayoutEngine` tests were added:

1. An empty column profile and an equivalent explicit Word fallback profile must produce the same page/fragment plan.
2. Real configured cell margins must reduce the number of rows that fit on the first Preview page; zero margins must no longer inherit hidden legacy padding.
3. A `NoWrap` column must not create extra Preview fragments merely because its token is long.

The tests exercise complete public layout results rather than calling the internal measurement helper directly.

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

Run the highest stacked branch to validate Tranches 08–10 together:

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

1. Export a zero-margin table with long wrapped text and compare Preview/Word page transitions.
2. Apply visible top/bottom cell margins and confirm both Preview and Word rows grow.
3. Use a narrow `NoWrap` identifier column and confirm long identifiers remain one logical line without causing extra Preview pages.
4. Confirm the 50–100 row Word smoke from Tranche 09 remains correct.
5. Confirm Excel transfer, diagnostics, Word preflight and the in-memory session remain unchanged.

No GREEN, ready or merge claim is valid until the exact-head Windows Release gate and manual smoke are supplied.
