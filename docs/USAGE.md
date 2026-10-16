---
title: Usage - Query Syntax and Shortcuts | Win Fast Finder
description: Complete reference for the Win Fast Finder popup, query syntax (ext:, size:, dm:, path:), keyboard shortcuts and settings.
permalink: /usage.html
---
# USAGE — win-fast-finder

Complete reference for the popup, query syntax, shortcuts and settings.
Everything here matches `src/Wff.Engine/Query.cs` and `src/Wff.App/MainWindow.xaml.cs`.

## Summoning the popup

| Trigger | Behaviour |
|---|---|
| Main hotkey — **Ctrl+Shift+F** by default | Shows the popup upper-centre of the work area, empty box, caret focused |
| **Ctrl** double-tap | Same (optional; toggle in Settings → Hotkeys) |
| First launch (no cache yet) | Shows the popup with live indexing progress |
| Click outside / **Esc** | Hides it |

The popup is **borderless** and collapses to just the search box until you
type. It follows your Windows theme (System / Light / Dark).

> If another topmost window covers the popup, `win-fast-finder` re-asserts
> `Topmost` on every summon, so it stays reachable.

## Keyboard

| Key | Action |
|---|---|
| Any text | Debounced 50 ms search, top `MaxResults` (default 50) shown |
| **Tab** (at end of text) | Accepts the highlighted suggestion, else completes directive prefixes: `ex`→`ext:`, `si`→`size:`, `dm`→`dm:`, `p`→`path:` |
| **↑ ↓** (suggestions open) | Move through the suggestion list |
| **Esc** (suggestions open) | Dismisses the list (popup stays) |
| **Enter** | Opens the selected file (falls back to the first result) — never accepts a suggestion |
| **Double-click** | Opens the row you clicked |
| **Esc** | Hides the popup |
| Arrow keys | Move the selection (standard listbox navigation) |

### Search-box highlighting and suggestions

Directives are highlighted **inside the search box** as you type:
directive names (`ext:`, `size:`, `dm:`, `path:`) glow **amber**, values and
globs glow **cyan**, free text stays normal. What you see is what the parser
sees — if it is not amber, it is not a directive.

Typing a directive prefix opens a suggestion list under the box:

- `ex` → `ext:`, `si` → `size:`, `dm` → `dm:`, `p` → `path:`, each with a
  one-line description.
- After a complete `ext:`, the list offers **extensions from your own
  index**, most frequent first (`ext:p` → `ext:pdf` with its file count).
  Other directives take free-form values, so no list is shown for them.
- **Tab** (or click) accepts; **↑↓** move; **Esc** dismisses; **Enter** always
  opens the selected file.

## Query syntax

Tokens are split on whitespace. Directive names are **case-insensitive**;
free text keeps its original case for display but matches case-insensitively.

### Free text

```
invoice
```

Matches by **subsequence**, not substring — `inv` matches `**inv**oice.pdf`,
`rn` matches `README` (as `r`…`n`). Matching letters are highlighted amber in
the results, and those highlights are exactly the characters the scorer used.

### Globs — `*` and `?`

```
2024-*.xlsx
report?.docx
```

Any token containing `*` or `?` is treated as a glob and matched against the
filename (or the full path when `path:` is active). Implemented as iterative
backtracking — no `Regex` engine, so pathological patterns stay fast.

### `ext:` — extension filter

```
ext:pdf
ext:pdf ext:docx          # repeatable = OR
ext:.PDF                  # leading dot optional
```

Checked **before** scoring and needs no filesystem access, so it is free.

### `size:` — file size

| Syntax | Meaning |
|---|---|
| `size:>10kb` | larger than 10 KB |
| `size:<2mb` | smaller than 2 MB |
| `size:500kb` | exactly 500 KB |

Units: `b`, `kb` (1024), `mb` (1024²), `gb` (1024³). Case-insensitive.
Stat calls are limited to the top-N candidates, so this never walks the disk.

### `dm:` — modified date

