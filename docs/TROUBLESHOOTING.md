# TROUBLESHOOTING â€” win-fast-finder

The single most useful thing you can do: **read the log.**

```
%APPDATA%\win-fast-finder\log.txt
```

Open it in Notepad, or use **Settings â†’ General â†’ Log viewer**. Every
startup path writes there, so "nothing happens" always leaves a trace.

## Reading the log

A healthy startup looks like this:

```
2026-10-06 19:39:03 app started
2026-10-06 19:39:03 hotkey Ctrl+Shift+F registered=True err=0
2026-10-06 19:39:03 startup roots=[C:\Users\you] blacklist=[AppData,node_modules,.git,obj,bin,.vs] forceFull=False
2026-10-06 19:39:21 refreshed 274664 files in 17339 ms
```

When you press the hotkey you should then see:

```
wm_hotkey received
summon enter
summon pre-show title='win-fast-finder' visible=False L=343 T=158.4
summon post-show visible=True handle=1181936
```

Map symptoms to lines:

| You see | It means |
|---|---|
| `app started` | Constructor and settings load succeeded |
| `hotkey ... registered=True err=0` | `RegisterHotKey` worked â€” the OS owns the shortcut for us |
| `hotkey ... registered=False err=1409` | **Hotkey already owned** by another app (or another copy) |
| `startup failed: ...` | Startup threw â€” the full exception (type, message, stack) follows |
| `wm_hotkey received` | Windows delivered the hotkey to us |
| `summon enter` / `summon post-show visible=True` | The popup actually appeared |
| `refreshed N files in X ms` | Index refresh finished |
| nothing after `app started` | Startup is stuck â€” scroll up for `startup failed` |

## Symptom: pressing the hotkey does nothing

Work down this list; each step is one line of evidence.

### 1. Is the app running?

Task Manager â†’ find `Wff.App.exe`. If it isn't there, launch it.

### 2. Did the hotkey register?

Check for `registered=True` in the log.

**`registered=False err=1409`** means something else owns `Ctrl+Shift+F`:
another launcher, an IDE, or â€” most often â€” **an older copy of
win-fast-finder that is stuck**. End every `Wff.App.exe` in Task Manager,
then relaunch.

> A copy running at a **higher integrity level** (elevated) cannot be ended
> from a normal Task Manager session. In that case open an elevated
> PowerShell and run:
> ```powershell
> taskkill /IM Wff.App.exe /F
> ```

### 3. Does the log show `wm_hotkey received`?

- **No** â†’ the OS never delivered it. Something else holds the shortcut, or
  the hook was blocked. Try changing the hotkey in Settings â†’ Hotkeys to
  something unusual (e.g. `Ctrl+Alt+Shift+F`).
- **Yes, but no `summon post-show visible=True`** â†’ see
  [Summons but you can't see it](#symptom-the-window-summons-but-you-cant-see-it).

### 4. Is a fullscreen window in front?

The fullscreen guard suppresses the hotkey while a **borderless** fullscreen
window (game, video player in fullscreen) owns the screen. A maximised
window is *not* fullscreen â€” that was fixed in v1.0.1. If you are in a game,
exit fullscreen or untick **Ignore in fullscreen** in Settings â†’ Hotkeys.

### 5. Is the hook being blocked?

If `Ctrl+Shift+F` works but Ctrl double-tap does not, your antivirus or
SmartScreen heuristics are almost certainly blocking the low-level keyboard
hook. This is a **known and accepted** cost of that feature â€” the hook
forwards only Ctrl press timestamps and never records keystrokes, but
heuristics cannot tell the difference. The main hotkey is unaffected.

## Symptom: the window summons but you can't see it

The log is decisive here.

- `summon post-show visible=True` but nothing on screen â†’ another window is
  covering it. Click the taskbar entry, or press the hotkey again.
- `summon skipped: fullscreen foreground` â†’ the guard fired; see step 4 above.
- `summon enter` with no `summon post-show` line â†’ an exception was thrown
  during summon. It is logged as `summon failed: ...`.

## Symptom: search returns nothing

1. Press **Resync** (Settings â†’ Index). The first run indexes your user
   profile only â€” that is fast but limited.
2. Add more roots in **Settings â†’ Index â†’ Roots to index**.
3. Check the status line. `starting.` means indexing is still running; results
   appear as it progresses.
4. Check that your search terms aren't being filtered out â€” `ext:` and
   `size:` are ANDed with free text.
5. Look for the `refreshed N files` line in the log. If `N` is suspiciously
   small, a folder you expect may be blacklisted (Settings â†’ Index).

## Symptom: installer says the runtime is missing

Install the **.NET 8 Desktop Runtime**, x64 â€” not the ASP.NET runtime, not
the SDK-only package:

https://dotnet.microsoft.com/en-us/download/dotnet/8.0

Pick **.NET Desktop Runtime 8.x â†’ x64**. Re-run the installer.

## Symptom: SmartScreen or Defender warns

The installer is **not code-signed**. Windows will show *"Windows protected
your PC"* on first run.

**More info â†’ Run anyway**.

This is documented openly in [SECURITY.md](../SECURITY.md) rather than buried.
If you would rather not trust an unsigned binary, build from source instead â€”
see [BUILDING.md](BUILDING.md) â€” the source is the ground truth.

## Symptom: "win-fast-finder is already running"

The single-instance mutex found a live copy. End `Wff.App.exe` in Task
Manager (or `taskkill /IM Wff.App.exe /F` from an elevated prompt) and
relaunch.

## Symptom: "Hwnd of zero is not valid" in the log

This was a v1.0 bug, fixed in v1.0.1: a never-shown hidden window could not
own a window handle, so hotkey registration failed at startup. If you see
this line, you are running an old build â€” upgrade.

## Symptom: files open in the wrong application

`win-fast-finder` opens paths through the shell association only. Change the
default handler for that extension in Windows Settings â†’ Apps â†’ Default apps.

## Reporting a bug

Open an issue with:

1. Windows version (`winver`)
2. Whether you installed the release or built from source
3. The **complete** `log.txt` from `%APPDATA%\win-fast-finder\`
4. What you expected, and what happened instead

If the app cannot start at all, attach `%TEMP%` crash information if present.

See [SECURITY.md](../SECURITY.md) if you believe you have found a vulnerability
rather than a bug.
