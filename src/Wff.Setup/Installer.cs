// Install core: validation (unit-tested), payload read, install/uninstall.
// The wizard (WPF) and silent flags both funnel through here.
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32;

namespace Wff.Setup;

public static class Installer
{
    public const string AppName = "win-fast-finder";
    public const string Version = "1.0.0";
    public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\win-fast-finder";
    public const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValue = "win-fast-finder";

    public static string DefaultDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);

    // Returns error text, or null when the directory is acceptable.
    public static string? ValidateDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return "Choose a folder.";
        // Relative paths would land under System32 when elevated: refuse.
        if (!Path.IsPathRooted(dir.Trim()))
            return "Use a full path like C:\\Program Files\\win-fast-finder.";
        string full;
        try { full = Path.GetFullPath(dir); }
        catch { return "That is not a valid path."; }
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (full.Equals(win, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase))
            return "Cannot install inside the Windows folder.";
        if (full.Length > 200)
            return "Path is too long (200 characters max).";
        return null;
    }

    public static void InstallTo(string dir, byte[] payload, Action<string>? log = null)
    {
        Directory.CreateDirectory(dir);
        log?.Invoke("Extracting files…");
        using (var zip = new ZipArchive(new MemoryStream(payload), ZipArchiveMode.Read))
            zip.ExtractToDirectory(dir, overwriteFiles: true);
        // This same binary becomes the uninstaller.
        File.Copy(Environment.ProcessPath!, Path.Combine(dir, "Uninstall.exe"), overwrite: true);
        // Start Menu shortcut.
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic lnk = shell.CreateShortcut(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            AppName + ".lnk"));
        lnk.TargetPath = Path.Combine(dir, "Wff.App.exe");
        lnk.WorkingDirectory = dir;
        lnk.Description = "win-fast-finder: instant file search (Ctrl+Shift+F)";
        lnk.Save();
        log?.Invoke("Registering…");
        using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            key?.SetValue("DisplayName", AppName);
            key?.SetValue("DisplayVersion", Version);
            key?.SetValue("Publisher", "win-fast-finder contributors");
            key?.SetValue("UninstallString", $"\"{Path.Combine(dir, "Uninstall.exe")}\" --uninstall");
            key?.SetValue("NoModify", 1);
            key?.SetValue("NoRepair", 1);
        }
        // Autostart (opt-out lives in app Settings).
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            key?.SetValue(RunValue, $"\"{Path.Combine(dir, "Wff.App.exe")}\" --tray");
        log?.Invoke("Done.");
    }

    public static void UninstallAll()
    {
        string ownDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
        foreach (var p in Process.GetProcessesByName("Wff.App"))
        {
            try { p.Kill(); } catch { }
        }
        Thread.Sleep(1000);
        DeleteDir(ownDir);
        try { File.Delete(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName + ".lnk")); }
        catch { }
        DeleteDir(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName));
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(RunValue) != null)
                run.DeleteValue(RunValue);
        }
        catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false); }
        catch { }
        SelfDelete(ownDir);
    }

    private static void DeleteDir(string dir)
    {
        for (int i = 0; i < 5 && Directory.Exists(dir); i++)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { Thread.Sleep(500); }
        }
    }

    private static void SelfDelete(string dir)
    {
        var me = Environment.ProcessPath!;
        var cmd = $"/c timeout /t 2 /nobreak >nul & del \"{me}\" & rmdir \"{dir}\" 2>nul";
        Process.Start(new ProcessStartInfo("cmd.exe", cmd)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    public static byte[] ReadPayload(string self)
    {
        using var fs = new FileStream(self, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Length < 8)
            throw new InvalidDataException("no payload appended (dev build?)");
        fs.Seek(-8, SeekOrigin.End);
        var lenBytes = new byte[8];
        fs.ReadExactly(lenBytes, 0, 8);
        long len = BitConverter.ToInt64(lenBytes, 0);
        if (len <= 0 || len > fs.Length - 8)
            throw new InvalidDataException("corrupt payload footer");
        fs.Seek(-8 - len, SeekOrigin.End);
        var data = new byte[len];
        fs.ReadExactly(data, 0, (int)len);
        return data;
    }

    public static string LicenseText()
    {
        try
        {
            var asm = typeof(Installer).Assembly;
            using var s = asm.GetManifestResourceStream("License.txt");
            if (s == null)
                return "MIT License — see LICENSE.txt after install.";
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
        catch
        {
            return "MIT License — see LICENSE.txt after install.";
        }
    }
}
