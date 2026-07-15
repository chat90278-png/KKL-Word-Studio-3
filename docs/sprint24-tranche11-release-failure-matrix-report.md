# Sprint 24 Tranche 11 — Release Failure Matrix

Base: `main@d512c3d6ee2383106f0295fb5ded5babe9cb9ac3`

This tranche adds regression coverage only. Production readers and import services are unchanged.

Covered failure and recovery cases:

- missing Excel source returns a controlled failure;
- corrupt Excel package returns a controlled failure;
- missing worksheet configuration is rejected before file access;
- missing data range is rejected before file access;
- in-memory WorkingData remains authoritative when the original file disappears;
- missing and corrupt reference-format DOCX files return controlled failures;
- missing and corrupt front-matter DOCX files return controlled failures.

Expected integrated inventory:

```text
Domain           20
Application     299
Engine           71
Architecture    128
Infrastructure  159
-------------------
Total           677 / 677
```

Windows Release build/test and manual missing/corrupt-file smoke remain pending.
