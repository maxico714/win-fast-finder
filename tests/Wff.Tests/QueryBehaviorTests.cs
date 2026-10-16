using System.Threading;
using Wff.Engine;

namespace Wff.Tests;

// End-to-end behaviour of the size/date grammar against real files.
public sealed class QueryBehaviorTests
{
    private static string MakeSizedTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffsz_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "exact-1mb.bin"), new string('x', 1048576));
        File.WriteAllText(Path.Combine(root, "plus-5k.bin"), new string('x', 1048576 + 5000));
        File.WriteAllText(Path.Combine(root, "half-mb.bin"), new string('x', 524288));
        File.WriteAllText(Path.Combine(root, "tiny.txt"), "x");
        return root;
    }

    private static QueryOptions Opts(string mode = "DisplayedSize") =>
        new() { ExactSizeMode = mode };

    [Fact]
    public void DisplayedSize_Bare_Finds_Explorer_Lookalikes()
    {
        var root = MakeSizedTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var hits = index.Search(QueryParser.Parse("bin size:1mb", Opts()), 10);
            Assert.Equal(2, hits.Count); // exact-1mb.bin + plus-5k.bin
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ExactBytes_Bare_Is_Strict()
    {
        var root = MakeSizedTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var hits = index.Search(QueryParser.Parse("bin size:1mb", Opts("ExactBytes")), 10);
            Assert.Single(hits);
            Assert.EndsWith("exact-1mb.bin", hits[0]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Percent1_Bare_Allows_Small_Drift()
    {
        var root = MakeSizedTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            // plus-5k is +0.48%: inside 1%, outside exact bytes.
            var hits = index.Search(QueryParser.Parse("bin size:1mb", Opts("Percent1")), 10);
            Assert.Equal(2, hits.Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Operators_Respect_Boundaries()
    {
        var root = MakeSizedTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            Assert.Single(index.Search(QueryParser.Parse("bin size:=1mb", Opts()), 10));
            Assert.Equal(3, index.Search(QueryParser.Parse("bin size:>=524288b", Opts()), 10).Count);
            Assert.Single(index.Search(QueryParser.Parse("bin size:>1048576b", Opts()), 10));
            Assert.Equal(2, index.Search(QueryParser.Parse("bin size:524288b..1048576b", Opts()), 10).Count);
        }
        finally { Directory.Delete(root, true); }
    }

    private static string MakeDatedTree(out string day)
    {
        var root = Path.Combine(Path.GetTempPath(), "wffdt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = DateTime.Today.AddDays(-5);
        day = target.ToString("yyyy-MM-dd");
        File.WriteAllText(Path.Combine(root, "morning.txt"), "x");
        File.SetLastWriteTime(Path.Combine(root, "morning.txt"), target.AddMinutes(10));
        File.WriteAllText(Path.Combine(root, "night.txt"), "x");
        File.SetLastWriteTime(Path.Combine(root, "night.txt"), target.AddHours(23).AddMinutes(59));
        File.WriteAllText(Path.Combine(root, "nextday.txt"), "x");
        File.SetLastWriteTime(Path.Combine(root, "nextday.txt"), target.AddDays(1).AddMinutes(10));
        File.WriteAllText(Path.Combine(root, "now.txt"), "x");
        return root;
    }

    [Fact]
    public void Absolute_Date_Matches_Whole_Local_Day()
    {
        var root = MakeDatedTree(out var day);
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var hits = index.Search(QueryParser.Parse($"txt dm:{day}", Opts()), 10);
            Assert.Equal(2, hits.Count);
            Assert.DoesNotContain(hits, h => h.EndsWith("nextday.txt"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Today_Yesterday_Work()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fresh = Path.Combine(root, "fresh.txt");
            File.WriteAllText(fresh, "x");
            var old = Path.Combine(root, "old.txt");
            File.WriteAllText(old, "x");
            File.SetLastWriteTime(old, DateTime.Now.Date.AddDays(-1).AddHours(12));
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var today = index.Search(QueryParser.Parse("txt dm:today", Opts()), 10);
            Assert.Single(today);
            Assert.EndsWith("fresh.txt", today[0]);
            var yest = index.Search(QueryParser.Parse("txt dm:yesterday", Opts()), 10);
            Assert.Single(yest);
            Assert.EndsWith("old.txt", yest[0]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SearchDetailed_Reports_True_Total()
    {
        var root = MakeSizedTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var (rows, total) = index.SearchDetailed(
                QueryParser.Parse("bin size:>1b", Opts()), 2);
            Assert.Equal(2, rows.Count);
            Assert.Equal(3, total);
            Assert.All(rows, r => Assert.True(r.SizeBytes > 1));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Cancelled_Search_Throws_OperationCanceled()
    {
        var root = MakeSizedTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                index.SearchDetailed(QueryParser.Parse("bin", Opts()), 10, cts.Token));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Hidden_Files_Follow_The_Flag()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var root = Path.Combine(Path.GetTempPath(), "wffhd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var secret = Path.Combine(root, "secret.txt");
            File.WriteAllText(secret, "x");
            File.SetAttributes(secret, FileAttributes.Hidden);
            File.WriteAllText(Path.Combine(root, "plain.txt"), "x");
            var skipped = Walker.Enumerate(root, null, skipHiddenSystem: true).ToList();
            Assert.DoesNotContain(skipped, f => f.EndsWith("secret.txt"));
            var kept = Walker.Enumerate(root, null, skipHiddenSystem: false).ToList();
            Assert.Contains(kept, f => f.EndsWith("secret.txt"));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(root))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(root, true);
        }
    }
}
