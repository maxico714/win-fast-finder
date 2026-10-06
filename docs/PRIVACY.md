# PRIVACY — win-fast-finder

**Short version: this application has no network code. It cannot phone home,
because there is no code that phones anywhere.**

## Verification, not assertion

Anyone can check this themselves. The project contains no use of:

- `System.Net.Http.HttpClient`
- `System.Net.WebClient` / `HttpWebRequest`
- `System.Net.Sockets.TcpClient` / `Socket` / `UdpClient`
- `Dns.GetHostEntry` and friends
- Any package reference (there are none at runtime)

Run it yourself against a fresh clone:

```powershell
Select-String -Path (Get-ChildItem src -Recurse -Include *.cs) `
  -Pattern 'HttpClient|WebRequest|TcpClient|UdpClient|Socket\(|Dns\.'
```

Expected output: **nothing**.

## What touches your disk

| Location | What | Why |
|---|---|---|
| `%APPDATA%\win-fast-finder\settings.json` | Your configuration | Persisted between runs |
| `%APPDATA%\win-fast-finder\cache.bin` | Filename index cache | Makes warm starts instant instead of re-walking |
| `%APPDATA%\win-fast-finder\cache.mtimes` | Incremental-refresh snapshot | Determines which folders need re-walking |
| `%APPDATA%\win-fast-finder\log.txt` | Timestamped diagnostics | So "nothing happens" is always answerable |

**Deleting `%APPDATA%\win-fast-finder\` removes every trace of the
application from your user profile.**

## What it reads

The indexer enumerates **filenames and paths only** — never file contents.
It skips:

- Reparse points (junctions and symlinks are never followed)
- Blacklisted directory names — `AppData`, `node_modules`, `.git`, `obj`,
  `bin`, `.vs` by default, plus any folders you add
- Hidden and system files (toggleable in Settings → Search)
- Ignored drives and extensions

The log records which roots were indexed and how many files they contain.

## What it writes to

Nothing outside `%APPDATA%\win-fast-finder\` and, if you installed it,
`C:\Program Files\win-fast-finder\` (the binaries) plus a Start Menu shortcut.
Launch-at-login, if you enable it, adds a single `HKCU\…\Run` registry value
named for this app — removable by unticking the setting or deleting the entry.

The application **never** writes to `HKEY_LOCAL_MACHINE`, never installs a
service, and never loads a kernel driver.

## Keyboard handling

Two input mechanisms exist, and their limits are deliberate:

| Mechanism | What it sees |
|---|---|
| `RegisterHotKey` (main hotkey) | Only the specific chord you configured. No key history, no other keys. |
| `WH_KEYBOARD_LL` (Ctrl double-tap, optional) | **Ctrl press timestamps only.** The hook callback discards every other key event immediately and never stores key codes, sequences, or text. |

There is deliberately **no way to configure a text-capture hook**, and no
code path that could turn the double-tap feature into a keylogger. This is
also why the feature is optional and off by default is a reasonable
preference — some antivirus heuristics flag low-level hooks on principle.

Note the standard Windows limit: a low-level hook cannot observe keystrokes
destined for an elevated window.

## Telemetry and analytics

None. There is no analytics SDK, no crash reporter that transmits, no
automatic update check, and no licensing call-home. Updates are a manual
affair: you download a new release when you choose to.

## Third-party dependencies

At runtime: **the .NET base class library only.** No NuGet packages ship with
the application. Development-only test packages (xunit, test SDK, coverlet)
are not part of the installed product. See
[THIRD_PARTY_NOTICES](../THIRD_PARTY_NOTICES).

The .NET runtime itself is a prerequisite installed by *you* from Microsoft;
this project neither bundles nor modifies it.

## Opening files

Selecting a result hands the path to the Windows shell for association-based
opening. The application never executes, interprets, or previews the file.

## Reporting a privacy concern

If you believe you have found a case where this application transmits data,
touches a location listed above, or reads more than filenames, please report
it as a security issue — see [SECURITY.md](../SECURITY.md).
