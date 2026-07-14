# Sprint 23 Tranche 02 — Grid, Column Selection and Transfer Placement Report

## Baseline

- Base branch: `main`
- Base SHA: `07087e3764c79d598cb81e658881ba08b71f53a4`
- Working branch: `sprint23/02-grid-column-transfer`

## Scope implemented

### Excel source grid

- DataGrid sorting is disabled globally and per generated column.
- Excel-style coordinate headers remain `A / B / C / ...`; working-data display headers no longer replace the coordinate row.
- Each coordinate header contains a compact transfer checkbox.
- The original worksheet header remains a real row beneath the coordinate headers.
- When project-owned WorkingData exists, its header row is projected back into the grid instead of disappearing.
- Header-cell edits update project-owned working header metadata, remain undoable, and never write to the source workbook.
- Data-row edit/clear/paste/insert/delete routing accounts for the projected working-data header row.

### Column transfer selection

- Removed the normal `Sütunları Eşle` button/drawer flow from the source surface.
- Canonical Turkish/English semantic roles from Tranche 01 drive initial transfer selection.
- Known standard fields are selected automatically.
- When English and Turkish Part Name both exist, English is selected by default and Turkish remains available but unchecked.
- Unknown extra columns remain visible and unchecked; the user can explicitly include one from the grid header.
- Selection, semantic role, logical field identity and edited display header persist per workbook/worksheet.
- Logical binding identity is separate from the user-visible header so renaming a header does not break canonical ordering.

### Source toolbar

- `Veri Aralığını Düzenle` now occupies the removed mapping-button position beside `Word'e Aktar`.
- The duplicate bottom range-editor button was removed.
- The bottom strip now shows only range/provenance and concise status text.

### Fixed Word table order

The shared Application coordinator orders selected standard fields as:

1. No
2. Part Name
3. Part Number
4. NSN
5. Serial Number
6. Quantity

Source Excel order remains untouched. Explicitly selected unknown fields follow the canonical fields in source order. The resulting `TableElement.Columns` order is shared by Preview and Word.

### Word transfer placement confirmation

`Word'e Aktar` now opens one placement confirmation surface instead of mutating immediately.

- Existing selected table: `Var olan <table> tablosunu güncelle` or `Yeni tablo olarak ekle`.
- New table mode shows only the relevant parent chain, not unrelated equal-level siblings.
- Proposed heading, alt heading and table name are editable.
- Heading and alt heading each have a remove action.
- Confirmation creates/updates the table and refreshes report selection/Preview.
- Cancellation performs no report mutation.
- A failed transfer rolls back the proposed root/heading chain.
- A first new transfer establishes the fixed document-root heading metadata; full numbering/deletion policy is completed in Tranche 03.

## Architecture constraints preserved

- Existing `IExcelReportTransferService` remains the authoritative transfer engine.
- `ExcelTransferPlacementCoordinator` composes placement around that engine; no second renderer, data provider or report engine was introduced.
- Source workbook remains read-only.
- Contents remains a projection of the real ordered report elements.
- Preview and Word consume the same ordered `TableElement.Columns` collection.

## Test delta

Baseline after Tranche 01: `552`

Added:

- Domain: `+2`
- Application: `+5`
- Expected total: `559`

Expected project totals:

- Domain: `20`
- Application: `239`
- Engine: `60`
- Architecture: `101`
- Infrastructure: `139`
- Total: `559`

## Windows gate — pending

Run against the final exact branch head:

```bat
git fetch origin
git checkout sprint23/02-grid-column-transfer
git reset --hard origin/sprint23/02-grid-column-transfer
git rev-parse HEAD
git status --short

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Manual smoke:

1. Source headers display `A/B/C/...` and clicking them does not sort rows.
2. Header checkboxes include/exclude transfer columns.
3. English Part Name is preferred when both language columns exist.
4. Editing the worksheet header row survives rerender and appears in the report table header.
5. `Veri Aralığını Düzenle` appears beside `Word'e Aktar`; the lower duplicate is gone.
6. `Word'e Aktar` opens the placement confirmation popup.
7. Heading/alt heading/table name are editable; remove buttons remove the proposed row.
8. New table output follows the fixed semantic order regardless of Excel source order.
9. Existing selected table can be updated or a new table can be created.
10. Cancelling the popup leaves the report unchanged.

## Deferred to Tranche 03

- Full persisted root-heading invariant for every new/opened project.
- Final `1 / 1.1 / 1.1.1` numbering resolver shared by Contents, Preview and Word.
- Root delete/outdent protection and legacy-project migration.
- Final sibling-block move/indent/drag-drop semantics.
