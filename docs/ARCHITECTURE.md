---
title: Architecture - How Win Fast Finder Works
description: How the shipped Win Fast Finder code is put together - file index, query engine, caching, privacy model and test ladder.
permalink: /architecture.html
---
# ARCHITECTURE — win-fast-finder v1.0.2

How the shipped code is put together. Everything here describes what is
actually in `src/`; see [BUILDING](BUILDING.md) to run it and
[USAGE](USAGE.md) to drive it.

## Layout

```mermaid
flowchart LR
  subgraph Wff.Engine["Wff.Engine (class library, no UI)"]
    W[Walker<br/>parallel walk] --> S[SnapshotStore<br/>incremental diff]
    W --> C[Cache<br/>binary save/load]
    W --> F[FileIndex<br/>bigram buckets]
    Q[QueryParser<br/>directives, grammar, hints] --> F
    F --> R[Sorter<br/>rank/name/date/size]
  end
  subgraph Wff.App["Wff.App (WPF + headless CLI)"]
    HK[RegisterHotKey<br/>Ctrl+Shift+F] --> SUM[MainWindow.Summon]
    LL[KeyboardHook<br/>WH_KEYBOARD_LL Ctrl-only] --> SUM
    SUM --> DB[50ms debounce<br/>250ms for directives] --> F
    FS[Fullscreen guard] --> SUM
    TH[Theme System/Light/Dark] --> UI[Borderless popup]
  end
  subgraph Wff.Setup["Wff.Setup (installer)"]
    Z[WizardWindow] --> I[Installer core]
    I --> R2[Registry / shortcuts / files]
  end
  F --> UI
  UI --> OP[Enter → os start of file]
```

Three projects, one solution:

| Project | Kind | Responsibility |
|---|---|---|
| `src/Wff.Engine` | Class library | Index, search, walk, cache, settings, parsing. **No UI, no WPF** — everything unit-testable. |
| `src/Wff.App` | WPF `WinExe` + headless CLI | Popup, hotkeys, theme, settings window, `--index`/`--query` mode. |
| `src/Wff.Setup` | WPF `WinExe` (UAC) | Install/uninstall wizard and silent flags; funnels through `Installer`. |
| `tests/Wff.Tests` | xunit | 239 tests over Engine + App + Setup. |

## Engine internals

**`Walker`** — parallel and sequential directory walks. Prunes blacklisted
names *before* descending, refuses to follow junctions/symlinks (reparse
points), and shuts down cleanly mid-walk. The parallel walker is asserted to
match the sequential one byte-for-byte in `ParallelWalkerTests`.

**`FileIndex`** — the speed core. Zero dependencies, purely in-memory:
1. `Build` lowercases each filename and emits every adjacent character pair
   (bigram) into a bucket keyed by that pair.
2. `Search` picks candidates by unioning per-term bigram narrowings (so
   multi-term queries keep recall), applies cheap filters first —
   extension matching needs **no syscalls** — then in-memory size/date
   filters over **all** survivors (sizes and mtimes live in the index
   since v1.0.2; no examined-cap, no scored-cap). Free-text terms form
   `[OR]` groups (AND inside, best group wins); directives stay global.
   Ranks the whole match set and returns top-N rows plus the true total.

It never stats per keystroke. `MatchSpans` re-walks the *same*
greedy scoring path, so highlighted characters are exactly the scored ones.
One-off cost moved to build time: one stat per file when the index is
built (missing files get unknown markers and fail active size/date
filters, like a stat failure used to).

**`QueryParser`** — whitespace tokens, case-insensitive, options-driven
(`QueryOptions`: exact-size mode, unit base, date zone):
- free text → subsequence score, ANDed in any order
- `[OR]` splits alternatives (best group wins); `[AND]` is explicit AND
- `ext:pdf` (repeatable → OR), `.pdf` accepted too, no wildcards in values
- `size:` — `>` `<` `>=` `<=` `=` bare; decimals; `b k kb m mb g gb t tb`;
  ranges (`1mb..5mb`); spaced units (`size:>1 gb`); bare value follows the
  Exact-size setting (Displayed/ExactBytes/±1%)
