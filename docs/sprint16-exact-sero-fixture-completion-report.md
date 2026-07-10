# Sprint 16 — Exact Sero Fixture Completion Report

## Scope

Narrow input-completion patch applied to the reviewed Sprint 16 integrated candidate.

Baseline:
`KKL.WordStudio-Sprint16-Integrated(1).zip`

The integration report recorded one exact-input blocker: the raw `Sero.docx` bytes
were not mounted in the Integration Lead sandbox, so the exact regression fixture
could not be populated.

In the current project workspace the raw `Sero.docx` is available.

## Exact fixture identity

SHA-256:

`8cfea71041e122fd3c3c5ccb3cc0e1032bfcad233f0e9aade781cbc8472c4d20`

The hash matches the authoritative value already frozen in:

- `SERO-REFERENCE-FORMAT-PROFILE.md`
- `ExactSeroProfile_ExtractsAuthoritativeSupportedProperties`

The raw bytes were copied unchanged to:

`tests/KKL.WordStudio.Infrastructure.Tests/TestData/Sero.Reference.docx`

No synthetic or reconstructed DOCX was used.

## Existing test/csproj path preserved

The integrated source already contains:

- `ExactSeroProfile_ExtractsAuthoritativeSupportedProperties`
- the exact expected SHA-256 assertion
- the real `OpenXmlReferenceDocumentFormatProvider` call
- the conditional test-project copy rule for `TestData\Sero.Reference.docx`
  with `CopyToOutputDirectory="PreserveNewest"`

Therefore no test logic or project file needed modification.

## Independent integrated-source checks

Confirmed before packaging:

- exactly one production `IReferenceFormatDocumentService` interface:
  `Application.Formatting.IReferenceFormatDocumentService`
- `PreviewViewModel` receives the reference import service through constructor DI
- no `new ReferenceFormatDocumentService(...)` remains
- `PreviewTableGridControl` uses explicit `Grid.SetRowSpan`
- `TableContentNode.CaptionFormat` exists
- both ReportContentBuilder table creation paths transport `formatProfile?.TableCaption`
- `TablePageBlockPayload.CaptionFormat` exists
- Word KeepWithNext uses resolved format plus compatibility-only heading fallback
- source test inventory = 400
- skipped tests = 0

## Files added

- `tests/KKL.WordStudio.Infrastructure.Tests/TestData/Sero.Reference.docx`
- `docs/sprint16-exact-sero-fixture-completion-report.md`

No production `src` file changed.
No test source file changed.
No existing test was removed, renamed, skipped, or weakened.

## Verification honesty

This environment does not provide the .NET/WPF Windows runtime gate.

Run on Windows from the solution root:

```text
dotnet restore
dotnet build
dotnet test
```

The exact-Sero regression is now physically present and should execute against the
real provider instead of failing at the fixture-presence assertion.

No green result is claimed until actual Windows command output is available.
