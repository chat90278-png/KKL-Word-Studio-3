# Sprint 15 — Team A Table Composition Report

## Role

Team A / Table Composition Lead / Data Semantics Engineer.

Baseline: `KKL.WordStudio-Sprint15-Contract-Baseline.zip`.

`SPRINT15-SHARED-CONTRACT.txt` was treated as authoritative and frozen. Team A changes are limited to the assigned Application composition, transfer, DI, Sprint 15 Application test, and report ownership paths. No Domain, Application Tables contract, ReportContentBuilder, Engine, UI, Infrastructure, or Architecture test file was changed.

No `CONTRACT_CHANGE_REQUEST-A.md` was required.

## Result

Production Application DI now resolves:

`ITableContentRowComposer -> SerialQuantityTableContentRowComposer`

The bootstrap `PassthroughTableContentRowComposer` remains unchanged and unregistered as the compatibility implementation required by the contract.

Added focused table-composition components under `KKL.WordStudio.Application.TableComposition`:

- `ISerialQuantityGroupingDetector`
- `SerialQuantityGroupingDetector`
- `SerialQuantityTableContentRowComposer`
- internal `ColumnRoleAliasNormalizer`

No Excel/provider access, WPF, OpenXML, Word, pagination, COM, WebView, or PDF dependency was introduced into the composer/detector implementation.

## Stable role detection

`SerialQuantityGroupingDetector` examines both `TableColumn.Header` and `TableColumn.SourceField` and resolves the frozen match-key, serial, and quantity alias sets.

Normalization is deterministic and role matching is exact after normalization:

- case-insensitive;
- spaces ignored;
- punctuation ignored for the configured word aliases;
- Turkish diacritic equivalents normalized;
- dotless `ı` normalized to `i`;
- `#` is retained explicitly for the frozen ordinal alias.

The detector does not use arbitrary substring/fuzzy matching and does not guess by column position.

A configuration is returned only when exactly one candidate exists for every required role and the three resolved `TableColumn.Id` values are distinct. The emitted `SerialQuantityGrouping` stores only stable column IDs and sets `WasAutoDetected=true`.

Ambiguous serial or quantity roles produce no configuration.

## Excel transfer configuration

`ExcelReportTransferService` was updated narrowly through the injected detector while preserving its parameterless/direct-construction compatibility via the default detector fallback.

Behavior now follows the frozen transfer rules:

- **New table:** detection runs after source columns are created and persists the stable role IDs.
- **Replace Columns From Source:** detection runs after the replacement columns are installed and replaces the previous grouping with the detected result or `null`.
- **Rebind Keep Columns:** a grouping is preserved only when all three distinct configured IDs still exist in the current table columns; otherwise detection runs against the current columns.
- **Add As Source:** any existing non-null grouping is left untouched; when grouping is `null`, detection may run against the unchanged table columns.

`TableSourceBinding.FieldMappings` and source-specific normalization behavior were not changed.

Display-header renames after configuration do not affect composition because the composer resolves the configured stable IDs against current `TableColumn.Id` values.

## Serial–Quantity composition

`SerialQuantityTableContentRowComposer` consumes only the `TableElement` and the already-normalized row matrix supplied by the frozen `ITableContentRowComposer` contract.

When `SerialQuantityGrouping` is `null`, the input row collection is returned unchanged with no spans, row groups, or warnings.

If any configured stable column ID is missing, the rows are passed through unchanged and the composer emits:

`Seri no/adet düzeni yapılandırması geçersiz; tablo satırları değiştirilmedi.`

For a valid configuration, every input row is normalized to `table.Columns.Count` cells. Missing cells become empty strings; extra cells are ignored at their original trailing positions and never shift report columns.

## Group order and empty keys

Non-empty match keys are compared as trimmed values with `OrdinalIgnoreCase` semantics.

The composer first indexes rows by key, then walks the normalized stream in original source order:

- the first occurrence of a key emits that complete composed key group;
- later rows for the same key are consumed and are not emitted again;
- an empty-key row is emitted independently at its own logical sequence position;
- the next first-occurrence key then emits its group.

No PN/key sorting is performed by the composer.

Empty match keys are never grouped together.

## Quantity resolution

Blank quantity cells are ignored while resolving the intended group quantity.

