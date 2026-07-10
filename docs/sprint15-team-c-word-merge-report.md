# Sprint 15 — Team C Word Table Merge Fidelity Report

## Role and scope

Team C operated as the Word Table Merge Fidelity team from `KKL.WordStudio-Sprint15-Contract-Baseline.zip` only. The frozen Sprint 15 shared contract was treated as authoritative.

Work stayed inside the assigned ownership boundary:

- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordTableWriter.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint15WordTableMergeTests.cs`
- this report

No Application, Domain, Engine, UI, other Word writer, `WordExporter`, composition/altChunk, or DI file was changed. No contract change request was required.

## Complete semantic CellSpans → Word vMerge

`WordTableWriter` now consumes `TableContentNode.CellSpans` directly. It does not inspect displayed Product No / Serial No / Quantity headers and does not inspect `SerialQuantityGrouping`.

A focused internal `VerticalMergeLookup` validates complete-table vertical spans and projects each unambiguous span to Word merge states:

- anchor coordinate → `VerticalMerge` with `MergedCellValues.Restart`;
- covered continuation coordinates → `VerticalMerge` with `MergedCellValues.Continue`.

The header row is intentionally outside the semantic span lookup. Therefore semantic `RowIndex = 0` still targets the first DATA row even when a repeated Word header row is emitted first.

Cells participating in a vertical merge also receive `TableCellVerticalAlignment` with `TableVerticalAlignmentValues.Center`. Existing paragraph/run text behavior is preserved; horizontal centering is not forced.

Continuation merge cells are emitted as real `TableCell` elements and their text is blank. The writer does not duplicate anchor text into continuation cells.

## Invalid and overlapping span safety

The lookup ignores span metadata when any of these conditions is true:

- `RowSpan < 2`;
- negative row or column index;
- anchor row outside `tableNode.Rows`;
- column outside the calculated table column count;
- span end beyond `tableNode.Rows.Count`.

Span-end validation uses a widened integer calculation so malformed large span metadata cannot overflow the row-bound check.

Valid span candidates are first mapped to all covered cell coordinates. If multiple span candidates occupy the same coordinate, every candidate participating in that ambiguity is ignored. No conflicting restart/continue state is emitted and export does not throw for the malformed metadata.

No user warning is emitted by `WordTableWriter`; warning ownership remains with composition/Engine as required by the shared contract.

## Full physical row/cell preservation

`BuildRow` now emits exactly the calculated `columnCount` cells for every generated table row. Short semantic rows are padded with empty cells instead of producing structurally shorter Word rows.

This preserves the Sprint 15 invariant that every physical semantic data row retains a `TableCell` for every table column, including continuation rows covered by a vertical merge.

## Sprint 14 Word table behavior preserved

The existing Word table behavior remains in `WordTableWriter`:

- repeated column-header row through `TableHeader`;
- preferred table width = 100% (`pct=5000`);
- fixed table layout;
- deterministic equal percentage cell widths;
- explicit `TableGrid` column count;
- existing table borders;
- editable Word table cells and paragraph/run text.

Caption behavior remains outside `WordTableWriter` and was not changed.

## Focused Sprint 15 tests added

Added `tests/KKL.WordStudio.Infrastructure.Tests/Sprint15WordTableMergeTests.cs` with all 12 requested focused tests:

1. `WordTable_VerticalSpanAnchor_WritesRestart`
2. `WordTable_VerticalSpanContinuation_WritesContinue`
3. `WordTable_SerialColumn_RemainsUnmerged`
4. `WordTable_MergedCells_AreVerticallyCentered`
5. `WordTable_HeaderRowOffset_DoesNotShiftSemanticSpanIndexes`
6. `WordTable_EveryDataRowRetainsFullCellCount`
7. `WordTable_MultipleIndependentColumnSpans_AreWritten`
8. `WordTable_InvalidSpan_IsIgnoredWithoutCorruptingTable`
9. `WordTable_OverlappingSpanMetadata_DoesNotEmitConflictingMergeState`
10. `WordTable_GroupedExample_ReopensWithExpectedVMergeXml`
11. `WordTable_ExistingRepeatedHeaderBehavior_IsPreserved`
12. `WordTable_ExistingWidthAndFixedLayoutBehavior_IsPreserved`

The grouped user example is written into a generated DOCX package, reopened with Open XML, and inspected as a real Word table. The test verifies:

- two data rows;
- six cells in each data row;
- restart vMerge in row 0 columns 0, 1, 2, 3, and 5;
- continue vMerge in row 1 columns 0, 1, 2, 3, and 5;
- no vMerge in serial column 4;
- `A222` and `A221` remain in their separate serial cells;
- serialized `w:vMerge` restart/continue values are present.

The invalid-span test also round-trips the generated table through a DOCX package and reopens it before inspection.

Baseline tests were not edited or weakened. Source inventory changed from 231 test methods in the supplied contract baseline to 243 test methods: exactly 12 Team C Sprint 15 tests added.

## Verification

The requested commands were attempted with actual shell execution:

```text
dotnet restore
exit 127
dotnet: command not found


dotnet build
exit 127
dotnet: command not found


dotnet test
exit 127
dotnet: command not found
```

Therefore this report does **not** claim restore, build, or test success. Windows/.NET 8 verification remains pending and is the final runtime/build truth.

Static/source checks completed in the available environment:

- all 12 required focused test names are present;
- modified/new C# files passed delimiter-balance scanning for braces, parentheses, and brackets;
- writer source contains restart/continue vMerge and vertical-center Open XML semantics;
- `WordTableWriter` contains no Product No / Serial No / Quantity header heuristic, no `SerialQuantityGrouping` inspection, and no `DocumentLayoutResult` dependency;
- fresh-baseline directory diff shows production changes only in the assigned `WordTableWriter.cs`, plus the new Sprint 15 Infrastructure test file and this report;
- supplied baseline ZIP passed `unzip -t` integrity verification;
- final delivery ZIP integrity is verified after packaging.

## Contract status

The frozen Sprint 15 shared contract was sufficient for Team C. `docs/CONTRACT_CHANGE_REQUEST-C.md` was not created.
