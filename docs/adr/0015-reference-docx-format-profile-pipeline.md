# ADR 0015 — Reference DOCX format profile pipeline

**Status:** Accepted for Sprint 16 contract bootstrap  
**Date:** 2026-07-09

## Context

Sprint 15 established one semantic table-row/span model shared by Preview and Word. The next product requirement is different: generated KKL content should follow the supported visual properties of a selected reference DOCX, specifically the supplied `Sero.docx`.

The existing `FrontMatterDocument` contract cannot own this responsibility. Front matter is arbitrary imported Word content composed before the generated report and previewed as imported blocks. A reference format is not report content at all: it is a project-owned source from which supported page, paragraph, caption, and table properties are resolved.

Putting OpenXML inspection in `ReportContentBuilder`, Engine, Preview, or Word would create four competing format interpretations. Likewise, allowing Word to inspect the DOCX directly would break the shared `ReportContentDocument` semantic path and make Preview fidelity impossible to reason about.

## Decision

### 1. Reference format is a distinct project-owned asset

`Project.ReferenceFormat` points to `ReferenceFormatDocument`.

The asset has stable package identity at:

`resources/reference-format/reference-format.docx`

It is not prepended to output, not converted into `ReportElement` objects, and does not replace `Project.FrontMatter`.

### 2. Application owns the normalized format contract

`KKL.WordStudio.Application.Formatting` defines one UI-independent format vocabulary:

- `DocumentFormatProfile`
- `PageFormatProfile`
- `ResolvedTextFormat`
- `TableCaptionSequenceProfile`
- `ReferenceTableFormatProfile`
- `ResolvedTableFormat`
- `ResolvedTableColumnFormat`
- `VerticalContentAlignment`

`IReferenceDocumentFormatProvider` is the only boundary that reads the project-owned reference-format asset. Infrastructure may use OpenXML behind this interface.

`IReportContentFormatResolver` maps an optional reference profile plus current KKL semantics to resolved content/page formats. Consumers do not inspect the reference DOCX.

### 3. Format resolution happens in `ReportContentBuilder`

The orchestration order is:

```text
Project / Report
        ↓
IReferenceDocumentFormatProvider
        ↓
DocumentFormatProfile?
        ↓
existing semantic content build
        ↓
serial/quantity row composition
        ↓
IReportContentFormatResolver
        ↓
TextContentNode.Format
TableContentNode.Format / CaptionSequence
ReportContentDocument.PageLayout / FormatWarnings
```

Serial/quantity composition remains semantic work and stays before table-format application. Formatting does not re-open Excel, re-normalize multi-source rows, or infer grouping roles.

### 4. Shared content carries resolved formats

`TextContentNode` carries `ResolvedTextFormat` while retaining `Bold` and `FontSize` compatibility properties for Sprint 16 migration.

`TableContentNode` carries `ResolvedTableFormat` and optional `TableCaptionSequenceProfile` while retaining all Sprint 15 row/span/group semantics.

`ReportContentDocument` carries non-blocking `FormatWarnings`.

`PageLayout` adds header/footer distance.

Layout payloads carry the same resolved text/table formats so Engine and Preview do not need a second format resolver.

### 5. Reference table selection uses a stable profile key

`TableElement.ReferenceTableFormatKey` persists the selected reference-table profile key.

The key is not a DOCX table index contract and is not a displayed header string. A reference-aware resolver owns selection rules: explicit key first, otherwise matching column count, then first reference profile, then generic fallback.

### 6. Caption numbering preserves field semantics

When the reference contains a real Word `SEQ` field, the normalized profile records:

- display label
- sequence identifier
- separator

Word consumes that semantic contract and emits a real `SEQ` field. Cached visible field numbers are not persisted as plain numbering text.

### 7. Compatibility defaults remain available

Direct callers and existing tests can still construct `ReportContentBuilder(IDataProviderRegistry)` or `ReportContentBuilder(IDataProviderRegistry, ITableContentRowComposer)`.

Those constructors use:

- `NoReferenceDocumentFormatProvider`
- `DefaultReportContentFormatResolver`

The bootstrap resolver preserves current authored KKL style/page semantics and does not interpret reference DOCX profiles. Production DI exposes replaceable provider/resolver seams for Sprint 16 teams.

Content and layout payload format properties have deterministic compatibility defaults so existing direct initializers remain source-compatible.

## Sero.docx profile decision

The exact supported-property evidence and normalized target values are frozen in:

`docs/SERO-REFERENCE-FORMAT-PROFILE.md`

The profile deliberately keeps Table 1 and Table 2 separate because they have different width ratios and header/body formatting details. Real `w:vMerge` observed in Table 2 is evidence of the visual reference only; grouping semantics continue to come from Sprint 15 `TableCellSpan` composition.

## Consequences

### Positive

- Preview and Word consume one resolved format interpretation.
- OpenXML extraction remains Infrastructure-only.
- Engine measures resolved properties rather than guessing DOCX semantics.
- Reference formatting and front matter remain independent.
- Table-profile selection survives displayed header renames.
- Sprint 15 grouped-table spans remain independent from visual format extraction.

### Trade-offs

- Compatibility properties temporarily coexist with richer resolved formats.
- A supported-property profile cannot represent arbitrary Word constructs.
- Mixed formatting inside a reference DOCX may require deterministic selection plus warnings.
- Pixel-identical Word fidelity is explicitly not claimed.

## Rejected alternatives

### Use `FrontMatterDocument` as the reference template

Rejected because front matter is composed content, while a reference format is a formatting source and must not appear as generated pages.

### Parse the reference DOCX in Engine, Preview, or Word

Rejected because it duplicates interpretation and breaks the shared semantic content path.

### Persist raw DOCX table indexes on `TableElement`

Rejected because table position in a reference file is not a stable product identity. `ReferenceTableFormatKey` is the normalized profile identity.

### Infer Sprint 15 grouping from reference `w:vMerge`

Rejected because visual merge state is not source-data grouping configuration. Grouping continues to use stable KKL table-column IDs and quantity/serial semantics.

### Claim Word pixel fidelity

Rejected. Sprint 16 targets supported structural/property fidelity only.
