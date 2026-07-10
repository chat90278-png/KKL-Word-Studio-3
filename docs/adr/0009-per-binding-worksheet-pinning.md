# ADR 0009: Per-Binding Worksheet Pinning

## Status
Accepted

## Context
Before building the Variant 2.5 Table Binding UI (which explicitly presents
"bind this table to Sheet1 / Sheet2 / Sheet3 of this Excel file" as its
core interaction), the existing binding-resolution architecture was
required to be tested against that exact scenario, not assumed correct.

**Scenario tested:** one `ExcelDataSource` wrapping a `Workbook` with two
worksheets; `Table1` bound to Sheet1, `Table2` bound to Sheet2; the user
repeatedly changes which worksheet is "active" (i.e. currently browsed in
the Excel Workspace) between report builds.

**Defect found:** `ExcelDataProvider.GetRowsAsync` resolved the worksheet
to read via `ExcelDataSource.ActiveWorksheetName` — a single mutable field
on the DataSource, shared by every `Binding` that pointed at it. `Binding`
itself had no field to say *which worksheet* a given table actually meant.
Practically: Table1 and Table2 would both silently follow whatever
worksheet the user happened to be browsing, rather than staying pinned to
the worksheet they were bound to. `TableWorksheetBindingStabilityTests`
encodes this scenario and would have been impossible to even express
against the old `Binding` shape (it had nowhere to record "Sheet1" vs
"Sheet2" per binding) — the missing field is the defect.

## Decision
Added `Binding.WorksheetName` (nullable `string`). `IDataProvider.GetRowsAsync`
gained an optional `worksheetNameOverride` parameter (placed after
`cancellationToken` so no existing positional call site could silently
break). `ExcelDataProvider` now resolves
`worksheetNameOverride ?? excelDataSource.ActiveWorksheetName` — an
explicit per-binding worksheet always wins; falling back to
`ActiveWorksheetName` keeps every `Binding` created before this field
existed working exactly as it did (null WorksheetName = old behavior,
unchanged). `ReportContentBuilder` passes `table.Binding.WorksheetName`
through when resolving a bound table's rows.

## Why this is the smallest correct fix, not a broader redesign
This was evaluated against exactly the alternative the task warned against
("a preselected DataSourceId/WorksheetId redesign"). Rejected because it
isn't needed: `Binding.DataSourceName` already identifies *which*
DataSource; the only missing piece was *which worksheet within it* for
that specific binding — a single nullable field plus one optional,
backward-compatible parameter. `ColumnMappings` and `DataRange` stay
exactly where ADR 0004 put them (DataSource/Worksheet level) — nothing
about this fix revisits that decision, since a worksheet's column layout
and range are still one coherent thing regardless of which table binds to
it; only "which worksheet is this binding about" needed to move.

## Consequences
- `TablePropertiesViewModel`'s binding UI (Variant 2.5 "Change Binding"
  workflow) sets both `DataSourceName` and `WorksheetName` when a user
  picks a specific worksheet as a bind target.
- `InMemoryDataProvider` and any future non-worksheet-shaped provider
  simply ignore `worksheetNameOverride` — it's optional and provider-specific
  by design, not a leak of Excel concepts into the generic `IDataProvider`
  contract (the parameter name is worksheet-flavored only because Excel is
  the only real provider so far; a future SQL provider would just ignore it,
  the same way it already ignores `ProviderKey`-specific concerns it
  doesn't need).
