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
- Logical binding identity is separate from the user-visible header so renaming a header does not break binding.

### Source toolbar

- `Veri Aralığını Düzenle` now occupies the removed mapping-button position beside `Word'e Aktar`.
- The duplicate bottom range-editor button was removed.
- The bottom strip now shows only range/provenance and concise status text.

### Word/Preview column order

Selected columns preserve their current physical left-to-right order in the Excel source grid.
Semantic roles continue to control automatic selection and stable binding identity, but they do not rearrange the output table.

Example source order:

1. No
2. Part Number
3. Serial Number
4. NSN
5. Quantity

The same order is used by the resulting `TableElement.Columns`, Preview and Word.

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

### Visible table title

- The popup's table-name field now produces a real bold report text element immediately above the table.
- The title is visible in both Preview and Word because it uses the existing `TextElement` rendering/export path.
- Updating an existing table updates or creates the associated title without replacing the table object.
- The title is not a heading and therefore does not create an extra Contents entry.

## Architecture constraints preserved

- Existing `IExcelReportTransferService` remains the authoritative transfer engine.
- `ExcelTransferPlacementCoordinator` composes placement around that engine; no second renderer, data provider or report engine was introduced.
- Source workbook remains read-only.
- Contents remains a projection of the real ordered report elements.
- Preview and Word consume the same ordered `TableElement.Columns` collection and normal `TextElement` table title.

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

## Windows correction passes

### Pass 1

- Removed obsolete `ColumnMappingRowViewModel.Selection.cs`, which duplicated the generated `IsIncluded` property.
- Updated Sprint 21 architecture guards for the new header-checkbox surface and worksheet-level persistence.

### Pass 2

- Corrected selected-column output from semantic/canonical rearrangement to active Excel grid source order.
- Materialized the popup table name as a visible bold title immediately above the table.
- Updated the remaining ObservableProperty architecture guard without changing its historical method identity.

## Windows gate — pending final head

```bat
git fetch origin
git checkout sprint23/02-grid-column-transfer
git reset --hard origin/sprint23/02-grid-column-transfer
git rev-parse HEAD
git status --short

dotnet clean -c Release
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
8. Output columns follow the selected Excel grid's left-to-right order.
9. The popup table name appears as a bold title immediately above the table.
10. Existing selected table can be updated or a new table can be created.
11. Cancelling the popup leaves the report unchanged.

## Deferred to Tranche 03

- Full persisted root-heading invariant for every new/opened project.
- Final `1 / 1.1 / 1.1.1` numbering resolver shared by Contents, Preview and Word.
- Root delete/outdent protection and legacy-project migration.
- Final sibling-block move/indent/drag-drop semantics.
