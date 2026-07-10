# Sprint 16 — Windows Stabilization v4 Report

## Windows ground truth

Exact `KKL.WordStudio-Sprint16-Integrated-v3.zip`:

- `dotnet restore`: SUCCESS
- `dotnet build`: FAILED
- warnings: 7
- errors: 3

Projects already green in `dotnet test` before blocked projects:

- Domain.Tests: 18 passed / 0 failed
- Application.Tests: 168 passed / 0 failed
- Engine.Tests: 55 passed / 0 failed

The remaining compile blockers were:

1. CS1061 `ServiceCollection.BuildServiceProvider` unavailable in Infrastructure.Tests
2. CS0136 duplicate local name `body` in a Word round-trip test
3. MC3072 `LineHeight` is not a direct `TextBox` property in Preview XAML

The seven warnings were three CS8604 nullable parse warnings, one xUnit2009,
and three xUnit2031 warnings.

`NETSDK1057` remains only the preview-SDK notice.

## 1. Reference-format DI test compile fix

`KKL.WordStudio.Infrastructure.Tests` does not reference the full
`Microsoft.Extensions.DependencyInjection` implementation package that supplies
`BuildServiceProvider()`.

The test already proves the registered descriptor targets
`OpenXmlReferenceFormatDocumentService`.

It now instantiates that exact registered implementation type through:

```csharp
Assert.IsAssignableFrom<IReferenceFormatDocumentService>(
    Activator.CreateInstance(descriptor.ImplementationType!))
```

and executes the corrupt `.docx` validation against the real OpenXML service.

No package was added.
Production DI was not changed.
The single Application import-boundary registration assertion remains.

## 2. Word round-trip test local-scope fix

Inside `SeroWord_GeneratedDocx_ReopensWithExpectedReferenceProperties`, the creation
block local `body` collided with the reopened-document `body` local later in the
same method.

Only the creation-scope local was renamed to `documentBody`.

No Word behavior or assertion changed.

## 3. Preview TextBox line-height XAML fix

The display presenter is a `PreviewTextBlockControl` and legitimately uses inherited
`TextBlock.LineHeight`.

The temporary caption editor is a `TextBox`; `LineHeight` is not a direct property
on that control.

The editor now uses WPF attached properties:

```xml
TextBlock.LineHeight="{Binding CaptionLineHeight}"
TextBlock.LineStackingStrategy="BlockLineHeight"
```

The resolved caption line-height binding is preserved.

All XAML and project files parse as XML after the patch.

## 4. Warning cleanup

All seven supplied warning sites were cleaned without suppressing analyzers:

- three CS8604 parse inputs are asserted as real strings before parsing;
- xUnit2009 uses `Assert.StartsWith`;
- three xUnit2031 sites use `Assert.Single(collection, predicate)`.

No test expectation was weakened.

## Files changed

Modified:

- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint16ReferenceFormatTests.cs`
- `tests/KKL.WordStudio.Infrastructure.Tests/Sprint16WordReferenceFidelityTests.cs`
- `src/KKL.WordStudio.UI/Views/PreviewView.xaml`

Added:

- `docs/sprint16-windows-stabilization-v4-report.md`

No other production source changed.

## Preservation checks

Confirmed:

- exact Sero fixture SHA-256:
  `8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`
- source test inventory: 400
- skipped tests: 0
- `CaptionFormat` semantic transport preserved
- both ReportContentBuilder caption-format assignments preserved
- single injected Application reference-format import boundary preserved
- no `new ReferenceFormatDocumentService(...)`
- explicit `Grid.SetRowSpan(...)` preserved
- Sprint 16 typed left/right OpenXML margin fix preserved

## Verification honesty

This package was source-patched against the exact supplied Windows diagnostics.

Final Windows gate remains:

```text
dotnet restore
dotnet build
dotnet test
```

No green result is claimed until the actual Windows commands complete.
