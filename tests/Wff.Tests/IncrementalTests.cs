using Wff.Engine;

namespace Wff.Tests;

public sealed class IncrementalTests
{
    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "wffi_" + Guid.NewGuid().ToString("N"));

    // Windows may publish a directory's new mtime slightly after the write.
    private static void WaitForMtimeChange(string dir, long seen, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (Directory.GetLastWriteTimeUtc(dir).Ticks == seen && DateTime.UtcNow < deadline)
            Thread.Sleep(10);
        Assert.NotEqual(seen, Directory.GetLastWriteTimeUtc(dir).Ticks);
    }

    [Fact]
    public void Refresh_Picks_Up_Added_And_Removed_Files_Without_Full_Rescan()
    {
        var root = NewRoot();
        var sub1 = Path.Combine(root, "a");
        var sub2 = Path.Combine(root, "b");
        Directory.CreateDirectory(sub1);
        Directory.CreateDirectory(sub2);
        File.WriteAllText(Path.Combine(sub1, "one.txt"), "x");
        File.WriteAllText(Path.Combine(sub2, "two.txt"), "x");
        try
        {
            var snap = Incremental.SnapshotRoots(new[] { root }, new List<string>());
            var old = Walker.Enumerate(root).ToList();
            int scanned = 0;
            File.WriteAllText(Path.Combine(sub2, "three.txt"), "x");
            File.Delete(Path.Combine(sub1, "one.txt"));
            WaitForMtimeChange(sub2, snap[sub2]);
            var fresh = Incremental.Refresh(old, snap, new[] { root }, new List<string>(), _ => scanned++);
            Assert.Contains(fresh, f => f.EndsWith("three.txt"));
            Assert.DoesNotContain(fresh, f => f.EndsWith("one.txt"));
            Assert.Contains(fresh, f => f.EndsWith("two.txt"));
            int totalDirs = Directory.GetDirectories(root, "*", SearchOption.AllDirectories).Length + 1;
            Assert.True(scanned < totalDirs, $"scanned {scanned}/{totalDirs}, not incremental");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Snapshot_Covers_Nested_Directories()
    {
        var root = NewRoot();
        var deep = Path.Combine(root, "a", "deep");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(deep, "old.txt"), "x");
        try
        {
            var snap = Incremental.SnapshotRoots(new[] { root }, Array.Empty<string>());
            Assert.Contains(root, snap.Keys);
            Assert.Contains(Path.Combine(root, "a"), snap.Keys);
            Assert.Contains(deep, snap.Keys);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Refresh_Picks_Up_Changes_Inside_Nested_Folders()
    {
        var root = NewRoot();
        var top = Path.Combine(root, "a");
        var deep = Path.Combine(top, "deep");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(deep, "old.txt"), "x");
        File.WriteAllText(Path.Combine(top, "keep.txt"), "x");
        try
        {
            var snap = Incremental.SnapshotRoots(new[] { root }, Array.Empty<string>());
            var old = Walker.Enumerate(root).ToList();
            File.WriteAllText(Path.Combine(deep, "fresh.txt"), "x");
            File.Delete(Path.Combine(deep, "old.txt"));
            WaitForMtimeChange(deep, snap[deep]);

            var scannedDirs = new List<string>();
            var fresh = Incremental.Refresh(old, snap, new[] { root }, Array.Empty<string>(), scannedDirs.Add);

            Assert.Contains(fresh, f => f.EndsWith("fresh.txt"));
            Assert.DoesNotContain(fresh, f => f.EndsWith("old.txt"));
            Assert.Contains(fresh, f => f.EndsWith("keep.txt"));
            Assert.Contains(deep, scannedDirs);
            Assert.DoesNotContain(root, scannedDirs);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Refresh_Finds_New_Nested_Directory_Tree()
    {
        var root = NewRoot();
        var top = Path.Combine(root, "a");
        Directory.CreateDirectory(top);
        File.WriteAllText(Path.Combine(top, "keep.txt"), "x");
        try
        {
            var snap = Incremental.SnapshotRoots(new[] { root }, Array.Empty<string>());
            var old = Walker.Enumerate(root).ToList();

            var deeper = Path.Combine(top, "new", "deeper");
            Directory.CreateDirectory(deeper);
            File.WriteAllText(Path.Combine(deeper, "fresh.txt"), "x");

            // Windows can withhold a parent directory's mtime after a child
            // directory is created (observed on the GitHub Windows runner).
            // Use the engine's production recency guard instead of gambling on
            // mtime publication: a just-captured snapshot re-scans recently
            // modified directories.
            var fresh = Incremental.Refresh(old, snap, new[] { root }, Array.Empty<string>(),
                snapshotCapturedUtc: DateTime.UtcNow);
            Assert.Contains(fresh, f => f.EndsWith("fresh.txt"));
            Assert.Contains(fresh, f => f.EndsWith("keep.txt"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Refresh_Drops_Files_From_Deleted_Subtree()
    {
        var root = NewRoot();
        var top = Path.Combine(root, "a");
        var gone = Path.Combine(top, "gone");
        Directory.CreateDirectory(gone);
        File.WriteAllText(Path.Combine(gone, "victim.txt"), "x");
        File.WriteAllText(Path.Combine(top, "keep.txt"), "x");
        try
        {
            var snap = Incremental.SnapshotRoots(new[] { root }, Array.Empty<string>());
            var old = Walker.Enumerate(root).ToList();

            Directory.Delete(gone, true);

            // Same lazy-parent-mtime guard as the new-tree test: directory
            // removal may not publish the parent's mtime promptly on CI
            // runners; the production recency window forces the re-scan.
            var fresh = Incremental.Refresh(old, snap, new[] { root }, Array.Empty<string>(),
                snapshotCapturedUtc: DateTime.UtcNow);
            Assert.DoesNotContain(fresh, f => f.EndsWith("victim.txt"));
            Assert.Contains(fresh, f => f.EndsWith("keep.txt"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Refresh_Keeps_Cached_Files_When_Nothing_Changed()
    {
        var root = NewRoot();
        var top = Path.Combine(root, "a");
        Directory.CreateDirectory(top);
        File.WriteAllText(Path.Combine(top, "one.txt"), "x");
        File.WriteAllText(Path.Combine(root, "top.txt"), "x");
        try
        {
            var snap = Incremental.SnapshotRoots(new[] { root }, Array.Empty<string>());
            var old = Walker.Enumerate(root).ToList();

            int scanned = 0;
            var fresh = Incremental.Refresh(old, snap, new[] { root }, Array.Empty<string>(), _ => scanned++);
            Assert.Equal(
                old.OrderBy(f => f, StringComparer.OrdinalIgnoreCase),
                fresh.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(0, scanned);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
