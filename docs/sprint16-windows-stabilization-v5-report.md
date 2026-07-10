# Sprint 16 — Windows Stabilization v5 Report

## Windows ground truth

Exact `KKL.WordStudio-Sprint16-Integrated-v4.zip`:

- `dotnet restore`: SUCCESS
- `dotnet build`: SUCCESS — 0 warnings / 0 errors
- `dotnet test`:
  - Domain: 18 passed / 0 failed
  - Application: 168 passed / 0 failed
  - Engine: 55 passed / 0 failed
  - Architecture: 53 passed / 1 failed
  - Infrastructure: 110 passed / 2 failed

Three focused failures remained.

## 1. Preferred row height on repeated Word header row

`SeroWord_Table_WritesPreferredRowHeight` found one of the two physical Word rows
without `TableRowHeight`.

`BuildRow` used `OpenXmlCompositeElement.AddChild(..., true)` separately for
`TableRowHeight` and `TableHeader`.

The reference `Sero.docx` row property order is:

```xml
<w:trHeight w:val="578"/>
<w:tblHeader/>
```

The writer now appends both row-property elements explicitly in that order:

```csharp
rowProperties.AppendChild(new TableRowHeight { ... });
rowProperties.AppendChild(new TableHeader());
```

Preferred row height therefore applies to the repeated header and data rows.
The expected height remains 578 twips / AtLeast.

## 2. Invalid DOCX import file-handle release

`ReferenceFormatImportBoundary_UsesSingleApplicationContractAndRealOpenXmlImplementation`
failed while deleting the temporary invalid `.docx`.

When `WordprocessingDocument.Open(filePath, false)` throws during package opening,
the assignment to the `using var document` local never completes. The failing Windows
run demonstrated that the path can remain locked long enough for immediate cleanup.

The real import service now owns the read-only stream explicitly:

```csharp
using var stream = new FileStream(
    filePath,
    FileMode.Open,
    FileAccess.Read,
    FileShare.Read);
using var document = WordprocessingDocument.Open(stream, false);
```

If OpenXML package creation throws, the outer stream is still disposed by the enclosing
`using` scope.

The source DOCX remains read-only and is never rewritten.

## 3. Sprint 15 source guard signature drift

Sprint 16 format resolution made `BuildMultiSourceErrorNode` an instance method because
it consumes the resolved format profile.

The Sprint 15 architecture guard still searched for the historical exact marker:

`private static TableContentNode BuildMultiSourceErrorNode`

The production path is still source-error-only and still does not call the row composer.

The guard marker now matches the real signature:

`private TableContentNode BuildMultiSourceErrorNode`

No source-error composition requirement was weakened.

## Files changed

- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordTableWriter.cs`
- `src/KKL.WordStudio.Infrastructure/ReferenceFormatting/OpenXmlReferenceFormatDocumentService.cs`
- `tests/KKL.WordStudio.Architecture.Tests/Sprint15OrchestrationAndHeuristicGuardTests.cs`

Added:
- `docs/sprint16-windows-stabilization-v5-report.md`

No Preview fidelity redesign is included in this narrow stabilization.

## Preservation

- exact Sero fixture SHA-256: `8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`
- source test methods: 400
- skipped: 0
- CaptionFormat transport preserved
- explicit Grid.SetRowSpan preserved
- single Application reference-format import boundary preserved

Final Windows restore/build/test is required.
