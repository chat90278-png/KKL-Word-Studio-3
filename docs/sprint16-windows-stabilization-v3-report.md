# Sprint 16 — Windows Stabilization v3 Report

## Windows ground truth

Exact `KKL.WordStudio-Sprint16-Integrated-v2.zip`:

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED
- warnings: 0
- errors: 6

Already executable before the blocked projects:

- Domain.Tests: 18 passed / 0 failed
- Application.Tests: 168 passed / 0 failed
- Engine.Tests: 55 passed / 0 failed

Infrastructure and Architecture could not complete because of compile blockers.

`NETSDK1057` is the preview-SDK notice and is not the compile failure.

## 1. WordTableWriter OpenXML typed margin fix

Windows reported four CS0029 errors in:

`src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordTableWriter.cs`

The two affected Open XML SDK types are:

- `TableCellLeftMargin`
- `TableCellRightMargin`

For the referenced `DocumentFormat.OpenXml` 3.1.0 API surface, their properties
compile as:

- `Width`: `Int16Value`
- `Type`: `EnumValue<TableWidthValues>`

The integrated code incorrectly supplied:

```csharp
Width = ToTwips(...).ToString()
Type = TableWidthUnitValues.Dxa
```

The patch uses the correct strongly typed values:

```csharp
Width = (short)Math.Min(ToTwips(...), (uint)short.MaxValue)
Type = TableWidthValues.Dxa
```

Only left/right margins are changed.

Existing `TopMargin` / `BottomMargin` use their own existing API surface and remain:

```csharp
Width = "...twips..."
Type = TableWidthUnitValues.Dxa
```

No table-format semantic was changed.

The expected Sero left/right margin remains 70 twips.

## 2. ReferenceFormatDocument required-member test fix

Windows reported two CS9035 errors in:

`tests/KKL.WordStudio.Architecture.Tests/Sprint16ReferenceFidelityArchitectureGuardTests.cs`

The architecture guard instantiated:

```csharp
new ReferenceFormatDocument()
```

twice, but `ReferenceFormatDocument.FileName` is a required member.

The test now creates one valid minimal instance:

```csharp
var referenceDocument = new ReferenceFormatDocument
{
    FileName = "reference.docx"
};
```

and performs the same embedded-entry/front-matter-separation assertions.

No production Domain contract was weakened.

## 3. Margin test type alignment

`Sprint16WordReferenceFidelityTests` expected the same numeric 70-twip values as strings.

Because `TableCellLeftMargin.Width` / `TableCellRightMargin.Width` are strongly typed
numeric values in the referenced SDK surface, the assertions now compare:

```csharp
(short)70
```

The behavioral expectation is unchanged.

## Files changed

Modified:

- `src/KKL.WordStudio.Infrastructure/Export/Exporters/Word/WordTableWriter.cs`
- `tests/KKL.WordStudio.Architecture.Tests/Sprint16ReferenceFidelityArchitectureGuardTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint16WordReferenceFidelityTests.cs`

Added:

- `docs/sprint16-windows-stabilization-v3-report.md`

No other production source changed.

## Preservation checks

Confirmed in the packaged tree:

- exact Sero fixture SHA-256:
  `8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`
- `TableContentNode.CaptionFormat` preserved
- both ReportContentBuilder caption-format transport paths preserved
- single injected Application reference-format service boundary preserved
- no `new ReferenceFormatDocumentService(...)` in PreviewViewModel
- explicit `Grid.SetRowSpan(...)` preserved
- source test inventory: 400
- skipped tests: 0

## Verification honesty

This patch was produced from the exact Windows diagnostics and source tree.

The current execution environment cannot run the Windows/.NET WPF gate.

Run:

```text
dotnet restore
dotnet build
dotnet test
```

No green result is claimed until actual Windows output is available.