Positive whole-number quantities are accepted from current culture first and invariant culture second, without allowing thousands separators. This accepts ordinary integers and mathematically whole decimal representations such as `2.0`; `2,0` is accepted when the current culture uses decimal comma.

The numeric grouping value is resolved as an integer, but the first original non-blank quantity text is retained as the canonical displayed quantity. Therefore a safe `2.0` group remains displayed as `2.0`; it is not rewritten as `2.000000` or `2`.

Rejected quantity examples include:

- `0`
- `-1`
- `2.5`
- malformed non-blank text such as `abc`

One distinct valid quantity is required for aggregation/grouping. Conflicting valid quantities, malformed non-blank quantity text, or no valid quantity make the key group unsafe. Original normalized rows are preserved and a Turkish data-quality warning is emitted.

## Serial token semantics

Serial cells are split on CRLF/LF only.

Tokens are trimmed, empty lines are ignored, and observed token order remains:

1. source row order;
2. line order inside the serial cell.

Comma and semicolon are not serial separators.

Observed tokens retain duplicate occurrences. Duplicate detection uses `OrdinalIgnoreCase` over trimmed tokens.

## Product-data consistency

Before any safe key group is collapsed, non-serial product data is checked.

The match-key column is naturally stable by group identity. Quantity uses the quantity-resolution rule. Frozen ordinal aliases (`#`, `No`, `No.`, `Sıra No`, `Sira No`, `Row`) may use their first non-empty value.

Every other non-serial column collects distinct trimmed non-empty values with `OrdinalIgnoreCase` semantics. More than one value makes the group unsafe.

Unsafe groups preserve their original normalized rows and emit a warning containing the key and the actual displayed `TableColumn.Header`. The composer does not choose a random first Product Name, NSN, or other conflicting product field.

Quantity and product-data conflicts are evaluated independently before returning an unsafe group. When both kinds of conflict are present, both data-quality warnings are retained rather than hiding one conflict behind the other.

## Exact grouped output

For a safe group with a valid quantity, exact grouped serial layout is emitted only when:

- quantity is greater than one;
- observed non-empty serial token count equals quantity;
- every observed serial token is distinct ignoring case.

The result contains one physical semantic row per serial token.

The first output row contains canonical non-serial values and the first serial token. Every continuation row contains blank non-serial cells and exactly one serial token.

A `TableCellSpan` is emitted for **every non-serial column**, including an empty canonical cell:

- `RowIndex = first expanded output row index`
- `ColumnIndex = the non-serial column index`
- `RowSpan = quantity`

The serial column never receives a span.

One `TableRowGroup` is emitted with:

- `StartRowIndex = first expanded output row index`
- `RowCount = quantity`
- `KeepTogetherWhenPossible = true`

Expanded row indexes are calculated from the actual accumulated output row collection, so spans/groups remain correct after preceding independent, aggregated, or already-expanded output.

## Aggregated and unsafe edge behavior

Safe group + zero or one observed serial token produces exactly one normal aggregated row, no spans/groups, and no serial-count mismatch warning. Quantity `100` therefore does not create 100 blank rows when there are zero or one serials.

Safe group + more than one serial token where the count does not equal quantity produces one normal row. The serial cell joins all observed tokens with `\n` in observed order and a concise count-mismatch warning is emitted.

Duplicate serial values never create grouped layout even when observed token count equals quantity. Duplicate occurrences remain in the aggregated serial cell and a duplicate warning is emitted.

Conflicting quantity or product data preserves the original normalized rows for that key, with no spans and no row group.

## Multi-source behavior

No source access exists inside the composer.

Focused tests present conceptual Source A and Source B rows as one normalized stream and prove that the same PN can be completed by serials from different normalized source contributions. This relies on the frozen `ReportContentBuilder` orchestration that invokes the composer only after complete multi-source normalization and append order are established.

## Focused tests

Added `tests/KKL.WordStudio.Application.Tests/Sprint15SerialQuantityCompositionTests.cs`.

The file contains 34 `[Fact]` methods and one `[Theory]` method with three inline quantity cases. All 21 test names requested by the Team A prompt are present, including:

