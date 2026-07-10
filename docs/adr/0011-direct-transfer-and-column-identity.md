# ADR 0011: Direct Excel-to-Report Transfer and Display/Source Column Identity

## Status
Accepted

## Context
Sprint 7 required a single primary action in the Excel Workspace
("Word'e Aktar") that transfers the currently configured worksheet/range
straight into the active report, with column mapping demoted to an
optional, advanced step. Before this sprint, transferring data into a
report required: opening the Table Binding UI, manually creating/selecting
a Binding, and (because `ExcelDataProvider` only ever read
`ColumnMappings`) always defining a full column mapping first — mapping
was a hard prerequisite, not an option.

A second, related problem surfaced while designing the interactive Preview
surface (Pillar B): a bound `TableElement` had no columns of its own — its
displayed headers came directly from `ExcelDataSource.Fields` (which is
itself derived from `ColumnMappings`). That meant there was no way to let
a user rename a table's *displayed* header without also renaming the
*data source field* — the two concepts were the same string.

## Decision

**1. Column mapping becomes optional, not removed.**
`ExcelDataProvider` now additionally exposes the raw column *letter*
("A", "B", ...) as a row key for every cell inside the configured range,
alongside whatever `ColumnMappings` already contribute. This is strictly
additive — `Dictionary.TryAdd` means a mapped field name always wins on
collision, so every existing mapped binding resolves exactly as it did
before this change (verified by the pre-existing
`TableWorksheetBindingStabilityTests`, which still pass unmodified).

**2. `TableColumn` gains a nullable `SourceField`.**
`Header` remains the *displayed* text (Preview, Word export).
`SourceField` is the *stable source identity* the column's data resolves
from — either a mapped field name or a raw column letter. `ReportContentBuilder`
checks whether a bound table's columns carry any non-null `SourceField`:
if so, headers/keys are resolved per-column from `TableColumn`; if not
(every table bound before Sprint 7 existed), the original
`DataSource.Fields`-only path runs unchanged. This is the mechanism behind
"renaming a displayed header never breaks data resolution" — the two are
now genuinely separate fields instead of one string playing both roles.

**3. `ExcelReportTransferService` is a coordination layer, not a second
binding mechanism.** It always ends in the pre-existing `Binding`
(`DataSourceName` + pinned `WorksheetName`, ADR 0009) on a `TableElement`,
resolved by the same `ReportContentBuilder`/`IDataProvider` pipeline
Preview and Word export already share. What it adds is: resolving/creating
the `ExcelDataSource` and `Worksheet`/`DataRange` for the active workbook,
building the transferred column structure (mapped field name → Excel
header text → `"Sütun {letter}"` fallback, in that priority), and routing
by the shared Workspace selection (selected table → transfer into it;
selected heading → new table under it; neither → default Body insertion).

**4. Transferring into an already-configured table never silently
overwrites it.** The service returns `RequiresExistingTableDecision`
instead of mutating anything when the target table is already bound or
has user-customized (non-placeholder) columns; the caller then re-invokes
with an explicit `ExistingTableTransferMode` (`RebindKeepColumns` —
preserve displayed headers, re-point source identity only — or
`ReplaceColumnsFromSource` — take the source range's columns as-is).

**5. `IReportEditingService` is the one commit path for inline design-
surface edits.** Both inline heading/text edits and table header renames
go through this Application-layer service so they're testable without WPF
and so Preview/Contents/Word export all observe the same mutated Domain
state via the existing `ReportContentChanged` event — the Preview never
edits its own block ViewModels as the source of truth.

## Rejected alternatives
- **A second, transfer-specific binding/resolution path** — rejected; the
  whole point of the direct transfer is that it *reuses* the existing
  Binding/WorksheetName/ColumnMapping/ReportContentBuilder pipeline, so
  Preview and Word export never need to know a table was populated via
  the direct transfer versus manual Table Binding UI.
- **Redesigning `TableElement`/`TableColumn` around a richer column
  model** (e.g., an Engine/Strategy abstraction for column resolution) —
  rejected as unearned complexity; a single nullable `SourceField` string
  is the minimal change that separates display from identity.
- **Silently overwriting a configured table's columns/binding on
  transfer** — rejected; the small decision flyout is a two-button choice,
  not a full dialog, and is only shown when there is genuinely something
  to lose.
- **A second selection/interaction state for the Preview surface** —
  rejected; `Workspace.SelectedReportElementId` is the one shared
  selection state Contents, Preview and Properties all read/write, guarded
  against redundant re-raise so the panels can't ping-pong.
