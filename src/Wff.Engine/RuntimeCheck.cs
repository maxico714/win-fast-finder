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
