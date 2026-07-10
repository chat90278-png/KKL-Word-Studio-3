# Sprint 14 Contract Baseline Stabilization Report

## Scope

Narrow Windows compile stabilization of `KKL.WordStudio-Sprint14-Contract-Baseline.zip` only. No Sprint 14 pagination, WPF preview, DOCX extraction, production behavior, or frozen shared-contract changes were made.

## Windows ground truth received

The stabilization prompt reports:

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED
- 0 warnings, 8 errors
- all failures are CS0246 for `Fact` / `FactAttribute`
- affected files are only:
  - `tests/KKL.WordStudio.Architecture.Tests/EngineProjectDependencyTests.cs`
  - `tests/KKL.WordStudio.Engine.Tests/FallbackDocumentLayoutEngineTests.cs`

Both test projects already reference `Microsoft.NET.Test.Sdk`, `xunit`, and `xunit.runner.visualstudio`. Source inspection confirmed the two affected files used `[Fact]` and `Assert` without importing the `Xunit` namespace.

## Exact fix

Added exactly one line to each affected test source file while preserving file-scoped namespaces:

```csharp
using Xunit;
```

Changed production source files: **0**.

No package version, global-usings architecture, `Directory.Build.props`, frozen Application contract, `PreviewSnapshot`, Engine fallback, or `PreviewRenderer` orchestration change was made.

## Static preservation checks

PASS.

- Baseline comparison before adding this report showed only the two requested test source files differed.
- Each affected source contains exactly one `using Xunit;` and retains its existing `[Fact]` / `Assert` test code.
- `KKL.WordStudio.Engine.csproj` still references only:
  - `KKL.WordStudio.Application`
  - `KKL.WordStudio.Shared`
- Engine still has no project reference to UI, Infrastructure, or Rendering.
- Engine still has no OpenXML package reference.

## Local command verification

The exact requested commands were invoked from the solution root in this environment:

### `dotnet restore`

Result: **NOT RUNNABLE / exit code 127**

```text
bash: line 1: dotnet: command not found
```

### `dotnet build`

Result: **NOT RUNNABLE / exit code 127**

```text
bash: line 1: dotnet: command not found
```

### `dotnet test`

Result: **NOT RUNNABLE / exit code 127**

```text
bash: line 1: dotnet: command not found
```

No local build or test success is claimed.

## Remaining blocker

Windows/.NET 8 verification remains required. Re-run the exact commands below on the Windows verification environment:

```text
dotnet restore
dotnet build
dotnet test
```

The delivered source fix is limited to the proven Contract Bootstrap xUnit namespace regression reported by Windows.
