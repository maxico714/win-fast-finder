// Prerequisite gate: .NET 8 Desktop Runtime must be installed by the user.
// Pure version logic is unit-tested; registry probing is best-effort.
using Microsoft.Win32;

namespace Wff.Engine;

public static class RuntimeCheck
{
    public const string DownloadUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/8.0";

    public static bool Satisfies(IEnumerable<string> versions)
    {
        foreach (var v in versions)
        {
            var num = v.Split('-')[0].Split('.');
            if (num.Length > 0 && int.TryParse(num[0], out int major) && major >= 8)
                return true;
        }
        return false;
    }

    public static bool IsDotNet8DesktopPresent()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");
            if (key != null)
            {
                var found = new List<string>();
                foreach (var name in key.GetValueNames())
                {
                    var val = key.GetValue(name)?.ToString();
                    if (val != null) found.Add(val);
                }
                // Value names ARE the versions; values are install paths.
                foreach (var name in key.GetValueNames())
                    found.Add(name);
                if (Satisfies(found))
                    return true;
            }
        }
        catch { }
        return DotnetCliHasDesktop();
    }

    private static bool DotnetCliHasDesktop()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return false;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            foreach (var line in output.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("Microsoft.WindowsDesktop.App 8.", StringComparison.Ordinal))
                    return true;
                if (t.StartsWith("Microsoft.WindowsDesktop.App 9.", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
        catch { return false; }
    }
}

// Install-time compatibility: OS + runtime, pure logic, unit-tested.
// The wizard disables Install while any issue is present.
public static class Compatibility
{
    private static readonly Version MinWindows = new(10, 0, 14393); // Win10 1607

    public static bool IsOsSupported(Version version, bool is64Bit) =>
        is64Bit && version >= MinWindows;

    public static (bool Ok, string Message) CheckOs()
    {
        var v = Environment.OSVersion.Version;
        bool x64 = Environment.Is64BitOperatingSystem;
        if (IsOsSupported(v, x64))
            return (true, "");
        return (false,
            $"Windows 10 version 1607 or later (64-bit) is required; " +
            $"this PC reports Windows {v} {(x64 ? "64-bit" : "32-bit")}.");
    }

    public static List<string> CheckAll()
    {
        var issues = new List<string>();
        var (ok, msg) = CheckOs();
        if (!ok)
            issues.Add(msg);
        if (!RuntimeCheck.IsDotNet8DesktopPresent())
            issues.Add("Microsoft .NET 8 Desktop Runtime is missing. " +
                "Install it, then press Recheck.");
        return issues;
    }

    // One-line "everything is fine" summary, shown even when compatible
    // so the check is never invisible.
    public static string ReadySummary()
    {
        var v = Environment.OSVersion.Version;
        string bits = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
        return $"Ready: Windows {v} {bits} · .NET 8 Desktop Runtime found.";
    }
}
