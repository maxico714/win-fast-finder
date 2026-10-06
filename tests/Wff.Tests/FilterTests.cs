using Wff.Engine;

namespace Wff.Tests;

public sealed class FilterTests
{
    private static string MakeTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "wfff_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "report.pdf"), new string('x', 5000));
        File.WriteAllText(Path.Combine(root, "report.txt"), "x");
        File.WriteAllText(Path.Combine(root, "notes.pdf"), "x");
        File.SetLastWriteTimeUtc(Path.Combine(root, "report.pdf"), DateTime.UtcNow);
        File.SetLastWriteTimeUtc(Path.Combine(root, "notes.pdf"), DateTime.UtcNow - TimeSpan.FromDays(30));
        return root;
    }

    [Fact]
    public void Ext_Filter_Keeps_Only_Matching_Extension()
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var hits = index.Search("report ext:pdf", 10);
            Assert.Single(hits);
            Assert.EndsWith("report.pdf", hits[0]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Size_Filter_Keeps_Only_Big_Files()
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var hits = index.Search("report size:>1kb", 10);
            Assert.Single(hits);
            Assert.EndsWith("report.pdf", hits[0]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Dm_Filter_Keeps_Only_Recent_Files()
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            var hits = index.Search("pdf dm:<7d", 10);
            Assert.Single(hits);
            Assert.EndsWith("report.pdf", hits[0]);
        }
        finally { Directory.Delete(root, true); }
    }
}
