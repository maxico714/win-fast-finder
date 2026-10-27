// Incremental refresh: persist every directory's mtime. On restart each known
// directory is stat'ed once; only directories whose mtime changed are
// re-enumerated, and unchanged directories reuse their cached files. Visiting
// directories (not files) is what makes nested changes visible cheaply.
namespace Wff.Engine;

public sealed record RefreshResult(List<string> Files, Dictionary<string, long> Snapshot);

public static class SnapshotStore
{
    private const uint Magic = 0x33534657; // "WSF3"
    private const int MaxEntries = 5_000_000;

    public static void Save(Dictionary<string, long> snap, string file, DateTime? capturedUtc = null)
    {
        long captured = (capturedUtc ?? DateTime.UtcNow).ToUniversalTime().Ticks;
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write,
            FileShare.None, 65536, FileOptions.SequentialScan);
        using var w = new BinaryWriter(fs, System.Text.Encoding.UTF8);
        w.Write(Magic);
        w.Write(captured);
        w.Write(snap.Count);
        foreach (var kv in snap)
        {
            w.Write(kv.Key);
            w.Write(kv.Value);
        }
    }

    public static Dictionary<string, long> Load(string file) => LoadSnapshot(file).Dirs;

    public static (Dictionary<string, long> Dirs, DateTime? CapturedUtc) LoadSnapshot(string file)
    {
        var snap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                FileShare.Read, 65536, FileOptions.SequentialScan);
            using var r = new BinaryReader(fs, System.Text.Encoding.UTF8);
            // A pre-v1.0.3 (WSF2) snapshot is shallow; force a full refresh.
            if (r.ReadUInt32() != Magic)
                return (snap, null);
            long capturedTicks = r.ReadInt64();
            int n = r.ReadInt32();
            if (n < 0 || n > MaxEntries)
                return (new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase), null);
            for (int i = 0; i < n; i++)
                snap[r.ReadString()] = r.ReadInt64();
            DateTime? captured = null;
            if (capturedTicks > 0 && capturedTicks <= DateTime.UtcNow.AddDays(1).Ticks)
                captured = new DateTime(capturedTicks, DateTimeKind.Utc);
            return (snap, captured);
        }
        catch
        {
            return (new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase), null);
        }
    }
}

public static class Incremental
{
    // Directory mtimes can be published slightly after the change; treat
    // anything modified just before the snapshot as dirty.
    private static readonly TimeSpan RecentWindow = TimeSpan.FromSeconds(5);

    public static Dictionary<string, long> SnapshotRoots(IEnumerable<string> roots,
        IEnumerable<string> blacklist, bool skipHiddenSystem = true)
    {
        var snap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var skip = Walker.BuildSkip(blacklist);
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                if (!TryStatDir(dir, root, skipHiddenSystem, skip, out var ticks))
                    continue;
                snap[dir] = ticks;
                foreach (var kid in ChildDirs(dir, skip, skipHiddenSystem))
                    stack.Push(kid);
            }
        }
        return snap;
    }

    public static List<string> Refresh(List<string> oldFiles,
        Dictionary<string, long> snapshot, IEnumerable<string> roots,
        IEnumerable<string> blacklist, Action<string>? onDirScanned = null,
        bool skipHiddenSystem = true, DateTime? snapshotCapturedUtc = null)
    {
        return RefreshWithSnapshot(oldFiles, snapshot, roots, blacklist,
            onDirScanned, skipHiddenSystem, snapshotCapturedUtc).Files;
    }

    public static RefreshResult RefreshWithSnapshot(List<string> oldFiles,
        Dictionary<string, long> snapshot, IEnumerable<string> roots,
        IEnumerable<string> blacklist, Action<string>? onDirScanned = null,
        bool skipHiddenSystem = true, DateTime? snapshotCapturedUtc = null)
    {
        var skip = Walker.BuildSkip(blacklist);
        var cachedByDir = GroupCached(oldFiles);

        // Children of every known directory, from the snapshot. Unchanged
        // directories use this instead of enumerating the filesystem.
        var childrenByParent = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in snapshot.Keys)
        {
            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent))
                continue;
            if (!childrenByParent.TryGetValue(parent, out var list))
                childrenByParent[parent] = list = new List<string>();
            list.Add(dir);
        }

        long cutoff = snapshotCapturedUtc.HasValue
            ? snapshotCapturedUtc.Value.ToUniversalTime().Add(-RecentWindow).Ticks
            : long.MaxValue;

        var files = new List<string>();
        var newSnap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                if (!visited.Add(dir))
                    continue;
                if (!TryStatDir(dir, root, skipHiddenSystem, skip, out var ticks))
                    continue;
                newSnap[dir] = ticks;

                bool known = snapshot.TryGetValue(dir, out var oldTicks);
                bool changed = !known || oldTicks != ticks || ticks >= cutoff;
                if (changed)
                {
                    onDirScanned?.Invoke(dir);
                    files.AddRange(DirectFiles(dir, skipHiddenSystem));
                    foreach (var kid in ChildDirs(dir, skip, skipHiddenSystem))
                        stack.Push(kid);
                }
                else
                {
                    if (cachedByDir.TryGetValue(dir, out var list))
                        files.AddRange(list);
                    if (childrenByParent.TryGetValue(dir, out var kids))
                        foreach (var kid in kids)
                            stack.Push(kid);
                }
            }
        }

        return new RefreshResult(
            files.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), newSnap);
    }

    private static Dictionary<string, List<string>> GroupCached(List<string> oldFiles)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in oldFiles)
        {
            var parent = Path.GetDirectoryName(f);
            if (string.IsNullOrEmpty(parent))
                continue;
            if (!map.TryGetValue(parent, out var list))
                map[parent] = list = new List<string>();
            list.Add(f);
        }
        return map;
    }

    // Root paths are chosen by the user: never apply blacklist/hidden checks
    // to the root itself, only to its children.
    private static bool TryStatDir(string dir, string root, bool skipHiddenSystem,
        HashSet<string> skip, out long ticks)
    {
        ticks = 0;
        try
        {
            if (!dir.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                var attr = File.GetAttributes(dir);
                if ((attr & FileAttributes.ReparsePoint) != 0)
                    return false;
                if (skipHiddenSystem && (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    return false;
                if (Walker.IsSkippedDir(dir, skip))
                    return false;
            }
            ticks = Directory.GetLastWriteTimeUtc(dir).Ticks;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> ChildDirs(string dir, HashSet<string> skip, bool skipHiddenSystem)
    {
        var children = new List<string>();
        try
        {
            foreach (var kid in Directory.EnumerateDirectories(dir))
            {
                FileAttributes attr;
                try { attr = File.GetAttributes(kid); }
                catch { continue; }
                if ((attr & FileAttributes.ReparsePoint) != 0)
                    continue;
                if (skipHiddenSystem && (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    continue;
                if (Walker.IsSkippedDir(kid, skip))
                    continue;
                children.Add(kid);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
        return children;
    }

    private static List<string> DirectFiles(string dir, bool skipHiddenSystem)
    {
        var files = new List<string>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                FileAttributes attr;
                try { attr = File.GetAttributes(f); }
                catch { continue; }
                if ((attr & FileAttributes.ReparsePoint) != 0)
                    continue;
                if (skipHiddenSystem && (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    continue;
                files.Add(f);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
        return files;
    }
}
