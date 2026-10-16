using Wff.Engine;

namespace Wff.Tests;

// Scale guards: selective filters and full-set sorts must hold past the
// old 500-examined and 20,000-scored cutoffs.
public sealed class ScaleTests
{
    [Fact]
    public void Filters_Hold_Over_5000_Files()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffsc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (int i = 0; i < 5000; i++)
            {
                bool isBig = i % 167 == 0; // ~30 files
                bool isRecent = i >= 4975; // 25 files
                var p = Path.Combine(root, $"scale-{i:D5}.dat");
                File.WriteAllText(p, new string('x', isBig ? 100 * 1024 : 10));
                if (!isRecent)
                    File.SetLastWriteTimeUtc(p, DateTime.UtcNow - TimeSpan.FromDays(40));
            }
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            Assert.Equal(5000, index.Count);
            var big = index.Search(QueryParser.Parse("scale size:>50kb"), 100);
            Assert.Equal(30, big.Count);
            var recent = index.Search(QueryParser.Parse("scale dm:<1d"), 100);
            Assert.Equal(25, recent.Count);
            var (rows, total) = index.SearchDetailed(QueryParser.Parse("scale size:>50kb"), 10);
            Assert.Equal(10, rows.Count);
            Assert.Equal(30, total);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Size_Sort_Sees_Past_20000_Matches()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffso_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var big = Path.Combine(root, "z-biggest.log");
            File.WriteAllText(big, new string('x', 10000));
            for (int i = 0; i < 20000; i++)
                File.WriteAllBytes(Path.Combine(root, $"f{i:D5}.log"), Array.Empty<byte>());
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            Assert.Equal(20001, index.Count);
            var q = QueryParser.Parse("log") with { Sort = "size" };
            var (rows, total) = index.SearchDetailed(q, 5);
            Assert.Equal(20001, total);
            Assert.EndsWith("z-biggest.log", rows[0].Path);
        }
        finally { Directory.Delete(root, true); }
    }
}
