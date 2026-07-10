# KKL Word Studio — Sprint 10 Windows Stabilization Report

## Objective

Resolve the seven real Windows/.NET 8 compile errors reported against the
Sprint 10 baseline (`KKL_WordStudio-Sprint10.zip`). No features, no Sprint 11
work, no architectural refactoring. The fix is confined to a single test file.

## Windows Ground Truth (as supplied)

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED — 0 warnings, 7 errors
- All 7 errors located in
  `tests/KKL.WordStudio.Infrastructure.Tests/Sprint10MultiSourceTests.cs`

### Reported errors

| Error  | Symbol    | Lines (approx.)                    |
|--------|-----------|------------------------------------|
| CS0104 | Workbook  | 51, 177, 204, 264, 328, 396        |
| CS0104 | DataField | 271                                |

### Namespace collisions

`Workbook`:
- `KKL.WordStudio.Domain.DataSources.Workbook`
- `DocumentFormat.OpenXml.Spreadsheet.Workbook`

`DataField`:
- `KKL.WordStudio.Domain.DataBinding.DataField`
- `DocumentFormat.OpenXml.Spreadsheet.DataField`

Both collisions arise because the test file legitimately imports
`DocumentFormat.OpenXml.Spreadsheet` (used to build a real XLSX fixture via
`workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();`)
alongside the KKL Domain namespaces. The compiler cannot disambiguate the
unqualified `Workbook` / `DataField` type names used in the Domain-model object
constructions.

## Root Cause

This is purely a name-resolution ambiguity, not a logic or architecture defect.
The `DocumentFormat.OpenXml.Spreadsheet` import is required and correct; the
Domain-model constructions simply need explicit disambiguation.

## Fix Applied (smallest viable change)

The file already followed an alias convention for other colliding Domain types
(`DomainTableColumn`, `DomainWorksheet`, `DomainPage`). The same convention was
extended:

```csharp
using DomainWorkbook  = KKL.WordStudio.Domain.DataSources.Workbook;
using DomainDataField = KKL.WordStudio.Domain.DataBinding.DataField;
```

Only the ambiguous **Domain-model constructions** were re-typed:

- `new Workbook { ... }`  → `new DomainWorkbook { ... }`  (6 sites)
- `new DataField { ... }` → `new DomainDataField { ... }` (1 site)

### Explicitly preserved

- `workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();`
  — the real OpenXML XLSX fixture construction remains OpenXML, unchanged.
- The `using DocumentFormat.OpenXml.Spreadsheet;` import — retained.
- Property-initializer identifiers (`Workbook = ...`, the
  `ExcelDataSource.Workbook` property, `dataSource.Workbook.Worksheets`) — these
  are member names, never type references, and require no change.

Nothing was renamed, reimplemented, mocked away, deleted, skipped, or weakened.

## Change Summary

| File                                                              | Change                                           |
|-------------------------------------------------------------------|--------------------------------------------------|
| `tests/.../Sprint10MultiSourceTests.cs`                           | +2 alias usings; 6 `Workbook` + 1 `DataField` constructions re-typed to Domain aliases |

Total: 1 file touched. No production/`src` code modified.

## Static Verification Performed

Because the sandbox has no .NET SDK and the SDK download hosts are outside the
network allowlist, a full `dotnet build` / `dotnet test` could not be executed
here. The following static checks were performed instead:

- Every reported error line (51, 177, 204, 264, 271, 328, 396) now uses an
  explicit Domain alias.
- No bare `new Workbook {` or `new DataField {` construction remains.
- The single OpenXML `Workbook()` construction is intact.
- The `DocumentFormat.OpenXml.Spreadsheet` import is intact.
- Test-method count unchanged: **94** `[Fact]`/`[Theory]` methods across the
  test projects.

CS0104 is triggered only by unqualified *type references* in `new T { }`
expressions and declarations; all such references for the two colliding symbols
are now uniquely aliased, which removes the ambiguity that produced all seven
errors.

## Verification Honesty

`dotnet` is not available in this environment and the .NET SDK hosts are not in
the egress allowlist, so no build/test command output can be presented. The
exact prescribed source fixes have been applied. **Windows/.NET 8 verification
is pending and remains the final source of truth.** Expected result on Windows:
build green (0 errors), with the existing 94 tests running as before.

## Invariants Preserved (unaffected by this change — no `src` edits)

- TableElement.Sources ordered persisted collection
- Legacy TableElement.Binding fallback
- TableColumn stable Id
- Header / SourceField separation
- Source-specific TableColumn.Id → provider SourceField mapping
- Pinned DataRange per table source
- Working-data-first provider precedence
- Worksheet.ColumnMappings ownership
- Multi-source rows append in configured source order
- Shared ReportContentDocument Preview/Word path
- Missing-source explicit error behavior
- Original XLSX/XLSM immutability
- Sprint 8 caption / front-matter behavior
- All existing 94 test methods

## Scope Confirmation

No Sprint 11 work. No Undo/Redo, find/replace, relink, autosave, report-structure
redesign, pagination, PDF, shell redesign, generic Engine, or Office COM/Interop
was added or touched.
