# Sprint 23 Tranche 00 — Contract Characterization Report

## Baseline

- Sprint 22 code baseline: `aa51d76850ce77596ba4e37a64559a926b11629a`
- Branch base: `main@8f7fd9781dd51a2ebded5214a6568154e0aea075`
- Branch: `sprint23/00-contract-characterization`

The branch base contains no production-code difference from the Sprint 22 baseline. Two documentation-only `main` commits created and removed an accidental placeholder before this branch was opened.

## Scope

This tranche intentionally changes no production behavior. It adds:

1. the detailed Sprint 23 dependency/tranche plan;
2. four Application characterization tests for the existing range detector;
3. two Infrastructure characterization tests for the current 100-row Preview limit and first-blank-row end detection;
4. four Architecture characterization tests for the existing Excel mapping surface, grid headers/sorting, fixed shell layout, flat report structure and single-level warning count.

## Test delta

- Previous total: `514`
- Added Application tests: `4`
- Added Infrastructure tests: `2`
- Added Architecture tests: `4`
- Expected total: `524`

## Windows gate — pending

Run on the final exact branch head:

```bat
git checkout sprint23/00-contract-characterization
git pull
git rev-parse HEAD

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected:

- 0 warnings;
- 0 errors;
- `524/524` tests;
- application startup smoke GREEN;
- no intended production UI/behavior change.

This tranche must not be called GREEN until the user supplies the Windows output for the exact head.
