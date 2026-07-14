# Sprint 23 Tranche 03 — Report Structure and Numbering

## Baseline

- Base branch: `main`
- Exact base SHA: `836f68a93976df05dadedaf967a792d598166f80`
- Working branch: `sprint23/03-report-structure-numbering`
- Previous Windows gate: `563/563`

## Implemented scope

### Fixed document root

Every activated report is normalized to contain one protected root heading:

```text
1. System Test Procedure Configuration List
```

- The text remains editable.
- The root cannot be deleted, moved, indented or outdented.
- A legacy report without the root receives it at the beginning without losing existing content.
- Duplicate legacy root identities are preserved as ordinary headings rather than silently deleted.
- `Workspace.SetActiveReport` applies the invariant before Preview/Contents see the report.
- `Workspace.NotifyReportContentChanged` re-applies numbering for edits coming from Preview, Properties or other editors.

### Shared numbering

The existing `ReportHeadingNumberingService` remains the one visible numbering path.

```text
1. Root
1.1 Heading
1.1.1 Alt heading
1.2 Heading
```

The policy invokes it after successful rename, delete, move, indent, outdent, drag/drop and contextual insertion. Preview, Contents and Word continue to consume the same real `TextElement` instances and heading styles.

### Contextual add behavior

- Table selected + `+ Başlık`: inserts an H1 immediately above the table.
- Table selected + `+ Alt Başlık`: inserts an H2 immediately above the table, under the nearest owning H1.
- Heading selected + `+ Başlık`: inserts after that heading block.
- Heading selected + `+ Alt Başlık`: inserts inside the selected heading scope.
- No selection + `+ Başlık`: appends under the fixed root.
- No selection + `+ Alt Başlık`: safely falls back to a normal H1, avoiding an orphan H2.
- `+ Tablo` uses the same contextual document-order insertion policy.

### Move and drag/drop behavior

The existing `ReportStructureService` remains authoritative for flat document-order block operations.

- Up/Down stays within same-level derived sibling scope.
- The fixed root is excluded from movement.
- A first user H1 cannot move above the root.
- Drop zones are explicit: `Before`, `Into`, `After`.
- Contents displays a blue top line, full outline or bottom line with `Önüne taşı / İçine taşı / Sonrasına taşı` feedback.
- Dropping an H2 into the fixed root promotes it to a normal H1 only after the move succeeds.
- Code-behind computes gesture intent only; mutations remain in Application services.

### Contents projection

The persisted report stays the established flat `Section.Root.Children` sequence. A UI-only collection projection displays the fixed root as the single top-level Contents item and places existing heading blocks beneath it. No `ParentId`, persisted `ContentsNode`, or parallel report model was introduced.

## Test delta

Added:

- Application tests: `+11`
- Architecture tests: `+2`

Expected total:

```text
576 / 576
```

## Pending Windows gate

```bat
git fetch origin
git checkout sprint23/03-report-structure-numbering
git reset --hard origin/sprint23/03-report-structure-numbering
git rev-parse HEAD
git status --short

dotnet clean -c Release
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

## Manual smoke

1. Open an old report without a root; root appears first and existing content remains.
2. Rename the root; number stays `1.`.
3. Root delete/move/indent/outdent is rejected non-modally.
4. Select a table and press `+ Başlık`; heading appears immediately above it.
5. Select a table and press `+ Alt Başlık`; alt heading appears between its owner and table.
6. Move H1 blocks with Up/Down; their tables move with them and numbering updates.
7. Move H2 blocks with Up/Down; they stay under the same H1.
8. Drag a node near the top/middle/bottom of another row; the matching drop indicator appears.
9. Drop a heading into another heading; Contents, Preview and numbering remain aligned.
10. Edit heading text from Preview/Properties; numbering is restored on refresh.
