# Sprint 14 — Team C Word / OpenXML Fidelity Report

## Role and scope

Team C operated as the Word/OpenXML Fidelity team from the stabilized Sprint 14 contract baseline. Work stayed inside the Infrastructure/OpenXML ownership boundary plus Team C Sprint 14 Infrastructure tests and this report. The frozen Application layout/imported-document contracts, Engine, UI, Domain, Rendering, `PreviewSnapshot`, and `PreviewRenderer` were not changed.

No contract change request was required.

## Imported front-matter preview extraction

Added `OpenXmlImportedDocumentPreviewProvider` and replaced the Infrastructure DI fallback registration with the real OpenXML implementation.

Behavior:

- no `Project.FrontMatter` → `Document = null`, `IsMissing = false`;
- persisted but unavailable front matter → `Document = null`, `IsMissing = true`, Turkish status text;
- readable DOCX → source resolved through `FrontMatterSourcePathResolver` and opened with `WordprocessingDocument.Open(path, false)`;
- the source package is never saved or modified by the preview provider.

### Supported section/page geometry

The provider reads body-level section properties and paragraph section boundaries where directly represented. It projects page width, page height, margins, and portrait/landscape geometry into the frozen `PageLayout` contract. Word twips are converted to millimeters.

When geometry is absent or only partially represented, A4 portrait with existing KKL default 20 mm margins is used for missing values. A warning explains that full Word section inheritance fidelity is not modeled. Multi-column sections are surfaced with a fidelity warning and remain a single preview flow column.

### Supported paragraphs and styles

Paragraphs are extracted in document order as `ImportedParagraphBlock` values. The implementation preserves separate runs and supports:

- visible text;
- tabs and line/carriage breaks;
- bold, italic, underline;
- font size and font family where directly/style-resolved;
- left, center, right, and justify alignment;
- keep-with-next;
- page-break-before.

A bounded style resolver applies document defaults, paragraph style run properties, a bounded `basedOn` style chain, optional character style properties, then direct run formatting. Direct formatting wins. Paragraph alignment/keep-next/page-break-before use direct paragraph properties, then paragraph style, then defaults.

Simple and complex Word fields are not executed. Stored visible result runs are extracted where present and warnings state the fidelity limit. Tracked changes, comments, and advanced numbering are also surfaced with concise warnings. Deleted revision text is not treated as current visible text. Inline wrapper/content-control style elements with nested runs are projected as flow content instead of being silently dropped.

### Supported tables

Body tables are extracted as `ImportedTableBlock` values with rows and cells in source order. Empty cells are preserved. Multiple paragraphs in a cell are joined with newlines. First-row repeat semantics are read from Word table-row header properties.

Merged-cell geometry, nested table layout, and table-cell image placement are not claimed as faithful. These cases emit warnings. Nested tables are flattened to best-readable row/cell text so meaningful content is not silently lost.

### Supported images

Common DrawingML images are resolved from `Drawing` → `Blip` embed relationship → `ImagePart` and returned as `ImportedImageBlock` values with:

- name;
- raw image bytes;
- content type;
- width/height from Drawing extent when present.

Image bytes are read from the package and no temp image path is exposed. Floating DrawingML anchors are treated as ordinary flow images with an explicit fidelity warning. Broken/unreadable image relationships become unsupported placeholders/warnings rather than failing the entire semantic preview. VML/legacy picture content is surfaced as unsupported.

### Explicit page breaks and unsupported constructs

Explicit `Break Type=Page` and resolvable paragraph `pageBreakBefore` semantics become `ImportedExplicitPageBreakBlock` values. The provider does not guess page breaks from Word pagination.

Meaningful unsupported content is represented with `ImportedUnsupportedBlock` and/or document warnings, including text boxes/shapes, SmartArt/diagram content, charts, equations, embedded objects/controls, unsupported field fidelity, advanced floating positioning, footnote/endnote body content, comments/revisions, and other unsupported body elements.

The implementation does not claim Microsoft Word pixel identity.

## Generated Word fidelity polish

### Heading keep-with-next

