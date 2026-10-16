# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.2] - 2026-10-16

### Added
- Logical operators `[AND]` (explicit default) and `[OR]` (either side
  may match, AND binds tighter, directives stay global) — amber keywords
  in the box, `[` offers both in suggestions.
- Installer compatibility gate: the directory page runs OS + runtime
  checks off the UI thread; **Install** stays disabled with reasons
  until the PC passes, with a download button and Recheck.
- Installer shows the real assembly version everywhere (welcome title,
  registry DisplayVersion) instead of a hardcoded string.
- Full-path blacklist entries now hold through incremental refresh
  (previously only bare folder names did).
- Full `size:` grammar: `>=` `<=` `=`, decimals (`1.5gb`), short units
  (`k m g t`), `tb`, ranges (`1mb..5mb`), spaced units (`size:>1 gb`).
- Full `dm:` grammar: hours/weeks/months/years (`dm:<12h`), `today` /
  `yesterday`, absolute ISO days (`dm:2024-05-10` = whole local day),
  date ranges, `>=` / `<=` / `=`.
- Bare `size:` follows Settings → Search → **Exact size**: Displayed size
  like Explorer (default), Exact bytes, or ±1%. New `=` form is always
  byte-exact.
- Invalid directives glow **red** with a status-line reason instead of
  failing silently; amber always means "understood".
- Size + modified time stored in the index (cache format v2, old caches
  rebuild automatically): filters and sorts run fully in-memory.
- Sorting runs over the **whole** match set (the 20,000 scored cap is gone);
  optional `N of M` total in the status line; max results up to 1000.
- Search cancellation per keystroke, longer debounce for directive queries,
  optional slow-query logging.
- Settings: exact-size mode, binary/decimal units, local/UTC days, total
  count, size badge toggle, filter delay, slow-query log, Quick/Deep
  refresh, query cheat-sheet window, live version line.
- In-box keyword highlighting: directive names glow amber, values and globs
  cyan, as you type — what you see is what the parser sees.
- Autocomplete suggestion list: directive names for a prefix (`ex`→`ext:`),
  index-backed `ext:` values with file counts; Tab/click accepts, ↑↓ move,
  Esc dismisses, Enter always opens the file.
- `SizeFormat` helper and per-row file sizes in results; the size glows amber
  while a `size:` filter is active.
- `path:value` directive form (`path:Downloads` ≡ `Downloads path:`).
- Multi-term queries use AND semantics in any order (`invoice 2024` finds
  `invoice-2024.pdf`).
- Size/date filters now test every candidate instead of the first 500 scored.
- Installer stub ships single-file (bare apphosts die without their DLLs);
  packaging refuses a non-single-file stub and wipes stale publish dirs.
- Public open-source release: MIT licence, documentation set, CI workflows,
  contributing/security/support guides.
- `tests/e2e/Invoke-WffE2E.ps1` — synthetic-keypress harness proving
  Ctrl+Shift+F summons a visible window.
- `build/package.ps1` — reproducible release packaging (build → test →
  publish → assemble installer → checksum → source zip).
- `Directory.Build.props` — centralised version, authors and repository
  metadata.

### Fixed
- The "Hide hidden and system files" setting was dead (never passed to the
  walker); it is now honoured at index time.
- Version bumped to 1.0.2 everywhere (assembly, installer manifest,
  registry DisplayVersion, settings version line) so builds are tellable.
- `QueryHint` extension suggestions now share `ExtensionScan` (last-dot:
  `archive.tar.gz` suggests `gz`, with counts matching Settings).
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
