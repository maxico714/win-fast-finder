// Windows autostart via HKCU\...\Run (no admin needed). Value name is a
// parameter so tests use an isolated TEST name, never production keys.
using Microsoft.Win32;

namespace Wff.Engine;

public static class Autostart
{
    public const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "win-fast-finder";

    public static bool IsEnabled(string valueName = ValueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(valueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled, string exePath, string valueName = ValueName)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null)
                return;
            if (enabled)
                key.SetValue(valueName, $"\"{exePath}\" --tray");
            else if (key.GetValue(valueName) != null)
                key.DeleteValue(valueName);
        }
        catch
        {
            // autostart is best-effort (policy may block it)
        }
    }

    public static string ExePath =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
}
