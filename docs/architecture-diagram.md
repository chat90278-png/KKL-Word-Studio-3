# KKL Word Studio — Architecture Diagram (post Sprint 8)

## Layer dependency graph

```mermaid
graph TD
    Shared["KKL.WordStudio.Shared<br/>(Result, Guards, Extensions, Spreadsheet helpers, ThemeKeys)"]
    Domain["KKL.WordStudio.Domain<br/>(Project, FrontMatterDocument, Report, DataSource hierarchy, Elements, Binding)"]
    Application["KKL.WordStudio.Application<br/>(content semantics, editing/transfer use cases, validators, abstractions, Workspace)"]
    Infrastructure["KKL.WordStudio.Infrastructure<br/>(.kws persistence, OpenXML Excel reader/provider, Word export/composition)"]
    Rendering["KKL.WordStudio.Rendering<br/>(Hit-testing, Selection, Snap, Zoom, Rulers)"]
    UI["KKL.WordStudio.UI<br/>(WPF composition root, ViewModels, Views, OS drag/drop routing)"]

    Domain --> Shared
    Application --> Domain
    Application --> Shared
    Infrastructure --> Application
    Infrastructure --> Domain
    Infrastructure --> Shared
    Rendering --> Domain
    Rendering --> Shared
    UI --> Application
    UI --> Infrastructure
    UI --> Rendering
    UI --> Domain
    UI --> Shared

    style Domain fill:#e8f4ea,stroke:#3c8c4a
    style Rendering fill:#eaeef7,stroke:#3c5c8c
    style Application fill:#f7f0e0,stroke:#a67c1e
```

`Domain` remains framework- and I/O-free. File existence/package validation and front-matter path resolution live in Infrastructure. WPF code-behind only routes OS drag/drop gestures; validation decisions and actual open/import use cases remain outside pixel-level UI code.

## Domain model (Sprint 8)

```mermaid
graph TD
    Project["Project (aggregate root)"]
    Project --> DataSources["List&lt;DataSource&gt;"]
    Project --> Reports["List&lt;Report&gt;"]
    Project --> Settings["ProjectSettings"]
    Project --> FrontMatter["FrontMatterDocument?\n(file metadata + asset entry name)"]

    DataSources --> ExcelDataSource
    ExcelDataSource --> Workbook
    Workbook --> Worksheet
    Worksheet --> DataRange["DataRange"]
    Worksheet --> WorksheetMappings["ColumnMappings\n(primary per-sheet mapping)"]
    ExcelDataSource -. "legacy compatibility fallback" .-> LegacyMappings["DataSource.ColumnMappings"]

    Reports --> Report
    Report --> Page
    Page --> Section
    Section --> Container
    Container --> ReportElement
    ReportElement --> TextElement
    ReportElement --> ImageElement
    ReportElement --> TableElement
    ReportElement --> ShapeElement
    ReportElement --> BarcodeElement
    ReportElement --> ChartElement
    ReportElement --> DataRegion

    TableElement --> Caption["Caption?\nvisible persisted table title"]
    TableElement -. "Binding\n(DataSourceName + WorksheetName + Filter + SortFields)" .-> DataSources
    DataRegion -. "Binding" .-> DataSources

    style Project fill:#e8f4ea,stroke:#3c8c4a
    style TableElement fill:#fdeeee,stroke:#a83c3c
    style FrontMatter fill:#edf4ff,stroke:#3c6aa8
    style WorksheetMappings fill:#fef6e0,stroke:#a67c1e
    style Caption fill:#fef6e0,stroke:#a67c1e
```

Sprint 8 corrects mapping ownership locally: `Worksheet.ColumnMappings` is authoritative for configured worksheet datasets. `DataSource.ColumnMappings` remains only as a pre-Sprint-8 persistence/runtime fallback so older `.kws` projects continue to resolve.

## Excel intake and report semantic flow

```mermaid
graph LR
    Drop["Kaynak Veri drop target\n.xlsx / .xlsm"] --> Validator["SourceFileDropValidator"]
    Picker["Excel Dosyası Aç"] --> Open["ExcelWorkspaceViewModel.OpenWorkbookFromPathAsync"]
    Validator --> Open
    Open --> Reader["IExcelWorkbookReader\nOpenXML read-only"]
    Reader --> Workspace["worksheet tabs + preview + DataRange"]
    Workspace --> Transfer["IExcelReportTransferService"]
    Transfer --> Binding["TableElement.Binding\nWorksheetName pinned"]
    Transfer --> Columns["TableColumn.Header + SourceField"]
    Transfer --> WorksheetMappings["Worksheet.ColumnMappings"]
    Binding --> Content["ReportContentBuilder"]
    Columns --> Content
    WorksheetMappings --> Provider["ExcelDataProvider"]
    Provider --> Content
    Content --> Preview["WPF structured preview"]
    Content --> Word["Word content writers"]
```

The drag/drop path deliberately converges on the same `OpenWorkbookFromPathAsync` workflow as the picker and Project Explorer navigation. The original spreadsheet is opened read-only; Sprint 8 adds no Excel working-data editor.

## Table caption flow

```mermaid
graph LR
    Properties["Properties: Tablo Başlığı"] --> EditService["IReportEditingService.CommitTableCaption"]
    HeadingChoice["Başlıktan Al"] --> Copy["UseHeadingTextAsTableCaption"]
    Inline["Preview caption double-click"] --> EditService
    Copy --> Caption["TableElement.Caption"]
    EditService --> Caption
    Caption --> Builder["ReportContentBuilder"]
    Builder --> Semantic["TableContentNode.Caption"]
    Semantic --> Preview["PreviewRenderer / table block"]
    Semantic --> Word["WordContentWriter\ncaption paragraph above table"]
    Caption --> Persistence["project.json round-trip"]
```

`Başlıktan Al` copies the current Heading/AltHeading text by value. It does not create a live cross-element reference and does not delete or move the source heading.

## Front-matter ownership and final Word composition

```mermaid
graph TD
    DocxDrop["Rapor Tasarımı drop target\n.docx"] --> DropValidator["SourceFileDropValidator"]
    Add["Ön Belge Ekle"] --> Importer["IFrontMatterDocumentService"]
    DropValidator --> Importer
    Importer --> Validation["OpenXmlFrontMatterDocumentService\nread-only package validation"]
    Validation --> State["Project.FrontMatter"]
    State --> Kws["KwsProjectRepository"]
    Kws --> Asset[".kws ZIP entry\nresources/frontmatter/front-matter.docx"]
    State --> Placeholder["Structured preview\ncomposition placeholder"]

    Asset --> Resolver["FrontMatterSourcePathResolver"]
    State --> Resolver
    Resolver --> Exporter["WordExporter"]
    Exporter --> Composer["WordFrontMatterComposer"]
    Composer --> AltChunk["AlternativeFormatImportPart\n+ w:altChunk anchor"]
    AltChunk --> Boundary["explicit page break"]
    Boundary --> Generated["TOC + KKL generated semantic content"]
    Generated --> Section["section properties + header/footer + page layout"]
```

The composer does **not** clone source `Body` children into the generated package. The full imported DOCX is fed into an `AlternativeFormatImportPart`; the destination body receives only the `w:altChunk` anchor followed by an explicit page break. This keeps Sprint 8 composition narrow and avoids implementing a generic style/numbering/media/relationship merge engine.

The WPF preview intentionally shows a composition placeholder rather than claiming Word rendering fidelity or page counts. Final DOCX composition is authoritative.
