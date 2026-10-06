using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Wff.Setup;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        var args = e.Args;
        // Uninstaller mode: explicit flag (Apps entry) OR exe renamed to
        // Uninstall.exe (double-click). The latter confirms first, since a
        // double-click may be accidental and wiping is irreversible.
        bool namedUninstaller = (Path.GetFileNameWithoutExtension(
            Environment.ProcessPath ?? "") ?? "").StartsWith(
            "Uninstall", StringComparison.OrdinalIgnoreCase);
        if (args.Contains("--uninstall") || namedUninstaller)
        {
            try
            {
                if (!args.Contains("--uninstall"))
                {
                    var answer = System.Windows.MessageBox.Show(
                        "Remove win-fast-finder completely?\n\nThis wipes the app, its index, settings and log.",
                        "win-fast-finder uninstall",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);
                    if (answer != System.Windows.MessageBoxResult.Yes)
                    {
                        Shutdown(0);
                        return;
                    }
                }
                Installer.UninstallAll();
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "win-fast-finder uninstall"); }
            Shutdown(0);
            return;
        }
        var silentDir = SilentDir(args);
        if (silentDir != null)
        {
            // Headless/smoke installs + elevation handoff. No UI.
            try
            {
                var err = Installer.ValidateDirectory(silentDir);
                if (err != null)
                {
                    Console.Error.WriteLine("FAILED: " + err);
                    Shutdown(1);
                    return;
                }
                Installer.InstallTo(silentDir, Installer.ReadPayload(Environment.ProcessPath!),
                    m => Console.WriteLine(m));
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAILED: " + ex.Message);
                Shutdown(1);
            }
            return;
        }
        base.OnStartup(e);
    }

    private static string? SilentDir(string[] args)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--install-to")
                return args[i + 1];
        return null;
    }
}
