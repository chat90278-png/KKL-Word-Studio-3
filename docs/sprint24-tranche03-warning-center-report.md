# Sprint 24 Tranche 03 — Actionable Warning Center

## Baseline

- Base: `main@5b907216f19d1bf1ca691efa0ceb09874196ac34`
- Branch: `sprint24/03-warning-center`
- Previous Windows gate: build `0/0`, tests `617/617`, manual Quick Report smoke GREEN

## Problem

Preview diagnostics already carry severity, stable report `ElementId`, source metadata and optional key values. The old warning center rendered every raw occurrence as a separate card and used the raw count as the dock badge. Large reports therefore produced counts such as `806`, even when those entries represented a much smaller number of actionable root causes.

## Implemented behavior

- Raw diagnostics remain intact for rendering and debugging.
- `PreviewDiagnosticSummaryService` creates deterministic user-facing groups by severity, title, normalized message template and report element.
- Occurrence-specific key values embedded inside raw messages are replaced by a display placeholder before grouping.
- Repeated row/key occurrences become one card with a repeat count.
- Distinct key values and source candidates remain available for navigation.
- Different problem types on the same table remain separate actions.
- Equivalent source references are deduplicated.
- Groups are ordered Error → Warning → Information.
- Context Dock badge shows actionable group count, capped at `99+`.
- Badge color turns red when at least one Error group exists.
- Warning Center header shows Error/Warning/Information totals.
- Filters are available for All, Error, Warning and Information.
- Each grouped card shows severity and repeat count.
- Clicking a card opens the report pane, navigates by stable `ElementId`, and then attempts Excel source navigation.
- The first representative key is used for source-cell navigation while all distinct keys remain summarized in the card.

## Architecture

- Grouping lives in Application Preview projection code, not WPF code-behind.
- Domain and persisted Project/Report models remain unchanged.
- No diagnostic is silently deleted from the raw runtime collection.
- Existing Preview and Excel navigation paths remain authoritative.
- No second validator or rendering pipeline is introduced.

## Test delta

- Application: `+5`
- Architecture: `+3`
- Expected total: `625/625`

## Manual smoke

1. Open a report that previously displayed hundreds of warnings.
2. Confirm the dock badge shows grouped action count rather than raw occurrence count.
3. Confirm all `Adet değeri eksik veya geçersiz` occurrences on the same table collapse into one card with an `x tekrar` badge.
4. Confirm different problem types such as missing quantity and conflicting merged values remain separate cards.
5. Switch between Tümü, Hata, Uyarı and Bilgi filters.
6. Click a grouped card and confirm report/Excel navigation still works.
7. Confirm an Error group uses the red badge state.
8. Confirm Preview and Word creation behavior remain unchanged.
