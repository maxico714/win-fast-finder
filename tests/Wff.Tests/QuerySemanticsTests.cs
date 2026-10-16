using Wff.Engine;

namespace Wff.Tests;

// Every behaviour the README, website and usage guide promise about
// multi-term queries and the path: directive. These failed before the
// fix (verified headless: `path:Downloads` -> 0 hits,
// `invoice 2024` -> 0 hits).
public sealed class QuerySemanticsTests
{
    private static string MakeTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffq_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));
        Directory.CreateDirectory(Path.Combine(root, "Downloads"));
        File.WriteAllText(Path.Combine(root, "projects", "report-final.docx"), new string('x', 15 * 1024));
        File.WriteAllText(Path.Combine(root, "projects", "invoice-2024.pdf"), new string('x', 12 * 1024));
        File.WriteAllText(Path.Combine(root, "projects", "notes.txt"), "x");
        File.SetLastWriteTimeUtc(
            Path.Combine(root, "projects", "notes.txt"), DateTime.UtcNow - TimeSpan.FromDays(40));
        File.WriteAllText(Path.Combine(root, "Downloads", "setup-tool.exe"), new string('x', 2048));
        File.WriteAllText(Path.Combine(root, "Downloads", "readme.md"), "x");
        return root;
    }

    private static void WithIndex(Action<FileIndex> test)
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            Assert.Equal(5, index.Count);
            test(index);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Path_Value_Form_Matches_Files_Under_That_Folder()
    {
        WithIndex(index =>
        {
            var hits = index.Search("path:Downloads", 10);
            Assert.Equal(2, hits.Count);
            Assert.All(hits, h => Assert.Contains("Downloads", h));
        });
    }

    [Fact]
    public void Path_Value_Is_Case_Insensitive()
    {
        WithIndex(index =>
        {
            var hits = index.Search("path:downloads", 10);
            Assert.Equal(2, hits.Count);
        });
    }

    [Fact]
    public void Path_Bare_Token_Still_Works()
    {
        WithIndex(index =>
        {
            var hits = index.Search("setup path:", 10);
            Assert.Single(hits);
            Assert.EndsWith("setup-tool.exe", hits[0]);
        });
    }

    [Fact]
    public void Path_Value_Combines_With_Ext_Filter()
    {
        WithIndex(index =>
        {
            var hits = index.Search("path:downloads ext:exe", 10);
            Assert.Single(hits);
            Assert.EndsWith("setup-tool.exe", hits[0]);
        });
    }

    [Fact]
    public void Multi_Term_Matches_Regardless_Of_Order()
    {
        WithIndex(index =>
        {
            foreach (var q in new[] { "invoice 2024", "2024 invoice" })
            {
                var hits = index.Search(q, 10);
                Assert.Single(hits);
                Assert.EndsWith("invoice-2024.pdf", hits[0]);
            }
        });
    }

    [Fact]
    public void Multi_Term_Requires_All_Terms()
    {
        WithIndex(index =>
        {
            Assert.Empty(index.Search("invoice xyzzy", 10));
            Assert.Empty(index.Search("xyzzy invoice", 10));
        });
    }

    [Fact]
    public void Multi_Term_Combines_With_Filters()
    {
        WithIndex(index =>
        {
            var hits = index.Search("2024 ext:pdf", 10);
            Assert.Single(hits);
            Assert.EndsWith("invoice-2024.pdf", hits[0]);
        });
    }

    [Fact]
    public void Single_Term_Exact_Name_Still_Ranks_First()
    {
        WithIndex(index =>
        {
            var hits = index.Search("readme.md", 10);
            Assert.NotEmpty(hits);
            Assert.EndsWith("readme.md", hits[0]);
        });
    }

    private static string MakeBigTree(out List<string> bigOnes)
    {
        var root = Path.Combine(Path.GetTempPath(), "wffb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        bigOnes = new List<string>();
        for (int i = 0; i < 600; i++)
        {
            var p = Path.Combine(root, $"file-{i:D4}.txt");
            bool big = i % 50 == 0;
            File.WriteAllText(p, new string('x', big ? 5 * 1024 : 10));
            if (big)
                bigOnes.Add(p);
        }
        return root;
    }

    [Fact]
    public void Size_Filter_Finds_Matches_Past_Any_Examined_Cap()
    {
        // 600 text-matching files, 12 passing the size filter. The old
        // top-500-examined loop silently dropped whichever passers fell
        // outside the first 500 scored: every passer must be found.
        var root = MakeBigTree(out var bigOnes);
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            Assert.Equal(600, index.Count);
            var hits = index.Search("file size:>1kb", 50);
            Assert.Equal(bigOnes.Count, hits.Count);
            foreach (var b in bigOnes)
                Assert.Contains(b, hits);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Size_Only_Query_Finds_All_Passers()
    {
        var root = MakeBigTree(out var bigOnes);
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var hits = index.Search("size:>1kb", 50);
            Assert.Equal(bigOnes.Count, hits.Count);
            foreach (var b in bigOnes)
                Assert.Contains(b, hits);
        }
        finally { Directory.Delete(root, true); }
    }
}
