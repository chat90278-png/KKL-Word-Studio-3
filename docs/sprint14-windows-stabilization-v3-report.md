# Sprint 14 — Windows Stabilization v3 Report

## Scope

Direct narrow patch of `KKL.WordStudio-Sprint14-Stabilized-v2.zip` after real
Windows verification reached a clean build but two imported-DOCX preview tests
failed at runtime.

No pagination, WPF page surface, report structure, frozen contract, DI registration,
Word export composition, or test behavior was redesigned.

## Windows ground truth

- `dotnet restore`: SUCCESS
- `dotnet build`: SUCCESS — 0 warnings, 0 errors
- `dotnet test`:
  - Domain: 16 passed / 0 failed
  - Application: 100 passed / 0 failed
  - Engine: 21 passed / 0 failed
  - Architecture: 27 passed / 0 failed
  - Infrastructure: 61 passed / 2 failed

Failing tests:

- `ImportedPreview_ExtractsParagraphTextAndRunFormatting`
- `ImportedPreview_ResolvesParagraphStyleFormattingAndKeepNext`

Both fail because `result.Document` is null after the provider converts an internal
extraction exception into the generic Turkish unreadable-front-matter state.

The running WPF application shows the same integrated status:

`Ön belge okunamıyor veya artık kullanılamıyor.`

## Root cause

`NarrowStyleResolver.GetAttributeValue(...)` used:

```csharp
element.GetAttribute(localName, WordprocessingNamespace)
```

The Open XML SDK `OpenXmlElement.GetAttribute(string, string)` API throws
`KeyNotFoundException` when the requested attribute is absent.

WordprocessingML on/off elements commonly represent enabled state without an
explicit `w:val`, for example `w:b`, `w:i`, and `w:keepNext`.

The resolver intentionally expects a missing `w:val` to mean an enabled on/off
element in `GetOnOff`, but the throwing attribute lookup prevents that logic from
running. The provider's outer catch then returns `Document = null`.

This explains both failing formatting/style tests and the real front-matter preview
warning observed in the running application.

## Exact fix

Changed only:

`src/KKL.WordStudio.Infrastructure/Word/OpenXmlImportedDocumentPreviewProvider.cs`

`GetAttributeValue` now performs a non-throwing attribute scan:

```csharp
var attribute = element.GetAttributes()
    .FirstOrDefault(candidate =>
        string.Equals(candidate.LocalName, localName, StringComparison.Ordinal)
        && string.Equals(candidate.NamespaceUri, WordprocessingNamespace, StringComparison.Ordinal));

return string.IsNullOrEmpty(attribute.Value) ? null : attribute.Value;
```

The existing `GetOnOff` semantics remain unchanged:

- missing element -> null / unresolved
- present on/off element with no `w:val` -> true
- `0`, `false`, or `off` -> false through the existing value logic
- explicit enabled values -> true

No exception suppression was added around the style resolver.

## Previous stabilization preserved

Confirmed:

- `ProcessParagraphElement` still uses `Run directRun`
- Engine PageNumber test still uses `Assert.Single(collection, predicate)`
- Sprint14WordFidelityTests still has the OpenXML root import
- Sprint14WordFidelityTests still uses the `OpenXmlTableRow` alias
- OpenXML preview source still opens with `WordprocessingDocument.Open(path, false)`

## Files changed

Modified:
- `src/KKL.WordStudio.Infrastructure/Word/OpenXmlImportedDocumentPreviewProvider.cs`

Added:
- `docs/sprint14-windows-stabilization-v3-report.md`

No test file changed in v3.
No additional production file changed.

## Test inventory

Static source inventory:

- `[Fact]` / `[Theory]` methods: 222
- skipped tests: 0

The two existing failing tests remain intact and serve as the direct runtime regression
coverage for this fix.

## Verification honesty

The current sandbox has no `dotnet` CLI, so this v3 patch was not executable-tested
here.

Final Windows verification remains required:

```text
dotnet restore
dotnet build
dotnet test
```

Expected test inventory remains 222 with 0 skipped.

After tests are green, rerun the real DOCX front-matter preview smoke test.
