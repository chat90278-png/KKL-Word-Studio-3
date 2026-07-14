# Sprint 24 Tranche 02 — Complete Quick Report Structure

## Baseline

- Base: `main@c130e8a1ce70d160c9020f4f13b5fc001c208cfa`
- Branch: `sprint24/02-quick-report-structure`
- Previous Windows gate: build `0/0`, tests `601/601`, startup GREEN

## Product behavior

Quick Report is no longer a tables-only batch action. Every selected worksheet becomes one editable report block:

```text
Heading (optional)
  Alt Heading (optional)
    Table
```

The default values are:

- Heading: workbook file name without extension
- Alt Heading: worksheet name
- Table: worksheet name

Heading and Alt Heading can be independently disabled. Table name remains editable.

## Ordering

- The first time a worksheet is selected, it receives the next `SelectionOrder`.
- The ordered report structure follows this click order across every loaded workbook.
- Deselecting a worksheet removes it and compacts the remaining sequence.
- Selecting it again places it at the end.
- Up/down actions allow explicit correction without deselecting.
- Refreshing loaded workbooks preserves selection order and edited structure metadata for surviving targets.
- The batch orchestrator uses `SelectionOrder` first and workbook/worksheet order only as a compatibility fallback.

## UI

The Hızlı Rapor popup now has two sections:

1. Source worksheets — workbook and worksheet checkboxes with live order badges.
2. Report structure — cards displayed in final report order with:
   - Heading toggle and text
   - Alt Heading toggle and text
   - Table name
   - Move earlier/later actions

The popup width and placement were adjusted for the full structure editor.

## Transfer architecture

- Normal Word'e Aktar and Quick Report both call `ExcelTransferPlacementCoordinator.Transfer`.
- The existing `IExcelReportTransferService` remains the only table transfer engine.
- Quick Report always uses `CreateNewTable`; it never overwrites an existing selected table.
- The current selected report element is the initial stable anchor.
- Each successful table becomes the next selected anchor, chaining complete blocks in click order.
- Existing heading numbering, caption normalization, rollback and source-column order remain authoritative in the placement coordinator.
- No second report tree, Excel reader, renderer or Word exporter was introduced.

## Failure safety

Before each worksheet read, the prior preview and detected end row are cleared. A failed read cannot reuse stale headers/range from the previous worksheet. Placement coordinator rollback removes provisional headings when transfer fails. Completed structures remain; skipped/failed selections stay selected for retry.

## Tests

New Application tests: `+5`

- click-order selection
- deselect/reselect order compaction
- manual up/down reordering
- metadata/order preservation across synchronization
- orchestrator selection-order execution

New Architecture tests: `+3`

- session-only structure metadata containment
- complete ordered editor surface
- shared placement coordinator and stable anchor chaining

Existing Sprint 21 and Sprint 24 architecture guards were evolved to the accepted complete-structure pipeline.

Expected Windows total:

```text
609 / 609
```

## Manual smoke

1. Load two workbooks with multiple worksheets.
2. Select sheets in a mixed order such as B2, A1, B1; confirm badges/cards show 1, 2, 3.
3. Edit every heading, alt heading and table name.
4. Disable one heading and one alt heading on different cards.
5. Move the last card upward and confirm sequence updates.
6. Create the report structure.
7. Confirm Contents and Preview show blocks in the visible card order.
8. Confirm each block uses the edited optional structure and table caption.
9. Confirm numbering is automatically continuous.
10. Export Word and confirm the same order and structure.
