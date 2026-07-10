# Sprint 15 — Windows Stabilization Report

## Windows ground truth

Exact integrated candidate:

- `dotnet restore`: SUCCESS
- `dotnet build`: SUCCESS — 1 warning / 0 errors
- `dotnet test`:
  - Domain: 18 passed / 0 failed
  - Application: 141 passed / 2 failed
  - Architecture: 42 passed / 0 failed
  - Engine: 34 passed / 2 failed
  - Infrastructure: 76 passed / 0 failed

The WPF application launches.

## 1. Application continuation-row null bug

Failing tests:

- `Composer_Quantity2AndTwoSerials_ExpandsRowsAndSpansNonSerialCells`
- `Composer_MissingCellsArePaddedAndExtraCellsAreIgnoredWithoutShifting`

Root cause:

`SerialQuantityTableContentRowComposer` created exact-group output rows with:

```csharp
var output = new string[table.Columns.Count];
```

C# initializes reference-array cells to `null`. The semantic contract and tests require
blank continuation/non-serial cells to be `string.Empty`.

Exact product fix:

```csharp
var output = new string[table.Columns.Count];
Array.Fill(output, string.Empty);
```

The canonical first row is still copied afterward and the serial cell is still assigned
one observed serial token.

This is a production semantic fix, not a test relaxation.

## 2. Engine span test page-height calibration

Failing tests:

- `FragmentSpans_AreLocalToPayloadRows`
- `SpanCrossingPageBoundary_RestartsAtContinuationFragment`

Both fixtures used `CreatePageLayout(height: 35.5)` and then assumed a page fragment
would contain at least two table rows, so `CellSpans.Single()` must exist.

Real Windows execution showed the fixture can produce a one-row fragment at that tight
height. Under the frozen Sprint 15 contract, a one-row span intersection MUST emit no
`TableCellSpan`, because emitted `RowSpan` must be at least 2.

Therefore the production Engine behavior is consistent with the contract; the test
fixture calibration was too close to the deterministic measurement boundary.

Only these two tests now use `height: 38.0`, providing stable capacity for two rows
while remaining below the capacity required for all four data rows.

No Engine production code changed.

## 3. xUnit2013 warning

The only build warning was in:

`Sprint15OrchestrationAndHeuristicGuardTests.cs`

The exact-one composer-call guard used `Assert.Equal(1, collection.Count)`.

It now uses `Assert.Single(...)` over the regex match collection.

The architecture requirement remains unchanged: exactly one successful composer call
boundary.

## Files changed

Modified:

- `src/KKL.WordStudio.Application/TableComposition/SerialQuantityTableContentRowComposer.cs`
- `tests/KKL.WordStudio.Engine.Tests/Sprint15GroupedTablePaginationTests.cs`
- `tests/KKL.WordStudio.Architecture.Tests/Sprint15OrchestrationAndHeuristicGuardTests.cs`

Added:

- `docs/sprint15-windows-stabilization-report.md`

No Engine production file, Preview file, Word writer, frozen contract, DI registration,
or shell file changed in this stabilization.

## Static inventory

- `[Fact]` / `[Theory]` methods: 308
- skipped tests: 0

Final Windows verification remains required:

```text
dotnet restore
dotnet build
dotnet test
```

No green result is claimed for this patched ZIP until those commands run on Windows.

`NETSDK1057` remains only the preview-SDK notice.
