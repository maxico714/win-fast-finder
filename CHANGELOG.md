# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Public open-source release: MIT licence, documentation set, CI workflows,
  contributing/security/support guides.
- `tests/e2e/Invoke-WffE2E.ps1` — synthetic-keypress harness proving
  Ctrl+Shift+F summons a visible window.
- `build/package.ps1` — reproducible release packaging (build → test →
  publish → assemble installer → checksum → source zip).
- `Directory.Build.props` — centralised version, authors and repository
  metadata.

### Fixed
- Startup no longer depends on the `Loaded` event. A never-shown hidden
  window may never raise it, which left hotkeys and indexing silently dead.
  `App.OnStartup` now drives `EnsureReady()` explicitly, showing the window
  once off-screen so it can own an HWND before registering the hotkey.
- The fullscreen guard no longer swallows hotkeys under an ordinary maximised
  window. It now requires the foreground window to cover the entire work area
  **and** have no caption (`WS_CAPTION` clear), i.e. real borderless
  fullscreen.
- Removed a duplicate `Loaded` handler in `MainWindow` left over from the
  startup fix (dead code).
- Version strings unified: 4 project files, the app manifest and the README
  now all report **1.0.1**.

### Changed
- `dist/` and build artifacts are excluded from version control; installers
  ship as GitHub Release assets.

## [1.0.1] - 2026-10-06

### Added
- Setup wizard: UAC elevation, default `C:\Program Files`, license gate,
  directory picker with validation, progress display, launch option,
  autostart option, Start Menu and Apps entries.
- Matching uninstall path (`Uninstall.exe --uninstall`) that removes app,
  data, shortcuts, autostart entry and registry keys. Double-clicking
  `Uninstall.exe` now asks for confirmation instead of launching the
  installer.
- .NET 8 Desktop Runtime prerequisite gate: the installer checks before
  copying anything and links to the Microsoft download.
- Production settings window (General / Hotkeys / Search / Index /
  Advanced): custom hotkeys, fullscreen-ignore, max results, path matching,
  rank/name/date/size sort, hidden & system toggle, theme System/Light/Dark,
  launch-at-login (HKCU, opt-out), export/import/reset, data folder, log
  viewer, index stats.
- Smart ignore UI: drives as tick-boxes, folder add validates the full
  address, extensions as tick-boxes populated from indexed files.
- Magnifier-and-bolt logo (window icon and `assets/logo.svg`), shown in the
  settings header and title bar.
- File log at `%APPDATA%\win-fast-finder\log.txt` with startup roots,
  hotkey registration status and index timings — so "nothing happens" is
  always diagnosable.
- AppData skipped by default, with a v2 settings migration for older files.

### Fixed
- Single-instance guard: a second copy previously fought the first over the
  hotkey and neither responded reliably.
- Ctrl hold auto-repeat no longer re-triggered the popup (edge-triggered
  press-release-press detection).
- Recall: matching now uses a union of bigram buckets so gappy queries no
  longer drop legitimate hits.
- Add-folder dialog is an owned modal and always stays in the foreground.
- First run indexes the user profile with live progress instead of silently
  walking every drive.

### Changed
- Slimmed ship builds: framework-dependent publish instead of self-contained.
  Setup 227 MB → 323 KB, app 154 MB → 237 KB, installed footprint 281 MB →
  about 1 MB.
- Popup collapses to just the search box until you type, and follows the
  Windows system theme.
- Ctrl+Shift+F now registers successfully at startup (the hidden-window HWND
  bug).

## [0.1.0] - 2026-10-05

### Added
- Initial skeleton: C# .NET 8 engine (parallel walker + bigram-narrowed fuzzy
  `FileIndex`), xunit suite, WPF popup with `Ctrl+Shift+F` via
  `RegisterHotKey`, and headless `--index` / `--query` mode.
- Measured baseline on a 5 142-file tree: index 699 ms, query 4 ms.

[Unreleased]: https://github.com/maxico714/win-fast-finder/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/maxico714/win-fast-finder/compare/v0.1.0...v1.0.1
[0.1.0]: https://github.com/maxico714/win-fast-finder/releases/tag/v0.1.0
