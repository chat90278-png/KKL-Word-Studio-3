# Sprint 22 — Startup Visibility & Diagnostics Hardening

## Trigger

The exact responsive-batch head passed Release build and all 494 tests on Windows, but:

- `dotnet run --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj` did not show the application window;
- the terminal remained attached without printing a runtime error.

The lack of terminal output is expected for a WPF project whose output type is `WinExe`. The lack of a visible main window is not accepted as GREEN UI smoke.

## Hardening added

- Startup stages are written to a stable per-user log folder:
  - `%LOCALAPPDATA%\KKL Word Studio\Logs\wordstudio-YYYYMMDD.log`
- Last-resort startup failures are appended to:
  - `%LOCALAPPDATA%\KKL Word Studio\Logs\startup-failures.log`
- Stage markers distinguish:
  - application startup entry;
  - DI host construction;
  - `MainWindow` resolution;
  - `MainWindow.Show` completion;
  - first content render.
- Global failure capture covers:
  - WPF dispatcher exceptions;
  - AppDomain unhandled exceptions;
  - unobserved task exceptions.
- Fatal dispatcher/startup errors show a message box with the diagnostic path.
- The application explicitly assigns `Application.MainWindow`.
- The main window is forced out of a minimized state, shown in the taskbar, checked against the Windows virtual desktop, re-centered if off-screen, activated and focused.

## Safety

- No Excel, transfer, Preview, layout or Word pipeline was changed.
- Startup logging is best-effort and cannot replace the original exception.
- Emergency logging does not depend on the DI host being built.
- Existing file logging moved from a working-directory-relative `logs` folder to a writable and discoverable per-user folder, matching the Sprint 22 release-readiness requirement.

## Windows validation

```bat
git checkout sprint22/release-readiness-big-data
git pull
git rev-parse HEAD

taskkill /IM KKL.WordStudio.exe /F 2>nul

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

Expected test totals after the two startup architecture guards:

- Domain: 18
- Application: 209
- Engine: 60
- Architecture: 82
- Infrastructure: 127
- Total: 496

## If the window is still not visible

Stop the attached run with `Ctrl+C`, then print the two diagnostic files:

```bat
type "%LOCALAPPDATA%\KKL Word Studio\Logs\wordstudio-20260713.log"
type "%LOCALAPPDATA%\KKL Word Studio\Logs\startup-failures.log"
```

Also verify whether the process remained alive:

```bat
tasklist | findstr /I "KKL.WordStudio"
```

The last startup stage in the log identifies whether the application is blocked during host construction, view resolution, window showing, or first render.
