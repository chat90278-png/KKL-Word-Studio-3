# ADR 0010: Variant 2.5 Shell — Right-Docked Context Workspace

## Status
Accepted

## Context
The application had grown five permanent panels (Project Explorer, Excel
Workspace, Report Designer, Table Properties, Preview) fighting for space
in a single row. An approved HTML/CSS prototype ("Variant 2.5") and a
companion implementation-definition document specified a restructured
shell: two hero surfaces (Excel Workspace, Live Word Preview) with a
narrow navigation rail and a resizable right Context Dock (Contents /
Properties, mutually exclusive) absorbing everything else.

This ADR covers the structural UI decisions. It does not introduce any
new Domain concepts — every decision below was evaluated against "does
this need a Domain change" first, per the task's own instruction.

## Decisions

### Dock state lives in a new UI-only `DockViewModel`, not Domain
`DockState` (Normal/Collapsed/Expanded) and `DockPage`
(Contents/Properties/ChangeBinding) are pure presentation state — no
Domain type was added for them. `DockViewModel` is registered as a DI
singleton so `MainWindow`'s column width, `ContextDockView`'s visible
page, and every dock-hosted ViewModel observe the exact same state.

**Real WPF gotcha found and fixed:** `ColumnDefinition` is not a
`FrameworkElement` and does not participate in normal `DataContext`
inheritance, so `Width="{Binding DockViewModel.State, ...}"` would not
actually update at runtime. Fixed by giving the column an `x:Name` and
setting its `Width` imperatively from `MainWindow`'s code-behind in
response to `DockViewModel.PropertyChanged` — a small, product-logic-free
visual workaround, not a ViewModel change.

### Contents is a projection, not a second persisted tree
`ContentsViewModel` walks the real `Report` (Sections → Container.Children)
and builds `ContentsNodeViewModel` rows for Heading/AltHeading/Table only.
`PageHeader`/`PageFooter` sections are skipped (header/footer text is page
furniture, not document structure a reader would see as an outline).
Heading/AltHeading/Table nesting (e.g. "Motor" and "Tablo 3" indented under
"Hava Aracı") is computed with a small stack-based outline-level algorithm
over the existing flat element sequence — no new Domain field records
"nesting"; it's derived every time from each element's existing
Heading/AltHeading/Table classification.

### Binding status is genuinely resolved, not a DataSourceName null-check
Per the task's explicit instruction, `ContentsViewModel.ResolveBindingStatus`
checks: does the named DataSource exist in `Project.DataSources`; for
Excel sources, does the bound worksheet (via the new `Binding.WorksheetName`,
falling back to `ActiveWorksheetName`) resolve to a real `Worksheet` with a
configured `SelectedRange`; and does the workbook's `SourcePath` file still
exist on disk. Only all three passing counts as `Bound`.

### `PropertiesViewModel` replaces the old table-only panel
The old `TablePropertiesViewModel` only ever showed fields for a selected
`TableElement` — selecting a Heading showed nothing. Renamed and extended
to `PropertiesViewModel` with a `PropertiesSelectionKind` (None/Heading/Table)
driving which section of `PropertiesView.xaml` is visible. Only
capabilities the current Domain model actually supports are exposed —
"Start New Page"/"Keep With Next"/"Numbering" from the HTML reference are
intentionally NOT present (nothing in `Section`/`TextElement` supports them
yet); inventing UI-only persisted properties for them was explicitly
disallowed by the task.

### Change Binding is a dock navigation state, not a dialog
`DockPage.ChangeBinding` is a third dock page (not a modal, not a separate
window). `PropertiesViewModel.ChangeBinding()` builds `BindingCandidateViewModel`
rows from every worksheet of every `ExcelDataSource` in the active
Project — real state, never hardcoded. `BindSelectedRange()` sets both
`Binding.DataSourceName` and `Binding.WorksheetName` (see ADR 0009) and
returns to the Properties page.

### Excel grid highlighting uses `IMultiValueConverter`, not stored per-cell flags
Header-row highlight and data-start/end row borders are computed at
render time from `ExcelWorkspaceViewModel.HeaderRowNumber` /
`EffectiveDataStartRow` / `DetectedDataEndRow` via two small
`IMultiValueConverter`s applied to `DataGridRow.Background`/`BorderThickness`.
No new state was added to the Domain `DataRange` model — the visualization
is entirely derived from fields that already existed.

### Fit Width chosen as the default zoom
Both Fit Page and Fit Width are implemented (`PreviewZoomOption`,
`PreviewViewModel.RecomputeZoom`). Fit Width was chosen as the default:
for an A4 portrait page inside a preview column that is itself usually
narrower than it is tall, Fit Page and Fit Width very often converge on
the same scale in practice (width is the binding constraint either way) —
but Fit Width additionally *guarantees* no horizontal scrollbar ever
appears, which matters more for at-a-glance readability than reproducing
a little extra top/bottom breathing room. Viewport size is fed from
`PreviewView`'s code-behind `SizeChanged` handler (WPF has no bindable
"my parent's actual rendered size"), a small, product-logic-free visual
wire-up.

## Consequences
- `ReportDesignerViewModel`/`ReportDesignerView` and the old
  `TablePropertiesView` were deleted — their functionality is fully
  absorbed into `ContentsViewModel`/`ContentsView` and the new
  `PropertiesViewModel`/`PropertiesView`, not dropped.
- `IFileDialogService` gained `OpenProjectFile()`; `MainViewModel` gained
  real Open Project (using the existing `IProjectService.OpenAsync`,
  never a second persistence service) and Save-vs-Save-As (remembers the
  last path so repeated Save doesn't re-prompt) — no fake dirty-state
  marker was added, per the task's explicit instruction; this is listed
  as a remaining UX gap in the final report instead.
