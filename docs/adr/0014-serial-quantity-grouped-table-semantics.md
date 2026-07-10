# ADR 0014: Serial-Quantity Grouped Table Semantics

- Status: Accepted
- Date: 2026-07-09

## Context

`TableContentNode.Rows` is a rectangular matrix of resolved cell text. That is sufficient for ordinary tables, but it cannot express the semantic difference between two unrelated physical rows and one logical product item expanded into multiple serial-number rows. In the required grouped serial table, the serial column has one value per physical row while safe non-serial cells represent one logical value spanning the whole serial group.

Duplicating those non-serial values into every row would lose merge intent. Blanking continuation values without a span contract would force Preview and Word to independently infer grouping from cell text, producing competing heuristics and different output.

Sprint 10 multi-source composition also means grouping cannot safely occur per source. Source-specific mappings must first normalize all source rows into the table's stable column order.

## Decision

### Stable persisted role configuration

`TableElement` may persist an optional `SerialQuantityGrouping`. Its match-key, serial-number, and quantity roles are identified only by stable `TableColumn.Id` values.

Displayed `TableColumn.Header` text is not grouping identity. Renaming a displayed header after configuration must not break the role assignment. Raw indexes and Excel column letters are not persisted for this semantic feature.

### Composition boundary

`ITableContentRowComposer` is the semantic composition boundary. `ReportContentBuilder` first completes existing provider retrieval, sort behavior, source-specific field mapping, and ordered multi-source append. It then passes one fully normalized row stream to the composer.

The composer does not open Excel, resolve providers, or redo source mappings. Partial multi-source rows associated with `SourceError` are not composed.

The bootstrap implementation is `PassthroughTableContentRowComposer`; it preserves rows and emits no spans, groups, or warnings. Sprint 15 grouping heuristics are implemented behind the interface rather than in `ReportContentBuilder`.

### Complete-table semantic spans

`TableContentNode.CellSpans` contains vertical `TableCellSpan` values whose `RowIndex` is relative to the complete semantic `Rows` collection. `TableContentNode.RowGroups` carries complete-table grouping intent for layout. `CompositionWarnings` carries non-blocking semantic warnings.

`Rows` remains the actual semantic row matrix. Sprint 15 does not replace it with a generic cell graph.

### Fragment-local layout spans

`TablePageBlockPayload.CellSpans` uses the same `TableCellSpan` type, but its indexes are relative to that page fragment's `Rows`.

The Engine owns the conversion from complete semantic spans to fragment-local spans. When a semantic span crosses a page boundary, the Engine clips the intersection, restarts a local span where necessary, and copies the original semantic anchor value into the first local continuation row so group identity remains visible.

Preview consumes only fragment-local payload spans. It does not reconstruct complete-table grouping or pagination semantics.

### Word merge semantics

Word consumes complete `TableContentNode.CellSpans`, not layout fragments or `DocumentLayoutResult`. For vertical spans, the Word writer uses true WordprocessingML vertical merge semantics: restart at the semantic anchor cell and continuation merge markers on covered cells. Every physical semantic row keeps a cell for every table column.

This preserves the shared semantic source while allowing Word to remain its own pagination engine.

## Consequences

- Preview and Word share one grouped-table semantic model instead of reverse-engineering blank cells independently.
- Stable `TableColumn.Id` role identity survives displayed-header renames.
- Multi-source grouping occurs only after source normalization and ordered append.
- Complete-table composition spans and fragment-local layout spans have explicit, different index scopes.
- The Engine owns page-boundary clipping/restart behavior; the UI does not calculate it.
- Word consumes complete semantic spans with vertical merge semantics; Preview consumes fragment-local spans.
- Existing direct `TableContentNode` and `TablePageBlockPayload` initializers remain compatible through empty collection defaults.
- Reverse-engineered DOCX-to-KKL structure conversion remains a later concern. Sprint 14 imported DOCX preview remains read-only preview extraction.
