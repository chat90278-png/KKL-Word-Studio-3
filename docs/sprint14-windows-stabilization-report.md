# Sprint 14 — Windows Stabilization Report

## 1. CS0136 root cause

Windows `dotnet build` ground truth reported three `CS0136` errors in `OpenXmlImportedDocumentPreviewProvider.ProcessParagraphElement(...)`. The method declared the pattern variable `run` in `element is Run run`, then reused the local name `run` in the `SimpleField`, `Hyperlink`, and `InsertedRun` `foreach` declarations. Under C# local scope rules those declarations collide.

## 2. Exact `directRun` rename

Applied the smallest source fix in:

`src/KKL.WordStudio.Infrastructure/Word/OpenXmlImportedDocumentPreviewProvider.cs`

```csharp
if (element is Run directRun)
{
    ProcessRun(mainPart, directRun, paragraphStyleId, pendingRuns, blocks, alignment, keepWithNext, styles, warnings);
    return;
}
```

The three later `foreach (var run ...)` loops are unchanged. Paragraph extraction order, field/hyperlink/inserted-run handling, deleted-run behavior, warnings, and read-only OpenXML semantics were not changed.

## 3. xUnit2031 fix

Applied the analyzer-supported equivalent in:

`tests/KKL.WordStudio.Engine.Tests/DeterministicDocumentLayoutEngineTests.cs`

```csharp
var block = Assert.Single(
    page.Blocks,
    candidate => candidate.Kind == PageBlockKind.PageNumber);
```

The page-number assertion semantics are unchanged. The assertion and analyzer remain enabled.

## 4. Additional concrete Windows blockers found

No additional Windows compiler/XAML/test diagnostic was available beyond the supplied `CS0136` and `xUnit2031` ground truth.

This execution sandbox has no .NET CLI, C# compiler, MSBuild, Mono, or PowerShell installation. Therefore the two fixes could not be recompiled here and no additional concrete build/test blocker was revealed by command execution.

## 5. Files changed

Changed:

- `src/KKL.WordStudio.Infrastructure/Word/OpenXmlImportedDocumentPreviewProvider.cs`
- `tests/KKL.WordStudio.Engine.Tests/DeterministicDocumentLayoutEngineTests.cs`

Added:

- `docs/sprint14-windows-stabilization-report.md`

Pristine integrated-baseline comparison before adding this report showed exactly the two requested source files changed and no added/removed files.

## 6. Test inventory

Source inventory after stabilization:

- `[Fact]` / `[Theory]` methods: **222**
- skipped test attributes: **0**
- Application.Tests: 100
- Architecture.Tests: 22
- Domain.Tests: 16
- Engine.Tests: 21
- Infrastructure.Tests: 63

The stabilization did not remove, rename, skip, or weaken a test. Team D architecture/integration guards and the reviewed Sprint 14 integrated candidate remain otherwise byte-for-byte preserved.

## 7. Actual restore/build/test output

Commands were executed from the solution root in the available sandbox.

```text
$ dotnet restore
bash: line 1: dotnet: command not found
EXIT:127

$ dotnet build
bash: line 1: dotnet: command not found
EXIT:127

$ dotnet test
bash: line 1: dotnet: command not found
EXIT:127
```

These outputs do **not** supersede the supplied Windows ground truth that the earlier Windows `dotnet restore` succeeded and the real Windows `dotnet build` exposed the three `CS0136` errors plus `xUnit2031`. They only record that this sandbox cannot execute the .NET verification gate.

No green build/test result is claimed.

## 8. Remaining blocker

Final Windows/.NET 8 WPF verification remains required:

```powershell
dotnet restore
dotnet build
dotnet test
```

The remaining blocker is verification-environment availability, not an additional diagnosed Sprint 14 source regression in this stabilization pass.
