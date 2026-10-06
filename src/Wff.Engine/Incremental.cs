// Incremental refresh: partition each root by top-level child; re-walk
// only children whose mtime changed. Unchanged subtrees keep cached entries.
namespace Wff.Engine;

public static class SnapshotStore
{
    private const uint Magic = 0x32534657; // "WSF2"

    public static void Save(Dictionary<string, long> snap, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write,
            FileShare.None, 65536, FileOptions.SequentialScan);
        using var w = new BinaryWriter(fs, System.Text.Encoding.UTF8);
        w.Write(Magic);
        w.Write(snap.Count);
        foreach (var kv in snap)
        {
            w.Write(kv.Key);
            w.Write(kv.Value);
        }
    }

    public static Dictionary<string, long> Load(string file)
    {
        var snap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                FileShare.Read, 65536, FileOptions.SequentialScan);
            using var r = new BinaryReader(fs, System.Text.Encoding.UTF8);
            if (r.ReadUInt32() != Magic)
                return snap;
            int n = r.ReadInt32();
            if (n < 0 || n > 5_000_000)
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < n; i++)
                snap[r.ReadString()] = r.ReadInt64();
        }
        catch
        {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
        return snap;
    }
}

public static class Incremental
{
    public static Dictionary<string, long> SnapshotRoots(IEnumerable<string> roots, IEnumerable<string> blacklist)
    {
        var snap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var skip = new HashSet<string>(blacklist ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;
            snap[root] = DirTime(root);
            IEnumerable<string> kids;
            try { kids = Directory.EnumerateDirectories(root); }
            catch { continue; }
            foreach (var k in kids)
            {
                if (skip.Contains(Path.GetFileName(k)))
                    continue;
                try
                {
                    if ((File.GetAttributes(k) & FileAttributes.ReparsePoint) != 0)
                        continue;
                }
                catch { continue; }
                snap[k] = DirTime(k);
            }
        }
        return snap;
    }

    public static List<string> Refresh(List<string> oldFiles,
        Dictionary<string, long> snapshot, IEnumerable<string> roots,
        IEnumerable<string> blacklist, Action<string>? onDirScanned = null)
    {
        var skip = new HashSet<string>(blacklist ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var rootsList = roots.ToList();

        bool UnderAnyRoot(string f, out string root)
        {
            foreach (var r in rootsList)
            {
                if (f.StartsWith(r.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    root = r;
                    return true;
                }
            }
            root = "";
            return false;
        }

        foreach (var root in rootsList)
        {
            if (!Directory.Exists(root))
                continue;
            var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> kids;
            try { kids = Directory.EnumerateDirectories(root); }
            catch { continue; }
            foreach (var k in kids)
            {
                var name = Path.GetFileName(k);
                if (skip.Contains(name))
                    continue;
                bool gone = !snapshot.TryGetValue(k, out var t) || t != DirTimeSafe(k);
                if (gone)
                    stale.Add(k);
            }
            // Root-level files: cheap, always re-list.
            try
            {
                foreach (var f in Directory.EnumerateFiles(root))
                    result.Add(f);
            }
            catch { }

            // Keep cached entries from unchanged children; re-walk stale ones.
            foreach (var f in oldFiles)
            {
                if (!UnderAnyRoot(f, out var r) || !r.Equals(root, StringComparison.OrdinalIgnoreCase))
                    continue;
                var top = TopChild(root, f);
                if (top == null)
                    continue; // root-level: already re-listed
                if (skip.Contains(Path.GetFileName(top)))
                    continue;
                if (!stale.Contains(top) && Directory.Exists(top))
                    result.Add(f);
            }
            foreach (var s in stale)
            {
                onDirScanned?.Invoke(s);
                result.AddRange(Walker.Enumerate(s, skip));
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? TopChild(string root, string file)
    {
        var rel = Path.GetRelativePath(root, file);
        int sep = rel.IndexOf(Path.DirectorySeparatorChar);
        if (sep < 0)
            return null; // root-level file
        return Path.Combine(root, rel[..sep]);
    }

    private static long DirTime(string dir)
    {
        try { return Directory.GetLastWriteTimeUtc(dir).Ticks; }
        catch { return 0; }
    }

    private static long DirTimeSafe(string dir)
    {
        try
        {
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                return -1;
            return Directory.GetLastWriteTimeUtc(dir).Ticks;
        }
        catch { return -1; }
    }
}
