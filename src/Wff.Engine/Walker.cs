// Parallel + sequential file walkers: early blacklist pruning,
// reparse-point avoidance (never follows junctions/symlinks).
namespace Wff.Engine;

using System.Collections.Concurrent;

public static class Walker
{
    private static readonly string[] DefaultSkip = new[]
    {
        "$Recycle.Bin", "System Volume Information", "$Windows.~BT", "$Windows.~WS",
    };

    public static IEnumerable<string> Enumerate(string root, IEnumerable<string>? blacklist = null,
        bool skipHiddenSystem = true)
    {
        var skip = new HashSet<string>(DefaultSkip, StringComparer.OrdinalIgnoreCase);
        if (blacklist != null)
            foreach (var b in blacklist)
                skip.Add(b);

        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            catch (IOException) { continue; }

            foreach (var e in entries)
            {
                FileAttributes attr;
                try { attr = File.GetAttributes(e); }
                catch { continue; }
                bool isDir = (attr & FileAttributes.Directory) != 0;
                bool isReparse = (attr & FileAttributes.ReparsePoint) != 0;
                if (skipHiddenSystem && (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    continue;
                if (isDir)
                {
                    if (isReparse || IsSkippedDir(e, skip))
                        continue;
                    stack.Push(e);
                }
                else if (!isReparse)
                {
                    yield return e;
                }
            }
        }
    }

    // Skip by folder NAME ("node_modules") or by full PATH ("C:\work\secret").
    public static bool IsSkippedDir(string fullPath, HashSet<string> skip)
    {
        if (skip.Contains(Path.GetFileName(fullPath)))
            return true;
        foreach (var s in skip)
        {
            if (s.IndexOfAny(new[] { '\\', '/' }) < 0)
                continue;
            var base_ = s.TrimEnd('\\', '/');
            if (fullPath.Equals(base_, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(base_ + "\\", StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(base_ + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static List<string> EnumerateParallel(string root, IEnumerable<string>? blacklist = null,
        IProgress<int>? progress = null, bool skipHiddenSystem = true)
    {
        var skip = new HashSet<string>(DefaultSkip, StringComparer.OrdinalIgnoreCase);
        if (blacklist != null)
            foreach (var b in blacklist)
                skip.Add(b);

        var pending = new ConcurrentStack<string>();
        pending.Push(root);
        var files = new ConcurrentBag<string>();
        int active = 0;
        int reported = 0;
        int workers = Math.Max(2, Environment.ProcessorCount);
        using var done = new CountdownEvent(workers);
        for (int w = 0; w < workers; w++)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var spin = new SpinWait();
                    while (true)
                    {
                        if (pending.TryPop(out var dir))
                        {
                            Interlocked.Increment(ref active);
                            try { ScanDir(dir, skip, pending, files, progress, ref reported, skipHiddenSystem); }
                            finally { Interlocked.Decrement(ref active); }
                            spin.Reset();
                        }
                        else if (Volatile.Read(ref active) == 0)
                        {
                            break; // stack empty and nobody scanning: truly done
                        }
                        else
                        {
                            spin.SpinOnce();
                        }
                    }
                }
                finally
                {
                    done.Signal();
                }
            });
        }
        done.Wait();
        var all = files.ToList();
        progress?.Report(all.Count); // exact final total
        return all;
    }

    private static void ScanDir(string dir, HashSet<string> skip,
        ConcurrentStack<string> pending, ConcurrentBag<string> files,
        IProgress<int>? progress, ref int reported, bool skipHiddenSystem)
    {
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(dir);
        }
        catch (UnauthorizedAccessException) { return; }
        catch (DirectoryNotFoundException) { return; }
        catch (IOException) { return; }

        foreach (var e in entries)
        {
            FileAttributes attr;
            try { attr = File.GetAttributes(e); }
            catch { continue; }
            bool isDir = (attr & FileAttributes.Directory) != 0;
            bool isReparse = (attr & FileAttributes.ReparsePoint) != 0;
            if (skipHiddenSystem && (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                continue;
            if (isDir)
            {
                if (isReparse || IsSkippedDir(e, skip))
                    continue;
                pending.Push(e);
            }
            else if (!isReparse)
            {
                files.Add(e);
                int n = Interlocked.Increment(ref reported);
                if (n % 1000 == 0)
                    progress?.Report(n);
            }
        }
    }
}
