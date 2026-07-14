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

Selected columns preserve the DataGrid's actual current `DisplayIndex` order.
The order is synchronized after generated columns are laid out, so a visual source order such as `A, B, C, E, D, F` reaches the `TableElement` as exactly `A, B, C, E, D, F`.
Semantic roles continue to control automatic selection and stable binding identity, but they do not rearrange the output table.

### Word transfer placement confirmation

`Word'e Aktar` opens one placement confirmation surface instead of mutating immediately.

- Existing selected table: `Var olan <table> tablosunu güncelle` or `Yeni tablo olarak ekle`.
- New table mode shows only the relevant parent chain, not unrelated equal-level siblings.
- Proposed heading, alt heading and table name are editable.
- Heading and alt heading each have a remove action.
- Confirmation creates/updates the table and refreshes report selection/Preview.
- Cancellation performs no report mutation.
- A failed transfer rolls back the proposed root/heading chain.

### Shared visible heading numbering

- The root heading is rendered as `1. System Test Procedure Configuration List`.
- Heading and alt-heading text is normalized as `1.1`, `1.1.1`, `1.2`, `1.2.1`, and so on.
- Re-running the numbering service is idempotent and does not duplicate prefixes.
- Headings remain real `TextElement` objects with the existing heading/alt-heading style presets.
- Contents, Preview and Word therefore consume the same numbered text and the same heading-style identity.
- The popup strips an existing visible prefix before showing its parent line, preventing `1. 1.` duplication.

### Native numbered table caption

- The popup table name is assigned to the existing `TableElement.Caption` contract.
- Preview continues to use the existing document-order caption resolver.
- Word continues to use the existing real `SEQ Tablo` field path.
- A table named `Deneme` renders as centered `Tablo 1: Deneme`.
- Temporary standalone title elements produced by earlier Tranche 02 heads are removed when that table is updated.
- The caption is not a heading and does not create an extra Contents entry.

## Architecture constraints preserved

- Existing `IExcelReportTransferService` remains the authoritative transfer engine.
- `ExcelTransferPlacementCoordinator` composes placement around that engine; no second renderer, data provider or report engine was introduced.
- Source workbook remains read-only.
- Contents remains a projection of the real ordered report elements.
- Preview and Word consume the same ordered `TableElement.Columns`, heading elements and native `TableElement.Caption`.

## Test delta

Baseline after Tranche 01: `552`

Added:

- Domain: `+2`
- Application: `+8`
- Expected total: `562`

Expected project totals:

- Domain: `20`
- Application: `242`
- Engine: `60`
- Architecture: `101`
- Infrastructure: `139`
- Total: `562`

## Windows correction passes

### Pass 1

- Removed obsolete `ColumnMappingRowViewModel.Selection.cs`, which duplicated the generated `IsIncluded` property.
- Updated Sprint 21 architecture guards for the new header-checkbox surface and worksheet-level persistence.

### Pass 2

- Corrected selected-column output from semantic/canonical rearrangement to source-order projection.
- Updated the remaining ObservableProperty architecture guard without changing its historical method identity.

### Pass 3

- Synchronized output order from the live DataGrid `DisplayIndex`, not stale persisted/source-letter ordering.
- Replaced the temporary standalone table title with the established centered `Tablo n:` caption pipeline.
- Added the shared visible heading-numbering policy and preserved real heading styles.

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
3. Editing the worksheet header row survives rerender and appears in the report table header.
4. `Word'e Aktar` opens the placement confirmation popup.
5. Heading/alt-heading/table name are editable; remove buttons remove the proposed row.
6. Output columns follow the Excel grid's actual visible left-to-right order.
7. Preview shows `1.`, `1.1`, and `1.1.1` numbering on real heading elements.
8. Contents shows the same numbered headings.
9. The table caption renders centered as `Tablo 1: <name>`.
10. Existing selected table can be updated or a new table can be created.
11. Cancelling the popup leaves the report unchanged.

## Deferred to Tranche 03

- Full persisted root-heading invariant for every newly created/opened legacy project, independent of transfer.
- Root delete/outdent protection and legacy-project migration.
- Automatic renumber invocation after every non-transfer structure move/indent/drag-drop operation.
- Final sibling-block move/indent/drag-drop semantics.
