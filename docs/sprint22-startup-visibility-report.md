# Sprint 22 — Startup Visibility & Diagnostics Hardening

## Trigger

The responsive-batch head passed Release build and all 496 tests on Windows, but the WPF application did not open. Startup diagnostics exposed the concrete failure:

```text
'KKL.WordStudio.UI.ViewModels.QuickAssemblyViewModel' türünde 'ProgressPercent' salt okunur özelliğinde TwoWay veya OneWayToSource bağlama çalışamaz.
```

The terminal itself stayed quiet because the UI project is a `WinExe`; the diagnostic message box correctly surfaced the startup exception.

## Root cause

`QuickAssemblyView.xaml` bound `ProgressBar.Value` to the computed, read-only `QuickAssemblyViewModel.ProgressPercent` property without an explicit binding mode. `RangeBase.Value` requested a writable binding, so WPF attempted to write the control value back to the read-only source property while constructing `QuickAssemblyView`.

The exception occurred during dependency-injection resolution of `MainWindow`, before `MainWindow.Show()`.

## Fix

The progress binding is now explicitly one-way:

```xaml
Value="{Binding ProgressPercent, Mode=OneWay}"
```

A regression assertion verifies that:

- the explicit `Mode=OneWay` form exists;
- the unsafe implicit `Value="{Binding ProgressPercent}"` form does not exist.

## Existing startup hardening

- Startup stages are written to `%LOCALAPPDATA%\KKL Word Studio\Logs\wordstudio-YYYYMMDD.log`.
- Last-resort failures are appended to `%LOCALAPPDATA%\KKL Word Studio\Logs\startup-failures.log`.
- Global failure capture covers WPF dispatcher, AppDomain and unobserved task exceptions.
- Fatal startup errors show a message box with the diagnostic path.
- The main window is assigned explicitly, checked against the Windows virtual desktop, centered if off-screen, activated and focused.

## Safety

- No Excel, transfer, Preview, layout or Word pipeline was changed.
- The fix only corrects the UI binding direction.
- Startup logging remains best-effort and independent of successful DI host construction.

## Exact-head Windows validation

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

Expected test totals:

- Domain: 18
- Application: 209
- Engine: 60
- Architecture: 82
- Infrastructure: 127
- Total: 496

Use the SHA printed by `git rev-parse HEAD` as the exact validation head. The UI gate is GREEN only after the main window visibly opens and the Hızlı Rapor progress/cancel smoke succeeds.