| Syntax | Meaning |
|---|---|
| `dm:<7d` | modified **within** the last 7 days |
| `dm:7d` | same as `<7d` |
| `dm:>30d` | modified **more than** 30 days ago |

Days only; computed against UTC.

### `path:` — match the full path

Two equivalent forms. Bare `path:` switches free-text matching from
filenames to full paths:

```
report-final path:
```

Or attach the value directly — `path:Downloads` means exactly the same as
`Downloads path:`:

```
path:Downloads
path:src\Wff.Engine
```

The value is plain text (no wildcards) and matches case-insensitively by
subsequence, like all free text. Multiple free-text terms each match
independently — `invoice 2024` finds `invoice-2024.pdf` regardless of word
order, because *every* term must appear *somewhere*.

### `sort:` — does not exist

There is **no `sort:` query token.** Sorting is a setting, not syntax:

- Settings → Search → **Sort:** Rank / Name / Date / Size

### Combining

```
rep* ext:pdf size:>10kb dm:<30d path:projects
```

All applicable filters must pass. With no text and only filters, every file
satisfying the filters is a candidate.

## Results

- **★** marks an exact filename match; those rank first.
- Matched characters glow **amber**.
- Each row shows the filename (highlighted) and its full path (dimmed).
- The status line shows live state: `starting.`, `checking for new files...`,
  or `N files in X ms`.
- The footer shows the total indexed file count.

## Settings

Open with the **Settings** button in the popup's bottom-right.

### General

| Setting | Notes |
|---|---|
| Theme | `System` follows Windows; `Light` / `Dark` override |
| Launch at login | Writes a `HKCU\…\Run` entry — **no admin needed**. Untick to opt out |
| Data folder | Where settings/cache/log live (`%APPDATA%\win-fast-finder` by default) |
| Log viewer | Opens `log.txt` — the first place to look when something misbehaves |
| Index stats | Indexed file count, cache age, root list |
| Export / Import / Reset | Back up or restore settings; Reset restores safe defaults |

### Hotkeys

| Setting | Notes |
|---|---|
| Main hotkey | e.g. `Ctrl+Shift+F`, `Alt+Space`. **A modifier is required** — bare keys would hijack typing. Applies on Save (re-registers) |
| Ctrl double-tap | Toggle the low-level-hook gesture |
| Ignore in fullscreen | Suppress the hotkey while a borderless fullscreen window is foreground |

### Search

| Setting | Notes |
|---|---|
| Max results | Rows rendered per query (default 50) |
| Match path | Default on/off for `path:` behaviour |
| Hide hidden/system files | Respect `FILE_ATTRIBUTE_HIDDEN` / `SYSTEM` |

### Index

| Setting | Notes |
|---|---|
| Roots to index | Empty = your user profile. Paste full paths |
| Disks to ignore | Tick boxes for fixed drives |
| Folders to ignore | **Validated full addresses** — rejects partial paths |
| Extensions to ignore | Tick boxes populated from extensions present in *your* index |
| Resync | Forces a full rebuild (normal restarts are incremental) |

Ignored drives are honoured by `ResolveRoots()`, so a ticked drive never gets
walked at all.

## Headless mode

For scripting and performance proof:

```powershell
dotnet run --project src/Wff.App -- --index "C:\Some\Folder"
dotnet run --project src\Wff.App -- --index "C:\Some\Folder" --query "report"
```

Prints `files=`, `build_ms=`, `query_ms=` and the top hits.

## Where things live

| Path | Contents |
|---|---|
| `%APPDATA%\win-fast-finder\settings.json` | All settings |
| `%APPDATA%\win-fast-finder\cache.bin` | Binary filename index |
| `%APPDATA%\win-fast-finder\cache.mtimes` | Incremental-refresh snapshot |
| `%APPDATA%\win-fast-finder\log.txt` | Timestamped startup / hotkey / index log |

Delete that folder to remove every trace. See [PRIVACY](PRIVACY.md).
