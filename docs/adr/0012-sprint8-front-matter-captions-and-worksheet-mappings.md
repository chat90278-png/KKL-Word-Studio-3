# ADR 0012: Sprint 8 Front Matter, Table Captions, and Worksheet-Owned Mappings

## Status
Accepted

## Context
Sprint 8 introduced three semantics that must survive Preview, Word export,
and `.kws` persistence without creating UI-only relationships:

1. a table needs a real visible caption/title;
2. a user may import an existing DOCX cover/preface before generated report
   content, but KKL Word Studio is not an arbitrary DOCX editor;
3. column mappings in a multi-sheet Excel workbook must be independent per
   configured worksheet/dataset.

The Sprint 7 model had `TableElement.Description`, but that property is author
notes shown in Properties and has a different meaning from visible document
content. It also kept `ColumnMappings` at `DataSource` level, so Sheet1 `B ->
PartName` and Sheet2 `B -> EngineType` could not coexist correctly.

DOCX composition cannot safely be implemented by cloning `Body` children into
another package: styles, numbering, media, hyperlinks, section properties and
relationship IDs belong to package parts and relationships, not only to body
XML.

## Decision

### 1. Table captions are explicit Domain state
`TableElement.Caption` is the persisted semantic display caption. `Description`
remains author notes. `ReportContentBuilder` copies `Caption` into
`TableContentNode`; Preview and Word output consume that same content node.

`IReportEditingService.UseHeadingTextAsTableCaption` copies the current text of
a Heading/AltHeading into `TableElement.Caption`. This is a value copy, not a
live cross-element reference. The heading is neither deleted nor moved.

### 2. Front matter is project-owned imported asset state
`Project.FrontMatter` is an optional `FrontMatterDocument`. It deliberately is
not a `ReportElement`: imported arbitrary Word content is not transformed into
or edited by the structured KKL report model.

On `.kws` save, the readable DOCX is copied into the package as the separate
ZIP entry:

`resources/frontmatter/front-matter.docx`

`project.json` stores only metadata/state. The binary is not base64-encoded into
JSON. On open, the embedded entry is materialized to a private temporary path
and attached as runtime-only `ResolvedFilePath`. If neither the embedded entry
nor the remembered source path exists, the project still opens and keeps its
front-matter state so the UI can show `Ön belge bulunamadı`.

### 3. Word composition uses a narrow Infrastructure composer and altChunk
`WordFrontMatterComposer` adds the whole source DOCX as an
`AlternativeFormatImportPart` of type `WordprocessingML`, places an `AltChunk`
anchor before generated KKL content, then adds an explicit page break before
the generated report flow.

This is not a raw `Body` append. The imported document remains a separate
package part; Sprint 8 does not clone and remap style/numbering/media/
relationship graphs into the host package. The composer is intentionally
Infrastructure-only and specific to front matter rather than a generic document
engine.

Preview uses a composition placeholder with file name and availability status.
It does not claim pixel-faithful Word rendering.

### 4. Worksheet mappings belong to Worksheet
`Worksheet.ColumnMappings` is now the primary mapping collection. The transfer
workflow writes applied mappings to the configured worksheet; `ExcelDataProvider`,
`ReportContentBuilder`, and legacy-table column materialization read worksheet
mappings first.

`DataSource.ColumnMappings` remains as a compatibility fallback when the
worksheet-specific collection is empty. This preserves pre-Sprint-8 project
semantics without blocking correct mappings for newly configured multi-sheet
workbooks.

## Consequences
- Table caption persistence is automatic through existing `TableElement` JSON
  polymorphism.
- Preview and Word cannot diverge on caption text because both consume
  `TableContentNode.Caption`.
- `.kws` projects with imported front matter are portable without the original
  DOCX path as long as the project has been saved after import.
- The custom Preview remains honest: front matter is represented as a
  composition placeholder, not a fake Word page render.
- Word consumers must process the WordprocessingML alternative-format import to
  materialize the imported content; Sprint 8 does not build its own DOCX merge
  engine.
- New worksheet configurations can safely map the same Excel column letter to
  different semantic fields.
- Legacy workbook-wide mappings remain readable but are no longer written by
  the Sprint 8 transfer/configuration workflow.

## Rejected alternatives
- Reusing `TableElement.Description` as the caption — rejected because it
  changes the established notes semantic and would leak author notes into final
  documents.
- A live `Table -> Heading` reference — rejected because the requested workflow
  only needs a value copy and a reference introduces lifecycle/selection
  coupling.
- Deleting or moving the heading after caption copy — rejected as an unexpected
  document-structure mutation.
- Copying imported DOCX `Body` children into the generated document — rejected
  because body XML alone does not carry all package relationships/styles/media.
- Word Interop/COM, PDF conversion, or WebView-based preview — rejected by Sprint
  8 scope and because they would turn a composition foundation into a major
  rendering/automation subsystem.
- Embedding DOCX bytes in `project.json` — rejected; `.kws` is already a ZIP
  container and imported binary assets belong in separate entries.
- A broad DataSource redesign — rejected; moving primary mappings to Worksheet
  plus a legacy fallback is the smallest local correction for the proven
  multi-sheet defect.
