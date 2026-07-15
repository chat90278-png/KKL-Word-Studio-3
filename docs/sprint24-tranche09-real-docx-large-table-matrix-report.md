# Sprint 24 Tranche 09

Base: `main@152aba1293f36faad1ef3b3281d86e1518111ed5`

This tranche adds four real DOCX regression tests and corrects the OpenXML table-border child order in the single production Word writer.

Covered scenarios:

- 100 rows and six columns are written once and in order.
- Header repeat and `CantSplit` row properties are preserved.
- Consecutive tables do not receive an explicit blank page.
- A physical long-text DOCX reopens and passes `OpenXmlValidator`.

The valid border order is:

```text
top, left, bottom, right, insideH, insideV
```

The supplied highest-stack Windows run completed with build `0 warnings / 0 errors` and `668/668` tests. Tranche 09 itself contributes the Infrastructure total increase to `150`.