1. `Detector_DetectsEnglishProductSerialQuantityAliases`
2. `Detector_DetectsTurkishAliases`
3. `Detector_AmbiguousRole_DoesNotGuess`
4. `Transfer_NewTable_PersistsStableGroupingColumnIds`
5. `Transfer_HeaderRenameAfterDetection_DoesNotChangeGroupingIds`
6. `Transfer_AddSource_PreservesExistingGrouping`
7. `Composer_NoConfiguration_PassesRowsThrough`
8. `Composer_Quantity2AndTwoSerials_ExpandsRowsAndSpansNonSerialCells`
9. `Composer_Quantity3AndThreeSerials_CreatesThreeRowGroup`
10. `Composer_Quantity100AndNoSerial_ProducesOneRow`
11. `Composer_Quantity100AndOneSerial_ProducesOneRow`
12. `Composer_Quantity3AndTwoSerials_AggregatesSerialCellAndWarns`
13. `Composer_DuplicateSerials_DoNotCreateGroupedLayout`
14. `Composer_ConflictingQuantity_PreservesOriginalRowsAndWarns`
15. `Composer_ConflictingProductData_PreservesOriginalRowsAndWarns`
16. `Composer_EmptyKeys_AreNeverGroupedTogether`
17. `Composer_FirstOccurrenceOrder_IsPreserved`
18. `Composer_MultiSourceNormalizedRows_CanCompleteOneSerialGroup`
19. `Composer_NewlineSeparatedSerialCell_CanFormExactGroup`
20. `Composer_MalformedQuantity_PreservesRowsAndWarns`
21. `Composer_OutputSpans_UseExpandedRowIndexes`

Additional focused gates cover:

- alias detection through renamed-header `SourceField` identities;
- ambiguous quantity aliases;
- rebind preservation and re-detection;
- Add As Source detection when grouping is absent;
- Replace Columns clearing an ambiguous configuration;
- whole-decimal quantity canonical text;
- decimal-comma current-culture parsing;
- zero, negative, and fractional quantity rejection;
- comma/semicolon non-splitting for serials;
- simultaneous quantity and product-data conflict warnings;
- frozen ordinal canonicalization;
- missing/extra row cell normalization;
- invalid configured stable-ID pass-through behavior.

The source-level test attribute inventory is 266 `[Fact]`/`[Theory]` attributes in the delivered workspace. This is a source inventory only, not a claim that runtime tests passed.

## Verification

The requested commands were attempted in the available environment:

```text
$ dotnet restore
bash: line 5: dotnet: command not found
EXIT:127

$ dotnet build
bash: line 9: dotnet: command not found
EXIT:127

$ dotnet test
bash: line 13: dotnet: command not found
EXIT:127
```

Therefore no restore/build/test success is claimed. Windows/.NET 8 verification remains pending.

Additional static/source checks completed in this environment:

- tree-sitter C# syntax parsing for all seven changed/added Team A C# files: `0` syntax error nodes;
- all 21 required focused test method names are present;
- production DI contains exactly one `ITableContentRowComposer` registration and it targets `SerialQuantityTableContentRowComposer`;
- `ISerialQuantityGroupingDetector -> SerialQuantityGroupingDetector` is registered;
- detector uses exact normalized alias hash-set membership and contains no arbitrary `serial`/`quantity` substring matching heuristic;
- serial splitting source check confirms LF/CRLF normalization and no comma/semicolon split path;
- quantity parser does not enable thousands separators;
- composer/detector contain no `IDataProvider`, `System.Windows`, OpenXML, Office Interop, WebView, or PDF references;
- frozen Domain grouping, Application Tables, `ReportContentBuilder`, `ReportContentNode`, and layout contract files are byte-identical to the baseline;
- baseline ownership diff reports `0` ownership violations.

## Exact Team A change record

Modified:

- `src/KKL.WordStudio.Application/Transfer/ExcelReportTransferService.cs`
- `src/KKL.WordStudio.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`

Added:

- `src/KKL.WordStudio.Application/TableComposition/ColumnRoleAliasNormalizer.cs`
- `src/KKL.WordStudio.Application/TableComposition/ISerialQuantityGroupingDetector.cs`
- `src/KKL.WordStudio.Application/TableComposition/SerialQuantityGroupingDetector.cs`
- `src/KKL.WordStudio.Application/TableComposition/SerialQuantityTableContentRowComposer.cs`
- `tests/KKL.WordStudio.Application.Tests/Sprint15SerialQuantityCompositionTests.cs`
- `docs/sprint15-team-a-composition-report.md`

## Contract change request

None created. The frozen Sprint 15 contract was sufficient for Team A implementation.
