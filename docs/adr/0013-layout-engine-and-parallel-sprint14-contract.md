# ADR 0013: Layout Engine and Parallel Sprint 14 Contract

- Status: Accepted
- Date: 2026-07-09

## Context

ADR 0002 deliberately deferred a pagination Engine until real layout work justified a separate layer. Sprint 14 introduces physical page surfaces, page flow, repeated regions, table fragmentation, imported front-matter preview semantics, and page-number projection. Keeping these responsibilities in WPF Rendering or in the preview UI would make interaction code own document execution and would encourage multiple incompatible page models during parallel implementation.

The existing generated-content semantic path remains authoritative:

`Project + Report -> IReportContentBuilder -> ReportContentDocument`

`ReportContentDocument` already contains the generated header, body, footer, table-of-contents semantics, and page layout required by a layout consumer.

## Decision

Create `KKL.WordStudio.Engine` as a .NET 8 layer that consumes Application contracts and `ReportContentDocument`.

The Engine owns pagination and layout behavior. Its public boundary is the frozen `KKL.WordStudio.Application.Layout` contract, centered on `IDocumentLayoutEngine`, `DocumentLayoutRequest`, and `DocumentLayoutResult`.

Rendering remains interaction-only. Hit testing, selection, snapping, rulers, zoom, and viewport behavior may use laid-out coordinates, but Rendering does not execute document pagination.

The UI consumes `DocumentLayoutResult` through `PreviewSnapshot.Layout`. Sprint 14 temporarily keeps `PreviewSnapshot` compatibility properties so the current Preview UI can remain unchanged while the paginated surface is implemented.

`WordExporter` continues to consume `ReportContentDocument`, not Engine layout coordinates. Microsoft Word is itself a pagination engine; exporting precomputed WPF positions would create a second absolute-position document model, reduce DOCX editability, and weaken the existing shared semantic path.

Imported DOCX preview is a supported-semantic extraction path. It may extract the frozen subset of paragraph, run, table, image, explicit page-break, and section layout semantics. It is not Microsoft Word pixel rendering. Unsupported Word constructs must be surfaced honestly as warnings or unsupported blocks.

## Dependency Direction

The new production dependency direction is:

`Shared <- Domain <- Application <- Engine`

Infrastructure continues to depend on Application/Domain/Shared and owns OpenXML/file-system concerns.

UI is the composition root and may reference Application, Engine, Infrastructure, Rendering, Domain, and Shared.

Engine must not reference UI, Infrastructure, Rendering, or OpenXML. Application and Domain must not reference Engine.

## Parallel Sprint 14 Contract

The Application layout contracts, imported-document contracts, `PreviewSnapshot.Layout` shape, and solution/project bootstrap structure are frozen after this bootstrap. Teams A, B, C, and D start from the same contract baseline and must not invent competing page/block/layout models.

A compileable single-page fallback engine and a non-parsing imported-document preview provider exist only to keep the shared contract baseline runnable until the owning teams replace those implementations.

## Consequences

- Engine is now justified by real pagination/layout responsibilities rather than speculative architecture.
- Generated Preview and Word remain semantically tied through `IReportContentBuilder` and `ReportContentDocument`.
- WPF page rendering can consume a stable layout DTO without taking ownership of pagination.
- Native DOCX extraction remains Infrastructure-owned and read-only.
- Parallel teams have one page/layout vocabulary and explicit dependency boundaries.
- Windows/.NET 8 verification remains the final WPF/runtime truth before parallel team work is integrated.
