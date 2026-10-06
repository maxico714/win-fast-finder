# BUILDING — win-fast-finder

Everything you need to build, test, run and package this project from a clean
clone.

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 8.0.x | `dotnet --version` → `8.0.4xx` |
| Windows | 10 x64 / 11 x64 | — |
| PowerShell | 5.1+ (for packaging scripts) | `powershell -v` |

No other toolchain is required. There is no Node, no Python, no CMake, and no
runtime NuGet package dependency.

The SDK pins itself via [`global.json`](../global.json) with
`rollForward: latestFeature`, so any 8.0.x SDK satisfies the build.

## Build

```powershell
dotnet build Wff.sln --no-incremental
```

Expected: **0 warnings, 0 errors.**

`--no-incremental` is deliberate — it forces a full recompile so you cannot
mistake stale output for a clean build. Omit it during rapid iteration.

## Test

```powershell
dotnet test tests/Wff.Tests/Wff.Tests.csproj --no-build
```

Expected: **62 passed, 0 failed.**

Use `--no-build` after a successful build; without it `dotnet test` may
rebuild and interleave output.

> Command gotcha: `dotnet test --no-incremental` is **not** a valid flag and
> fails. Build first, then test with `--no-build`.

## Run

```powershell
# GUI
dotnet run --project src/Wff.App

# Headless — index and query
dotnet run --project src/Wff.App -- --index "C:\Some\Folder"
dotnet run --project src/Wff.App -- --index "C:\Some\Folder" --query "report"
```

For the GUI, press **Ctrl+Shift+F** to summon. See
[USAGE](USAGE.md) for the full feature set.

### Proving the hotkey works

A synthetic-keypress harness launches the real app, presses **Ctrl+Shift+F**
via `SendInput`, and asserts that a visible window titled `win-fast-finder`
appears:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\e2e\Invoke-WffE2E.ps1
```

Expected final line: `RESULT: PASS`.

> If it reports `registered=False err=1409`, another instance is holding the
> hotkey — see [TROUBLESHOOTING](TROUBLESHOOTING.md).

## Project layout

```
Wff.sln
Directory.Build.props        ← version + authors + repo URL (single source)
global.json                  ← SDK pin
src/
  Wff.Engine/                ← index, search, walk, cache, settings (no UI)
  Wff.App/                   ← WPF popup, hotkeys, headless CLI
  Wff.Setup/                 ← installer wizard (UAC)
tests/
  Wff.Tests/                 ← 62 xunit tests
  e2e/                       ← synthetic-keypress harness
build/
  package.ps1                ← release packaging
docs/
```

## Version

`Directory.Build.props` holds the version. To cut a release:

1. Bump `<Version>` there.
2. Add a section to [`CHANGELOG.md`](../CHANGELOG.md).
3. Commit, tag `v1.0.2`, push the tag — CI builds and attaches artifacts.

Individual `.csproj` files deliberately carry **no** `<Version>` element; do
not add one back.

## Package a release locally

```powershell
.\build\package.ps1
```

This runs, in order:

1. `dotnet build Wff.sln --no-incremental` (must be 0 errors)
2. `dotnet test --no-build` (must be 62/62)
3. Publish `Wff.App` framework-dependent, single-file → `dist\win-fx`
4. Publish `Wff.Setup` framework-dependent → `dist\setup-fx`
5. Assemble `payload-slim.zip` (app exe + LICENSE)
6. Concatenate setup stub + payload + 8-byte length footer →
   `dist\win-fast-finder-Setup-<version>.exe`
7. Write a `.sha256` file
8. Produce a source zip excluding `bin/`, `obj/`, `dist/`

The installed footprint is roughly 1 MB because the .NET runtime is a
**prerequisite, not a bundle** — framework-dependent publish keeps the
installer at a few hundred KB instead of hundreds of MB.

## Clean-room rebuild

To prove the tree has no hidden state:

```powershell
Get-ChildItem src,tests -Recurse -Directory -Include bin,obj |
  Remove-Item -Recurse -Force
dotnet build Wff.sln --no-incremental
dotnet test tests/Wff.Tests/Wff.Tests.csproj --no-build
```

## Troubleshooting the build

| Problem | Cause / fix |
|---|---|
| `MSB4018 ... file is being used` during publish | A `Wff.App.exe` instance is running from `dist\win-fx` — end it first |
| `error NU1101` / restore failures | Check network access to nuget.org; the only packages are test-time |
| Version shows `1.0.0` unexpectedly | You added `<Version>` back to a csproj — remove it; `Directory.Build.props` wins |
| Hotkey test fails with `err=1409` | Stale instance holding the hotkey — see [TROUBLESHOOTING](TROUBLESHOOTING.md) |

## CI

`.github/workflows/ci.yml` builds and tests on `windows-latest` for every push
and pull request. The README badge tracks that workflow. Release builds are
produced by `.github/workflows/release.yml` when a `v*` tag is pushed.
