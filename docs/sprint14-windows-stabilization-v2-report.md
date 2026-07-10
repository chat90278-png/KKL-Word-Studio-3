# Sprint 14 — Windows Stabilization v2 Report

## Scope

Direct narrow patch of `KKL.WordStudio-Sprint14-Stabilized.zip` after the next real
Windows `dotnet build` exposed three remaining test compile blockers.

No production feature, pagination, Preview, imported-DOCX extraction, Word writer,
frozen contract, DI registration, or Team D architecture behavior was redesigned.

## Windows ground truth

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED
- 0 warnings
- 3 errors

All three errors are in:

`tests/KKL.WordStudio.Infrastructure.Tests/Sprint14WordFidelityTests.cs`

Errors:
- CS0104 `TableRow` ambiguity at the two OpenXML table-row queries.
- CS0103 `WordprocessingDocumentType` unresolved at the DOCX fixture creation.

## Exact source fixes

Added:

```csharp
using DocumentFormat.OpenXml;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
```

Changed only the two OpenXML row queries:

```csharp
table.Elements<OpenXmlTableRow>()
```

The alias is intentionally OpenXML-specific because the test also imports
`KKL.WordStudio.Domain.Elements`, which contains the distinct domain `TableRow` type.

`using DocumentFormat.OpenXml;` resolves the existing
`WordprocessingDocumentType.Document` usage consistently with other OpenXML tests
already present in this repository.

## Previous stabilization preserved

Confirmed in the patched source tree:

- `OpenXmlImportedDocumentPreviewProvider.ProcessParagraphElement` still uses
  `element is Run directRun` and passes `directRun` to `ProcessRun`.
- `DeterministicDocumentLayoutEngineTests` still uses
  `Assert.Single(collection, predicate)` for the PageNumber assertion.

## Files changed

Modified:
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint14WordFidelityTests.cs`

Added:
- `docs/sprint14-windows-stabilization-v2-report.md`

No production `src` file changed in this v2 patch.

## Test inventory

Static source inventory:
- `[Fact]` / `[Theory]` methods: 222
- skipped tests found: 0

No test was deleted, renamed away, skipped, or weakened by this patch.

## Verification honesty

The current execution environment has no `dotnet` CLI, so executable
restore/build/test could not be run here.

This ZIP is source-patched against the exact Windows diagnostics only.

Final Windows/.NET 8 verification remains required:

```text
dotnet restore
dotnet build
dotnet test
```

`NETSDK1057` remains only the preview-SDK notice and is not a compile blocker.
