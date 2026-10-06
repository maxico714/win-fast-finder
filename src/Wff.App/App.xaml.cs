using System.Windows;

namespace Wff.App;

public partial class App : Application
{
    private static System.Threading.Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;
        // Headless E2E / perf proof: Wff.App --index <dir> [--query <q>]
        if (args.Length >= 2 && args[0] == "--index")
        {
            int rc = Headless.Run(args);
            Shutdown(rc);
            return;
        }
        // Single instance: two copies fight over the same global hotkeys
        // and neither answers reliably.
        _single = new System.Threading.Mutex(true, "win-fast-finder-single", out bool fresh);
        if (!fresh)
        {
            MessageBox.Show("win-fast-finder is already running.\nPress Ctrl+Shift+F to summon it.",
                "win-fast-finder", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }
        base.OnStartup(e);
        // GUI mode: drive startup explicitly. Do NOT rely on Loaded firing
        // (a Hidden window may never raise it) nor on MainWindow being set.
        try
        {
            if (MainWindow is not MainWindow w)
            {
                w = new MainWindow();
                MainWindow = w;
            }
            w.EnsureReady();
        }
        catch (Exception ex)
        {
            Wff.Engine.Logger.Info("guistart failed: " + ex.Message);
        }
    }
}
