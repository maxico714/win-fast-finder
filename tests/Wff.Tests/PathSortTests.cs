using Wff.Engine;

namespace Wff.Tests;

public sealed class PathSortTests
{
    private static string MakeTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffps_" + Guid.NewGuid().ToString("N"));
        var proj = Path.Combine(root, "myproject");
        Directory.CreateDirectory(proj);
        var big = Path.Combine(proj, "aaa.txt");
        var small = Path.Combine(proj, "zzz.txt");
        File.WriteAllText(big, new string('x', 5000));
        File.WriteAllText(small, "x");
        File.SetLastWriteTimeUtc(big, DateTime.UtcNow - TimeSpan.FromDays(10));
        File.SetLastWriteTimeUtc(small, DateTime.UtcNow);
        return root;
    }

    [Fact]
    public void Path_Token_Matches_Folder_Name()
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var plain = index.Search("myproject", 10);
            Assert.Empty(plain); // filename-only: folder name is not a hit
            var hits = index.Search("myproject path:", 10);
            Assert.Equal(2, hits.Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Sort_Name_Orders_Alphabetically()
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var q = QueryParser.Parse("txt");
            q = q with { Sort = "name" };
            var hits = index.Search(q, 10);
            Assert.Equal(2, hits.Count);
            Assert.EndsWith("aaa.txt", hits[0]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Sort_Size_Puts_Biggest_First_And_Date_Puts_Newest_First()
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var bySize = index.Search(QueryParser.Parse("txt") with { Sort = "size" }, 10);
            Assert.EndsWith("aaa.txt", bySize[0]);
            var byDate = index.Search(QueryParser.Parse("txt") with { Sort = "date" }, 10);
            Assert.EndsWith("zzz.txt", byDate[0]);
        }
        finally { Directory.Delete(root, true); }
    }
}
