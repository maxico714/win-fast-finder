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
| **Tab** (at end of text) | Completes directive prefixes: `ex`→`ext:`, `si`→`size:`, `dm`→`dm:` |
| **Enter** | Opens the selected file (falls back to the first result) |
| **Double-click** | Opens the row you clicked |
| **Esc** | Hides the popup |
| Arrow keys | Move the selection (standard listbox navigation) |

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

```
path:Downloads
path:src\Wff.Engine
```

When present, free text is matched against the whole path instead of just the
filename.

> **`path:` must be the entire token.** `path:Downloads` is *not* parsed as a
> path directive — the parser accepts the bare token `path:` only, so
> `path:Downloads` falls through to ordinary free-text matching.

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
