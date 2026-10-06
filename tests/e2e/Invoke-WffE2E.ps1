<#
.SYNOPSIS
  Synthetic-keypress E2E proof for win-fast-finder.

.DESCRIPTION
  Kills any running instance, launches the real app, waits for the hotkey to
  register (reading ONLY log bytes written by this run), sends a real
  Ctrl+Shift+F via SendInput, then asserts a visible window titled
  'win-fast-finder' exists.

  This is the harness that proved the v1.0.1 startup fix. A hidden window
  that never got an HWND could not register a hotkey; the fix shows the
  window once off-screen so it can own an HWND before RegisterHotKey.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\tests\e2e\Invoke-WffE2E.ps1

.EXAMPLE
  .\tests\e2e\Invoke-WffE2E.ps1 -AppExe C:\path\to\Wff.App.exe
#>
[CmdletBinding()]
param(
    [string]$AppExe = (Join-Path $PSScriptRoot '..\..\dist\win-fx\Wff.App.exe'),
    [string]$LogPath = (Join-Path $env:APPDATA 'win-fast-finder\log.txt'),
    [int]$RegistrationTimeoutSec = 90,
    [int]$SummonTimeoutSec = 15
)

$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class WffE2E
{
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int L; public int T; public int Rr; public int B; }

    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Cb c, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);

    public delegate bool Cb(IntPtr h, IntPtr l);
    public static uint WantPid;
    public static IntPtr Found;

    public static bool Each(IntPtr h, IntPtr l)
    {
        uint p = 0;
        GetWindowThreadProcessId(h, out p);
        if (p == WantPid && IsWindowVisible(h))
        {
            var sb = new StringBuilder(128);
            GetWindowText(h, sb, 128);
            if (sb.ToString().IndexOf("win-fast-finder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Found = h;
                return false;
            }
        }
        return true;
    }

    public static IntPtr FindVisible(int pid)
    {
        WantPid = (uint)pid;
        Found = IntPtr.Zero;
        var cb = new Cb(Each);
        EnumWindows(cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        return Found;
    }

    public static string RectOf(IntPtr h)
    {
        RECT r;
        if (!GetWindowRect(h, out r)) return "GetWindowRect failed";
        return r.L + "," + r.T + "-" + r.Rr + "," + r.B;
    }

    public const uint UP = 0x0002;
    static void Key(ushort vk, bool up)
    {
        var i = new INPUT { type = 1 };
        i.u.ki.wVk = vk;
        if (up) i.u.ki.dwFlags = UP;
        if (SendInput(1, new INPUT[] { i }, 40) != 1)
            throw new Exception("SendInput failed for vk=" + vk);
    }
    public static void Combo(ushort a, ushort b, ushort c)
    { Key(a, false); Key(b, false); Key(c, false); Key(c, true); Key(b, true); Key(a, true); }
}
"@

function Read-FreshLog {
    param([long]$Offset)
    if (-not (Test-Path -LiteralPath $LogPath)) { return '' }
    $fs = [IO.File]::Open($LogPath, 'Open', 'Read', 'ReadWrite')
    try {
        $null = $fs.Seek($Offset, 'Begin')
        $sr = New-Object IO.StreamReader($fs)
        try { return $sr.ReadToEnd() } finally { $sr.Close() }
    } finally { $fs.Close() }
}

# --- 0. preconditions -------------------------------------------------------
if (-not (Test-Path -LiteralPath $AppExe)) {
    Write-Output "RESULT: FAIL (app not found: $AppExe)"
    exit 1
}

Write-Output "app: $AppExe"
Get-Process Wff.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
if (Get-Process Wff.App -ErrorAction SilentlyContinue) {
    Write-Output "RESULT: BLOCKED (zombie instance survived; end it elevated if needed)"
    exit 2
}

$logLen0 = 0
if (Test-Path -LiteralPath $LogPath) { $logLen0 = (Get-Item -LiteralPath $LogPath).Length }

# --- 1. launch --------------------------------------------------------------
Start-Process -FilePath $AppExe
$deadline = (Get-Date).AddSeconds($RegistrationTimeoutSec)
$reg = $null
$proc = $null
while ((Get-Date) -lt $deadline) {
    $proc = Get-Process Wff.App -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc) {
        $fresh = Read-FreshLog -Offset $logLen0
        $reg = $fresh -split "`r?`n" | Where-Object { $_ -like '*registered=True*' } | Select-Object -Last 1
        if ($reg) { break }
    }
    Start-Sleep -Milliseconds 400
}

if (-not $proc) {
    Write-Output "RESULT: FAIL (app never started)"
    exit 1
}
if (-not $reg) {
    Write-Output "RESULT: FAIL (no registered=True in log)"
    Write-Output "log tail:"
    Get-Content -LiteralPath $LogPath -Tail 10 -ErrorAction SilentlyContinue
    exit 1
}
Write-Output "LOG: $reg"

# --- 2. real keypress -------------------------------------------------------
try {
    [WffE2E]::Combo(0x11, 0x10, 0x46)   # Ctrl + Shift + F
    Write-Output "sent Ctrl+Shift+F"
} catch {
    Write-Output "RESULT: FAIL ($($_.Exception.Message))"
    exit 1
}

# --- 3. assert a visible window appeared -------------------------------------
$deadline = (Get-Date).AddSeconds($SummonTimeoutSec)
$hb = [IntPtr]::Zero
while ((Get-Date) -lt $deadline) {
    $hb = [WffE2E]::FindVisible($proc.Id)
    if ($hb -ne [IntPtr]::Zero) { break }
    Start-Sleep -Milliseconds 200
}

if ($hb -eq [IntPtr]::Zero) {
    Write-Output "RESULT: FAIL (window did not appear after hotkey)"
    Write-Output "log tail:"
    Get-Content -LiteralPath $LogPath -Tail 10 -ErrorAction SilentlyContinue
    exit 1
}
Write-Output "visible window: hwnd=$hb rect=$([WffE2E]::RectOf($hb))"

Get-Process Wff.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Output "RESULT: PASS"
exit 0