- `dm:` — relative (`h d w m y`, bare digits = days), `today`/`yesterday`,
  absolute ISO days (whole local day), ranges, full operators
- any token containing `*` or `?` → `Glob` (iterative backtracking matcher,
  no `Regex` engine)
- bare `path:` → full-path mode; `path:value` ≡ value + full-path mode
- a directive-looking token that does not parse lands in `Query.Invalid`
  with a hint and never becomes filename text (`:` is illegal in Windows
  filenames, so it could never match)

Two documented sharp edges, by design: **there is no `sort:` token** —
sorting is a setting (`Settings.SortMode`), not query syntax — and globs
are global filters like directives (they don't join `[OR]` groups).

**`Cache`** — v2 binary blob: magic + version + count + (UTF-8 path, size,
mtime) per entry, buffered 64 KB sequential IO. Loading validates magic
and version and throws on mismatch/corruption rather than serving
garbage; a v1 cache triggers one automatic full rebuild, then re-saves
as v2.

**`SnapshotStore` (`Incremental`)** — persists a per-root snapshot of
top-level child mtimes. On restart only children whose mtime changed are
re-walked; untouched subtrees keep their cached entries. `Resync` in Settings
forces a full rebuild.

**`Settings`** — JSON in `%APPDATA%\win-fast-finder\`, BCL only. Holds roots,
ignored drives/folders/extensions, hotkey strings, max results (to 1000),
sort mode, theme, launch-at-login, hidden/system toggle, exact-size mode,
unit base, date zone, totals/size-badge toggles, filter delay, slow-query
log, refresh mode. Corrupt files fall back to
safe defaults instead of crashing (see `ProdSettingsTests`).

## App internals

**Startup** — `App.OnStartup` creates a single-instance mutex
(`win-fast-finder-single`); a second launch shows a message and exits. It
then calls `MainWindow.EnsureReady()` directly rather than waiting for
`Loaded`, because a never-shown `Hidden` window may never raise it. All
startup failures land in the log file.

**Hotkeys**
| Trigger | Mechanism | Notes |
|---|---|---|
| Main hotkey (default `Ctrl+Shift+F`, user-configurable) | `RegisterHotKey` P/Invoke | No hook, no permissions, works from lock screen state changes. A modifier is required (`Hotkey` refuses bare keys). |
| `Ctrl` double-tap | `WH_KEYBOARD_LL` hook → `DoubleTapDetector` | The hook forwards **Ctrl press timestamps only**; it never records keystrokes, so there is no keylogging surface. Heuristic AV/SmartScreen hits are an accepted, documented cost — the app degrades to the main hotkey. |

**Summon path** — `Summon()` places the borderless window upper-centre of the
work area, `Show()`/`Activate()`/`Topmost = true`, clears the box and moves
caret focus via `Dispatcher` at `ApplicationIdle`. It is idempotent and
logged (`wm_hotkey received` → `summon enter` → post-show state) so "nothing
happens" is always diagnosable from `log.txt`.

**Fullscreen guard** — `Fullscreen.IsForegroundFullscreen()` returns true only
when the foreground window covers the whole work area **and** has no caption
(`WS_CAPTION` bit clear). A maximised Explorer/VS window is therefore *not*
treated as fullscreen; a borderless game or video is.

**Search UX** — `DispatcherTimer` debounces 50 ms for plain text (longer for
directive queries, Settings → Search), results carry a query id plus a
per-search `CancellationTokenSource` so stale responses are dropped and
stale scans die. Only the top `MaxResults` (default 50) render, as
`ResultRow`s carrying index metadata (sizes display with no syscalls);
the status optionally shows `N of M` totals or `⚠` invalid-directive
reasons. `Deactivated` hides the popup unless a modal dialog owns focus.
`Esc` hides, `Enter` opens the selection (`OpenSelected`), double-click
opens too. `Tab` accepts autocomplete suggestions (`[AND]`/`[OR]`,
directives, index-backed `ext:` values); in-box overlay highlights
directives amber, values/globs cyan, broken directives red.

**Headless** — `Wff.App --index <dir> [--query <q>]` walks, times, prints
`files=`, `query_ms=`, `hits=`, `total=` and top hits, plus `WARN` lines
for invalid directives. This is what the perf numbers below come
from and what CI-style proof uses.

## Speed contract

Budgets, measured on an i5 Windows host against a 700 k-file target:

| Path | Budget | Measured |
|---|---|---|
| Query over 100 k cached names | worst < 1000 ms | typical single-digit ms; 5 142-file tree → `query_ms=4` |
| Live profile (~304 k files) | — | first index ~37 s, queries ~6 ms |
| Incremental refresh on restart | < 5 s | ~8 s at 297 k files |
| Popup appear on hotkey | < 100 ms | instant (no index work on summon) |

Reproduce any of these with headless mode:

```powershell
dotnet run --project src/Wff.App -- --index "C:\Some\Big\Folder" --query "report"
```

Because it is a **filename** index (never file contents), the query path
allocates little and touches no syscalls during scoring.

## Privacy & threat model

- **No network code at all** — `HttpClient`, `WebRequest`, `TcpClient`,
  raw sockets: zero hits in `src/`. No telemetry, no update pings.
- **No admin required** at runtime. The installer elevates once (default
  `C:\Program Files`); the app itself runs as the user, reads no MFT,
  holds no elevated handles.
- **Blacklist skips sensitive trees** (`AppData`, `node_modules`, `.git`,
  `obj`, `bin`, `.vs` by default) and is user-extensible.
- **Keyboard hook is Ctrl-only** — see above.
- **Opens files via the shell** association only; the app never executes
  anything it indexes.
- Settings, cache and log live in `%APPDATA%\win-fast-finder\` — deleting
  that folder removes every trace. See [PRIVACY](PRIVACY.md).

## Testing ladder

| Level | What | Where |
|---|---|---|
| L0 | Compiles with 0 warnings / 0 errors | `dotnet build Wff.sln --no-incremental` |
| L1 | Unit tests, mutation-verified (red → green → flip the code → must fail) | `tests/Wff.Tests/*.cs` (239 tests) |
| L2 | Real filesystem temp dirs, real walkers, real cache round-trip | `WalkerTests`, `CacheTests`, `IncrementalTests` |
| L3 | Journeys: headless index+query, installer validation, wizard rules | `StartupTests`, `WizardValidationTests` |
| L4 | Fresh clone, README commands run literally | see [BUILDING](BUILDING.md) |
| L5 | Adversarial: 10 k-char paths, unicode, null bytes, corrupt cache/settings | `AdversarialTests`, `ProdSettingsTests` |
| E2E | Synthetic `SendInput` Ctrl+Shift+F → visible window asserted | `tests/e2e/Invoke-WffE2E.ps1` |

Key suites: `FileIndex`/ranking/scoring (`IndexTests`, `FilterTests`,
`HighlightTests`, `PathSortTests`, `QuerySemanticsTests`,
`QueryBehaviorTests`, `QueryGrammarTests`, `OperatorTests`, `ScaleTests`),
hotkeys (`HotkeyParseTests`, `DoubleTapTests`, `DoubleTapHoldTests`),
ignore rules (`IgnoreOptionTests`, `SafeDefaultTests`, `HiddenTests`,
`CoverageTests`), perf (`PerfTests`), setup/installer
(`WizardValidationTests`, `RuntimeCheckTests`, `CompatibilityTests`),
persistence (`CacheTests`, `SnapshotPersistTests`, `SettingsTests`).

## Known limitations

1. **Installer is not code-signed** — SmartScreen/Defender may warn on
   first run. Documented, not hidden; see [SECURITY](../SECURITY.md).
2. **Cold walk is slower than Everything** — Everything uses a kernel
   driver and NTFS MFT reads; we stay non-admin and portable. The binary
   cache + incremental refresh make warm starts instant.
3. **Elevated-window hook limits (UIPI)** — a `WH_KEYBOARD_LL` hook cannot
   see keystrokes destined for an elevated window. `Ctrl+Shift+F`
   (`RegisterHotKey`) is unaffected.
4. **Single-machine tested** on Windows 10/11 x64 with the .NET 8 Desktop
   Runtime.
