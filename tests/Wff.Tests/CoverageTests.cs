using Wff.Engine;

namespace Wff.Tests;

// Completeness: nothing the user allowed may silently go missing.
public sealed class CoverageTests
{
    private static string MakeTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffcv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "keep"));
        Directory.CreateDirectory(Path.Combine(root, "secret"));
        File.WriteAllText(Path.Combine(root, "keep", "a.txt"), "x");
        File.WriteAllText(Path.Combine(root, "secret", "b.txt"), "x");
        File.WriteAllText(Path.Combine(root, "top.txt"), "x");
        return root;
    }

    [Fact]
    public void Full_Path_Blacklist_Holds_Through_Refresh()
    {
        var root = MakeTree();
        try
        {
            var roots = new[] { root };
            var oldFiles = Walker.Enumerate(root).ToList();
            Assert.Equal(3, oldFiles.Count);
            var snap = Incremental.SnapshotRoots(roots, Enumerable.Empty<string>());
            // New file lands in secret AFTER the snapshot (dir mtime moves).
            File.WriteAllText(Path.Combine(root, "secret", "c.txt"), "x");
            var refreshed = Incremental.Refresh(oldFiles, snap, roots,
                new[] { Path.Combine(root, "secret") });
            Assert.DoesNotContain(refreshed, f => f.Contains("secret"));
            Assert.Contains(refreshed, f => f.EndsWith("a.txt"));
            Assert.Contains(refreshed, f => f.EndsWith("top.txt"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Name_Blacklist_Still_Holds_Through_Refresh()
    {
        var root = MakeTree();
        try
        {
            var roots = new[] { root };
            var oldFiles = Walker.Enumerate(root).ToList();
            var snap = Incremental.SnapshotRoots(roots, Enumerable.Empty<string>());
            var refreshed = Incremental.Refresh(oldFiles, snap, roots, new[] { "secret" });
            Assert.DoesNotContain(refreshed, f => f.Contains("secret"));
            Assert.Equal(2, refreshed.Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Sequential_And_Parallel_Walkers_Agree()
    {
        var root = MakeTree();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "keep", "nested"));
            File.WriteAllText(Path.Combine(root, "keep", "nested", "deep.txt"), "x");
            var seq = Walker.Enumerate(root).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            var par = Walker.EnumerateParallel(root).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.Equal(seq, par);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Hidden_Folders_Follow_The_Flag()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var root = Path.Combine(Path.GetTempPath(), "wffhd2_" + Guid.NewGuid().ToString("N"));
        var hid = Path.Combine(root, "hiddendir");
        Directory.CreateDirectory(hid);
        try
        {
            File.WriteAllText(Path.Combine(hid, "inside.txt"), "x");
            File.WriteAllText(Path.Combine(root, "outside.txt"), "x");
            new DirectoryInfo(hid).Attributes |= FileAttributes.Hidden;
            var skipped = Walker.Enumerate(root, null, skipHiddenSystem: true).ToList();
            Assert.DoesNotContain(skipped, f => f.Contains("hiddendir"));
            var kept = Walker.Enumerate(root, null, skipHiddenSystem: false).ToList();
            Assert.Contains(kept, f => f.EndsWith("inside.txt"));
        }
        finally
        {
            new DirectoryInfo(hid).Attributes &= ~FileAttributes.Hidden;
            Directory.Delete(root, true);
        }
    }
}
