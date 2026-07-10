# CONTRACT CHANGE REQUEST — TEAM D

## Sprint 16 / Word caption resolved-format transport

### Exact contract symbol

`KKL.WordStudio.Application.Content.TableContentNode`

Related frozen symbols:

- `KKL.WordStudio.Application.Formatting.DocumentFormatProfile.TableCaption`
- `KKL.WordStudio.Application.Content.ReportContentBuilder.BuildComposedTableNode`
- `KKL.WordStudio.Application.Content.ReportContentBuilder.BuildMultiSourceErrorNode`

### Exact issue

The Sprint 16 formatting contract resolves a supported reference caption format as `DocumentFormatProfile.TableCaption : ResolvedTextFormat`.

However, the shared semantic `TableContentNode` transports only:

- `ResolvedTableFormat Format`
- `TableCaptionSequenceProfile? CaptionSequence`

It does **not** transport the resolved caption `ResolvedTextFormat`.

`WordContentWriter` and `WordParagraphWriter` consume `TableContentNode` after `ReportContentBuilder` has finished. They do not and must not read the reference DOCX or the project reference-format asset. Therefore the Word layer has no supported-property source for the Sero caption font, size, alignment, spacing, indent, or keep-with-next values.

For the supplied Sero profile this loses, among other values:

- Arial
- 8 pt canonical caption size
- centered alignment
- 2.0 line-spacing multiple
- 12.488 mm first-line indent
- keep-with-next

The real `SEQ Tablo` field can be emitted from `CaptionSequence`; the caption **appearance** cannot be emitted faithfully from the frozen shared semantic payload.

### Why Team A/B/C/D cannot safely integrate without a contract change

- Team A owns reference DOCX format extraction and can populate `DocumentFormatProfile.TableCaption`, but it cannot carry that value into Word.
- Team B owns format resolution/UI and cannot alter frozen `TableContentNode` or `ReportContentBuilder` to add a transport member.
- Team C owns Engine/Preview and must not create a Word-only side channel.
- Team D owns Word writers but must consume resolved shared formats and must not re-open the reference DOCX, inspect `Project.ReferenceFormat`, or hard-code Sero caption values.

Any Team D workaround would create one of the explicitly prohibited designs:

1. Sero-specific constants in Word;
2. reference DOCX parsing in Word export;
3. a Word-only caption formatting resolver/duplicate semantic model.

### Smallest compatible change

Add one default-compatible optional property to `TableContentNode`:

```csharp
public ResolvedTextFormat? CaptionFormat { get; init; }
```

Populate it in both `ReportContentBuilder` table-node creation paths:

```csharp
CaptionFormat = formatProfile?.TableCaption,
```

That is sufficient for Team D to:

- preserve current legacy caption behavior when `CaptionFormat` is null;
- apply the resolved reference caption properties when it is present;
- keep `CaptionSequence` as the independent real-SEQ semantic contract.

No new interface, resolver method, Domain property, persisted state, WPF type, OpenXML type, or reference-DOCX access is required.

### Migration impact

Low and additive.

- Existing serialized project state is unaffected because `TableContentNode` is runtime Application content, not persisted Domain state.
- Existing direct initializers remain source-compatible because the property is optional/default-null.
- `ReportContentBuilder` changes are two narrow assignments in frozen bootstrap code.
- Team D updates `WordContentWriter` / `WordParagraphWriter` to consume `CaptionFormat` when non-null.
- Sprint 15 table rows, spans, groups, and vMerge semantics are unchanged.
- WordExporter remains on `ReportContentDocument`; no `DocumentLayoutResult` or reference asset path is introduced.

### Team D branch behavior until approved

Team D implements all Word fidelity that the frozen contract can carry and emits a real `SEQ` field from `CaptionSequence`.

It deliberately does **not** hard-code Sero caption appearance. The named `SeroWord_Caption_WritesCenteredReferenceFormat` fidelity gate remains red with a precise contract-block message until this change is approved and integrated.
