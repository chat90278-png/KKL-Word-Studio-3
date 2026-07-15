# Sprint 24 Tranche 10

Base: `main@52db15e3b5d75548b59fa0395719d42dde6c3933`

This test-only tranche locks three Preview measurement cases:

- compatibility fallback equals explicit physical cell insets;
- larger vertical margins increase measured table height;
- long `NoWrap` identifiers do not add Preview fragments.

Production layout and Word code are unchanged.

Windows result: build `0 warnings / 0 errors`, tests `668/668`.