`WordParagraphWriter` now emits `w:keepNext` for generated Heading1/Heading2 paragraphs while preserving existing heading styles and run formatting behavior.

### Repeated table header row

`WordTableWriter` now marks the generated column-header `TableRow` with Word table-header semantics so Word may repeat that row across page boundaries.

### Deterministic table width/layout

Generated tables now receive:

- preferred table width = 100% (`pct=5000`);
- fixed table layout;
- deterministic equal percentage cell widths derived from the generated column count;
- an explicit table grid column count.

No persisted/user-defined column width model was invented.

### Page orientation

`WordPageLayoutWriter` continues to write the shared `PageLayout` dimensions and margins, and now writes an explicit Word page orientation value based on those dimensions. Landscape width/height remain the shared semantic dimensions rather than being re-derived from a layout-engine result.

### Preserved fields, parts, and export pipeline

The following existing behavior remains intact:

- Heading1/Heading2 styles;
- TOC field;
- PAGE field;
- header/footer generation;
- explicit Styles/Header/Footer save behavior;
- source-error export refusal;
- `IReportContentBuilder → ReportContentDocument → Word writers` export flow.

`WordExporter` does not consume `DocumentLayoutResult` coordinates.

## Front-matter final composition

The final Word composition path remains package-preserving:

`AlternativeFormatImportPart + w:altChunk + explicit page break + generated KKL report`

The preview extractor is a separate read-only semantic projection. It does not replace altChunk composition, does not clone source `Body` children, and does not mutate the source DOCX.

## Focused Sprint 14 tests added

Real generated DOCX fixtures were added for:

- `ImportedPreview_NoFrontMatter_ReturnsNullDocument`
- `ImportedPreview_MissingAsset_ReturnsMissingState`
- `ImportedPreview_DoesNotModifySourceDocx`
- `ImportedPreview_ExtractsParagraphTextAndRunFormatting`
- `ImportedPreview_ResolvesParagraphStyleFormattingAndKeepNext`
- `ImportedPreview_ExtractsParagraphAlignment`
- `ImportedPreview_ExtractsTableRows`
- `ImportedPreview_ExtractsInlineImageBytesAndDimensions`
- `ImportedPreview_ExtractsExplicitPageBreak`
- `ImportedPreview_UsesSectionPageGeometry`
- `ImportedPreview_UnsupportedShape_IsWarnedOrPlaceholder`
- `ImportedPreview_PreservesDocumentOrderAcrossParagraphTableImage`
- `HeadingWordParagraph_UsesKeepNext`
- `WordTable_HeaderRowRepeats`
- `WordTable_UsesDeterministicFullWidthLayout`
- `LandscapePageLayout_WritesConsistentOrientation`
- `FrontMatterAltChunkComposition_RemainsIntact`
- `GeneratedWord_ReopensWithOpenXmlAfterFidelityChanges`

Baseline tests were not weakened or edited.

## Verification

The requested commands were attempted in this environment with actual shell execution:

```text
dotnet restore
exit 127
bash: dotnet: command not found


dotnet build
exit 127
bash: dotnet: command not found


dotnet test
exit 127
bash: dotnet: command not found
```

Therefore this report does **not** claim restore/build/test success. Windows/.NET 8 verification remains pending and is the final WPF/runtime truth.

Static/package checks completed in the available environment:

- all required focused Sprint 14 test names are present;
- modified C# files passed delimiter-balance scanning for braces, parentheses, and brackets;
- Infrastructure DI has a single real `IImportedDocumentPreviewProvider` registration to `OpenXmlImportedDocumentPreviewProvider`;
- obsolete fallback provider was removed from the Team C delivery workspace;
- fresh-baseline diff shows changes only in Team C-owned Infrastructure/OpenXML files, Team C Sprint 14 Infrastructure test files, and this report;
- no frozen Application contract, Domain, Engine, UI, Rendering, `PreviewSnapshot`, or `PreviewRenderer` file changed;
- final ZIP package integrity was checked with `unzip -t` after packaging.

## Contract status

The frozen Sprint 14 shared contract was sufficient for Team C. `docs/CONTRACT_CHANGE_REQUEST-C.md` was not created.
