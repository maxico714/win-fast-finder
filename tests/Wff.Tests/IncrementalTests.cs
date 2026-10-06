using Wff.Engine;

namespace Wff.Tests;

public sealed class IncrementalTests
{
    [Fact]
    public void Refresh_Picks_Up_Added_And_Removed_Files_Without_Full_Rescan()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffi_" + Guid.NewGuid().ToString("N"));
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
}
