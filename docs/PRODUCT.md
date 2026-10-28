# PRODUCT — win-fast-finder

## Problem

Finding a file on Windows is slower and less reliable than it should be.

- **Windows Search** typically takes 2–5 seconds and depends on a background
  indexer that can be disabled, damaged, or simply behind on large drives.
  (PowerToys Run reuses that same indexer — see
  [PowerToys#43864](https://github.com/microsoft/PowerToys/issues/43864).)
- **Everything** is genuinely fast, but it is closed-source and its
  file-content features sit behind a paid licence.
- **Listary** and similar tools are fast, closed-source, and paywalled for
  the capabilities power users actually want (filters, network paths).

No single tool offered: *one hotkey + fuzzy filename search + whitelist and
blacklist control + network-path awareness, fully offline, under an MIT
licence.*

## Users

1. **Developers on Windows** — hit a hotkey, open any project file in under
   a second, without waiting on an indexer.
2. **Writers and support staff** — locate documents and screenshots across
   several drives, including mounted shares.

## Value proposition

> One hotkey, instant offline filename search across all your drives — free,
> fast, and auditable.

## What v1.0.1 delivers

| Area | Delivered |
|---|---|
| Indexing | Parallel multi-threaded walker, binary cache, incremental refresh on every restart, manual Resync |
| Search | Subsequence fuzzy matching, `*`/`?` globs, `ext:`, `size:`, `dm:`, `path:` filters, exact-match boost with ★ ranking |
| Hotkeys | Configurable main hotkey via `RegisterHotKey` (default `Ctrl+Shift+F`), optional Ctrl double-tap hook, fullscreen guard |
| UI | Borderless upper-centre popup, System/Light/Dark themes, amber match highlighting, Tab directive completion, dismiss on Esc/outside-click |
| Settings | Roots, ignored drives/folders/extensions, max results, sort mode, hidden-file toggle, launch-at-login (HKCU, opt-in), export/import/reset, log viewer, index stats |
| Install | UAC wizard, runtime-compatibility gate, validated dedicated app directory, Start Menu entry, Apps entry, silent uninstall |
| Assurance | 239 mutation-verified tests, synthetic-keypress E2E, zero network code, adversarial input suite |

## What v1.0.3 delivers (stable)

Installer and release hardening on top of v1.0.2:

| Area | Delivered |
|---|---|
| Installer safety | Dedicated `win-fast-finder` leaf required; drive/share roots, protected system/personal folders and shell-metacharacter paths rejected |
| Uninstall safety | Recursive removal refused unless the folder is a real app directory (correct leaf, `Uninstall.exe` + `Wff.App.exe`, not a reparse point) |
| Autostart | Opt-in only (`HKCU\…\Run` written only when the checkbox or `--autostart` is used); existing choices untouched by default |
| Runtime | .NET 8-only gate rejects .NET 9+ runtimes; no `rollForward` |
| Release | Workflow and packaging fail before building when the tag does not match `Directory.Build.props` |

## What v1.0.2 delivered (stable)

Everything v1.0.1 promised, finished to the documented spec:

| Area | Delivered |
|---|---|
| Query grammar | Full `size:` (`>=` `<=` `=` decimals short-units ranges spaced-units) and `dm:` (hours/weeks/months/years, `today`, absolute days, ranges) grammars; `[AND]`/`[OR]` logic; `path:value`; invalid directives reported, never silent |
| Engine | Size + mtime in the index (cache v2); filters and sorts over the whole match set; true totals; per-keystroke cancellation |
| Search UX | In-box amber/cyan/red highlighting, autocomplete with index-backed values, per-row sizes, `N of M` totals, cheat-sheet window |
| Settings | Exact-size mode, unit base, date zone, totals/size toggles, filter delay, slow-query log, Quick/Deep refresh, results to 1000 |
| Install | OS + runtime compatibility gate (Install disabled until green), dynamic versions everywhere, single-file stub |

## Non-goals

These are deliberately out of scope:

- **File content search** — this is a *filename* index. Content search would
  need a different engine and a different privacy story.
- **Any network feature** — no cloud sync, no update ping, no telemetry.
- **Non-Windows platforms** — the speed comes from Windows-specific choices.
- **Admin-only acceleration** (NTFS MFT / USN journal reads) — parked until
  there is a clean, optional, separately-auditable path.
- **Paid tiers.** Everything here is MIT.

## Success criteria (v1.0.1)

| Criterion | Status |
|---|---|
| Popup opens on the hotkey | ✅ proven by synthetic-keypress E2E |
| Typing filters a large cache in milliseconds | ✅ ~6 ms live at 274 k files |
| Blacklist and ignore rules respected | ✅ `IgnoreOptionTests`, `SafeDefaultTests` |
| Zero third-party runtime dependencies | ✅ base class library only |
| Works without admin rights | ✅ |
| Hotkey survives restart, single instance enforced | ✅ fixed in 1.0.1 |

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Cold indexing is slower than kernel-driver tools (Everything) | Binary cache + incremental refresh make warm starts instant; documented honestly |
| Low-level keyboard hooks trip antivirus heuristics | Hook is optional and Ctrl-only; main hotkey uses `RegisterHotKey` and needs no hook |
| Unsigned installer draws SmartScreen/Defender warnings | Documented loudly in README and SECURITY rather than hidden; build-from-source path provided |
| Single-maintainer bus factor | Small, dependency-free codebase; complete architecture doc; tests encode the behaviour |

## Name and licensing

- **Name:** `win-fast-finder` — re-check GitHub/PyPI at publish time; the
  name is free as of this writing.
- **Licence:** MIT, copyright **Rehan Khan**.
