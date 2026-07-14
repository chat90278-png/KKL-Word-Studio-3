# Sprint 24 Tranche 02 — Complete Quick Report Structure

## Baseline

- Base: `main@c130e8a1ce70d160c9020f4f13b5fc001c208cfa`
- Branch: `sprint24/02-quick-report-structure`
- Previous merged Windows gate: build `0/0`, tests `601/601`, startup GREEN
- Earlier Tranche 02 head before parent targeting: build `0/0`, tests `609/609`, startup GREEN

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

## Explicit outline ownership

A block can never silently fall back to the document root. The editor applies four explicit rules:

1. Heading ON, Alt Heading ON
   - create new Heading
   - create new Alt Heading under it
   - create Table under the Alt Heading
2. Heading ON, Alt Heading OFF
   - create new Heading
   - create Table under it
3. Heading OFF, Alt Heading ON
   - require an explicit existing/earlier **Heading** selection
   - create the new Alt Heading under that Heading
   - create Table under the new Alt Heading
4. Heading OFF, Alt Heading OFF
   - require an explicit existing/earlier **Alt Heading** selection
   - create only the Table inside that Alt Heading block

The selector contains only valid targets at the required level:

- headings/alt headings already present in the active report
- headings/alt headings that an earlier Quick Report card will create

Later cards cannot target future cards. Reordering a card recalculates its allowed targets and removes a reference that is no longer valid. The create command remains unavailable until every selected block has a valid required target.

Existing report targets use stable element IDs. Earlier Quick Report targets use the source card key while editing, then resolve to the actual created heading ID during the ordered batch. If the source block fails, the dependent block is safely skipped instead of attaching elsewhere.

## Ordering

- The first time a worksheet is selected, it receives the next `SelectionOrder`.
- The ordered report structure follows this click order across every loaded workbook.
- Deselecting a worksheet removes it and compacts the remaining sequence.
- Selecting it again places it at the end.
- Up/down actions allow explicit correction without deselecting.
- Refreshing loaded workbooks preserves selection order, edited structure metadata and surviving target references.
- The batch orchestrator uses `SelectionOrder` first and workbook/worksheet order only as a compatibility fallback.
- Multiple tables attached to the same selected Alt Heading are appended inside that block in click order.

## UI

The Hızlı Rapor popup has two sections:

1. Source worksheets — workbook and worksheet checkboxes with live order badges.
2. Report structure — cards displayed in final report order with:
   - Heading toggle and text
   - Alt Heading toggle and text
   - level-specific `Üst Başlık` or `Alt Başlık` selector when required
   - Table name
   - Move earlier/later actions

The popup width and placement were adjusted for the full structure editor.

## Transfer architecture

- Normal Word'e Aktar and Quick Report both call `ExcelTransferPlacementCoordinator.Transfer`.
- The existing `IExcelReportTransferService` remains the only table transfer engine.
- Quick Report always uses `CreateNewTable`; it never overwrites an existing selected table.
- Full blocks continue to chain from the prior successful table.
- Parent-targeted blocks resolve and validate their required Heading/Alt Heading ID before transfer.
- The placement coordinator rejects a missing or wrong-level target.
- The coordinator owns heading creation, block insertion, rollback, numbering, caption normalization and source-column order.
- No second report tree, Excel reader, renderer or Word exporter was introduced.

## Failure safety

Before each worksheet read, the prior preview and detected end row are cleared. A failed read cannot reuse stale headers/range from the previous worksheet. Placement coordinator rollback removes provisional headings when transfer fails. Completed structures remain; skipped/failed selections stay selected for retry.

A removed target, changed target level or failed earlier source card never causes root fallback. That single block is rejected/skipped with a user-facing explanation.

## Tests

Original Tranche 02 delta:

- Application: `+5`
- Architecture: `+3`

Parent-targeting delta:

- Application target-reference tests: `+5`
- Application real placement-coordinator tests: `+3`
- Existing Architecture guards evolved without increasing their count

New coverage includes:

- heading-required and alt-heading-required mode selection
- target-reference synchronization and stale-reference removal
- created heading identity propagation through the batch
- real Heading → Alt Heading → Table placement
- table-only insertion into an explicit Alt Heading block
- wrong-level target rejection without mutation
- level-specific selector and existing/earlier target containment
- same-alt-heading table append ordering

Expected Windows total:

```text
617 / 617
```

## Manual smoke

1. Load two workbooks with multiple worksheets.
2. Select sheets in a mixed order such as B2, A1, B1; confirm badges/cards show 1, 2, 3.
3. Keep one card as Heading + Alt Heading + Table.
4. On a later card disable Heading, keep Alt Heading, and select the earlier card's Heading.
5. On another later card disable both Heading and Alt Heading, and select an earlier/existing Alt Heading.
6. Confirm `Rapor Yapısını Oluştur` is disabled while a required selector is empty.
7. Move a referenced card after its dependent card; confirm the now-invalid target is cleared.
8. Restore the order/target and create the report structure.
9. Confirm Contents and Preview show the requested ownership and click order.
10. Attach two table-only cards to the same Alt Heading and confirm both remain ordered in that block.
11. Export Word and confirm the same order, levels and captions.
