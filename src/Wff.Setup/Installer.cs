// Install core: validation (unit-tested), payload read, install/uninstall.
// The wizard (WPF) and silent flags both funnel through here.
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace Wff.Setup;

public static class Installer
{
    public const string AppName = "win-fast-finder";
    // Fallback only: the real version always comes from the assembly
    // (Directory.Build.props), so screens and registry never go stale.
    public const string Version = "1.0.3";

    public static string AppVersion =>
        typeof(Installer).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? Version;
    public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\win-fast-finder";
    public const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValue = "win-fast-finder";

    public static string DefaultDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);

    // Returns error text, or null when the directory is acceptable.
    // InstallTo and UninstallAll both assume this contract: the leaf must be
    // AppName, outside protected locations, and free of cmd.exe metacharacters
    // (the path is later embedded in SelfDelete's `cmd /c` line).
    public static string? ValidateDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return "Choose a folder.";
        // Relative paths would land under System32 when elevated: refuse.
        if (!Path.IsPathRooted(dir.Trim()))
            return "Use a full path like C:\\Program Files\\win-fast-finder.";
        string full;
        try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir.Trim())); }
        catch { return "That is not a valid path."; }
        if (full.Length > 200)
            return "Path is too long (200 characters max).";
        if (HasUnsafeShellChars(full))
            return "Path contains characters the installer cannot handle safely " +
                "(& | < > ^ % \" ! ( ) * ?).";
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (full.Equals(win, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase))
            return "Cannot install inside the Windows folder.";
        // A drive root (or UNC share root) is never a dedicated app folder.
        string root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? "");
        if (root.Length > 0 && full.Equals(root, StringComparison.OrdinalIgnoreCase))
            return "Cannot install to a drive or share root. Use a dedicated app folder.";
        foreach (var protectedPath in ProtectedPaths())
        {
            if (string.IsNullOrWhiteSpace(protectedPath))
                continue;
            string p;
            try { p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedPath)); }
            catch { continue; }
            if (p.Length > 0 && full.Equals(p, StringComparison.OrdinalIgnoreCase))
                return "Choose a dedicated app folder, not a system or personal folder.";
        }
        // UninstallAll recursively deletes this directory: require an app-owned leaf.
        string leaf = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        if (!leaf.Equals(AppName, StringComparison.OrdinalIgnoreCase))
            return $"The install folder must be named \"{AppName}\", " +
                $"for example C:\\Program Files\\{AppName}.";
        return null;
    }

    private static IEnumerable<string> ProtectedPaths()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.System);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return profile;
        if (profile.Length > 0)
        {
            string? users = Path.GetDirectoryName(profile);
            if (!string.IsNullOrEmpty(users))
                yield return users;
            yield return Path.Combine(profile, "Downloads");
        }
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static bool HasUnsafeShellChars(string path)
    {
        foreach (char c in path)
        {
            if (c < 0x20 || c == 0x7f)
                return true;
            if (c is '&' or '|' or '<' or '>' or '^' or '%' or '"' or '!' or '(' or ')' or '*' or '?')
                return true;
        }
        return false;
    }

    public static void InstallTo(
        string dir, byte[] payload, Action<string>? log = null, bool enableAutostart = false)
    {
        // Defence in depth: the wizard and silent path validate first, but a
        // direct caller must not be able to install into a drive root or an
        // arbitrary existing folder that UninstallAll would later delete.
        var invalid = ValidateDirectory(dir);
        if (invalid != null)
            throw new ArgumentException(invalid, nameof(dir));
        dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir.Trim()));
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
            key?.SetValue("DisplayVersion", AppVersion);
            key?.SetValue("Publisher", "win-fast-finder contributors");
            key?.SetValue("UninstallString", $"\"{Path.Combine(dir, "Uninstall.exe")}\" --uninstall");
            key?.SetValue("NoModify", 1);
            key?.SetValue("NoRepair", 1);
        }
        // Autostart is opt-in (README / PRIVACY): only write the Run value when
        // the caller explicitly asked, otherwise leave an existing choice alone.
        if (enableAutostart)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key?.SetValue(RunValue, $"\"{Path.Combine(dir, "Wff.App.exe")}\" --tray");
        }
        log?.Invoke("Done.");
    }

    // True only for an installed app directory: correct leaf AND the two
    // executables this product puts there. UninstallAll refuses otherwise so
    // a renamed/relocated stub can never recursively erase an arbitrary folder.
    public static bool IsSafeUninstallDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return false;
        string full;
        try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)); }
        catch { return false; }
        if (!Path.GetFileName(full).Equals(AppName, StringComparison.OrdinalIgnoreCase))
            return false;
        if (HasUnsafeShellChars(full))
            return false;
        try
        {
            var attrs = File.GetAttributes(full);
            if (attrs.HasFlag(FileAttributes.ReparsePoint))
                return false;
            return File.Exists(Path.Combine(full, "Uninstall.exe"))
                && File.Exists(Path.Combine(full, "Wff.App.exe"));
        }
        catch
        {
            return false;
        }
    }

    public static void UninstallAll()
    {
        string ownDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
        if (!IsSafeUninstallDirectory(ownDir))
            throw new InvalidOperationException(
                $"Refusing to uninstall from \"{ownDir}\": expected a folder named " +
                $"\"{AppName}\" containing Uninstall.exe and Wff.App.exe.");
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
