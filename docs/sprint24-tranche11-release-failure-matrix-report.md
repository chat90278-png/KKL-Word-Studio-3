# Sprint 24 Tranche 11 — Release Failure Matrix

Base: `main@d512c3d6ee2383106f0295fb5ded5babe9cb9ac3`

## Failure and recovery coverage

- missing Excel source returns a controlled failure;
- corrupt Excel package returns a controlled failure;
- missing worksheet configuration is rejected before file access;
- missing data range is rejected before file access;
- in-memory WorkingData remains authoritative when the original file disappears;
- missing and corrupt reference-format DOCX files return controlled failures;
- missing and corrupt front-matter DOCX files return controlled failures.

## Windows finding and correction

The first Windows run built with `0 warnings / 0 errors`, but two Infrastructure tests failed while deleting corrupt temporary packages. Path-based OpenXML open calls retained a file handle after package-open failure.

The authoritative production readers now own an explicit read-only `FileStream` before calling OpenXML:

- `ExcelDataProvider` opens the Excel source stream explicitly;
- `OpenXmlFrontMatterDocumentService` opens the front-matter stream explicitly.

If OpenXML rejects a corrupt package, the outer stream is still disposed deterministically. Failure messages, WorkingData precedence and successful import behavior are unchanged. The reference-format importer already used this safe stream-ownership pattern.

## Expected integrated inventory

```text
Domain           20
Application     299
Engine           71
Architecture    128
Infrastructure  159
-------------------
Total           677 / 677
```

Exact-head Windows build/test and manual missing/corrupt-file smoke remain pending.
