// Settings: blacklist (skipped dir names/paths) + whitelist roots
// (if empty: all fixed drives). JSON in %APPDATA%, BCL-only.
using System.Text.Json;

namespace Wff.Engine;

public sealed class Settings
{
    // No initializer: a file WITHOUT this key deserializes to 0 (= old version).
    public int Version { get; set; }

    public Settings()
    {
        Version = 2;
    }    public List<string> Blacklist { get; set; } = new()
    {
        // AppData holds browser/IDE caches (100k+ churn files): skip by
        // default for a fast first index; removable in Settings.
        "AppData", "node_modules", ".git", "obj", "bin", ".vs",
    };
    public List<string> WhitelistRoots { get; set; } = new();
    // Full drive roots to skip, e.g. "D:\". Also filters whitelists.
    public List<string> IgnoredDrives { get; set; } = new();
    // File extensions skipped at index time, e.g. ".tmp". With dot or not.
    public List<string> IgnoredExtensions { get; set; } = new();
    // Production options (see docs): hotkeys, search, appearance, startup.
    public string MainHotkey { get; set; } = "Ctrl+Shift+F";
    public bool CtrlDoubleTap { get; set; } = true;
    public bool IgnoreInFullscreen { get; set; } = true;
    public int MaxResults { get; set; } = 50;
    public bool MatchPath { get; set; } = false;
    public string SortMode { get; set; } = "rank"; // rank|name|date|size
    public bool HideHiddenAndSystem { get; set; } = true;
    public string Theme { get; set; } = "System"; // System|Light|Dark
    public bool LaunchAtLogin { get; set; } = false;

    public bool IsDriveIgnored(string path)
    {
        foreach (var d in IgnoredDrives)
        {
            var base_ = d.TrimEnd('\\', '/');
            if (path.Equals(base_, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(base_ + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public List<string> ResolveRoots()
    {
        List<string> roots = WhitelistRoots.Count > 0
            ? WhitelistRoots.Where(Directory.Exists).ToList()
            : DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .ToList();
        return roots.Where(r => !IsDriveIgnored(r)).ToList();
    }

    // First-run default: user profile only (fast). Full drives need an
    // explicit whitelist — walking C:\ blind takes many minutes.
    public List<string> StartupRoots()
    {
        var white = WhitelistRoots.Where(Directory.Exists).ToList();
        if (white.Count > 0)
            return white.Where(r => !IsDriveIgnored(r)).ToList();
        return new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
    }

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "win-fast-finder", "settings.json");

    // Old files were written before Version existed: detect by key absence
    // (constructors/initializers run before deserialization, so a missing
    // key never reads as 0 — must inspect the raw JSON).
    private static bool IsPreV2(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return !doc.RootElement.TryGetProperty("Version", out _);
        }
        catch
        {
            return false;
        }
    }

    public static void Save(Settings s, string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    // Backup to anywhere / restore with validation (throws InvalidDataException).
    public static void Export(Settings s, string path)
    {
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static Settings Import(string path)
    {
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception ex) { throw new InvalidDataException("cannot read file: " + ex.Message, ex); }
        Settings? s;
        try { s = JsonSerializer.Deserialize<Settings>(text); }
        catch (Exception ex) { throw new InvalidDataException("not a settings file: " + ex.Message, ex); }
        if (s == null)
            throw new InvalidDataException("not a settings file: empty");
        return s;
    }

    public static Settings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
                return new Settings();
            var text = File.ReadAllText(path);
            var s = JsonSerializer.Deserialize<Settings>(text);
            if (s == null)
                return new Settings();
            if (IsPreV2(text) && !s.Blacklist.Contains("AppData"))
            {
                s.Blacklist.Add("AppData"); // v2 migration: skip cache noise
                s.Version = 2;
            }
            return s;
        }
        catch
        {
            return new Settings(); // corrupt file: safe defaults, never crash
        }
    }
}
