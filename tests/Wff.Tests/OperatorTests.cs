using Wff.Engine;

namespace Wff.Tests;

// Logical operators: [AND] is an explicit no-op (AND is the default),
// [OR] splits free text into alternative groups. Directives stay global.
public sealed class OperatorTests
{
    private static string MakeTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffop_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "invoice-2024.pdf"), new string('x', 12 * 1024));
        File.WriteAllText(Path.Combine(root, "report-final.docx"), new string('x', 15 * 1024));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "x");
        File.WriteAllText(Path.Combine(root, "setup.exe"), "x");
        return root;
    }

    private static void WithIndex(Action<FileIndex> test)
    {
        var root = MakeTree();
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            Assert.Equal(4, index.Count);
            test(index);
        }
        finally { Directory.Delete(root, true); }
    }

    private static List<string> Names(List<string> hits) =>
        hits.Select(Path.GetFileName).ToList()!;

    [Fact]
    public void Or_Finds_Either_Side()
    {
        WithIndex(index =>
        {
            var names = Names(index.Search("invoice [OR] report", 10));
            Assert.Equal(2, names.Count);
            Assert.Contains("invoice-2024.pdf", names);
            Assert.Contains("report-final.docx", names);
        });
    }

    [Fact]
    public void Or_Is_Case_Insensitive()
    {
        WithIndex(index =>
        {
            Assert.Equal(2, index.Search("invoice [or] report", 10).Count);
            Assert.Equal(2, index.Search("invoice [Or] report", 10).Count);
        });
    }

    [Fact]
    public void And_Is_Explicit_Default()
    {
        WithIndex(index =>
        {
            var plain = Names(index.Search("invoice 2024", 10));
            var explicit_ = Names(index.Search("invoice [AND] 2024", 10));
            Assert.Equal(plain, explicit_);
            Assert.Single(explicit_);
        });
    }

    [Fact]
    public void And_Binds_Tighter_Than_Or()
    {
        WithIndex(index =>
        {
            // (notes) OR (setup AND exe-absent-impossible) -> notes only... use real:
            // (invoice) OR (report AND final) -> both files.
            var names = Names(index.Search("invoice [OR] report [AND] final", 10));
            Assert.Equal(2, names.Count);
            // Counter-proof: (invoice OR report) AND final would be 1.
            var anded = Names(index.Search("final", 10));
            Assert.Single(anded);
        });
    }

    [Fact]
    public void Directives_Stay_Global_Across_Or()
    {
        WithIndex(index =>
        {
            var hits = index.Search("invoice [OR] report ext:pdf", 10);
            Assert.Single(hits);
            Assert.EndsWith("invoice-2024.pdf", hits[0]);
        });
    }

    [Fact]
    public void Trailing_Or_Changes_Nothing()
    {
        WithIndex(index =>
        {
            var a = Names(index.Search("invoice", 10));
            var b = Names(index.Search("invoice [OR]", 10));
            Assert.Equal(a, b);
        });
    }

    [Fact]
    public void Bare_Operators_Match_Nothing()
    {
        WithIndex(index =>
        {
            Assert.Empty(index.Search("[OR]", 10));
            Assert.Empty(index.Search("[AND]", 10));
            Assert.Empty(index.Search("[OR] [AND]", 10));
        });
    }

    [Fact]
    public void Or_With_Size_Filter()
    {
        WithIndex(index =>
        {
            var hits = index.Search("invoice [OR] report size:>10kb", 10);
            Assert.Equal(2, hits.Count);
        });
    }

    [Fact]
    public void Totals_Count_Or_Matches()
    {
        WithIndex(index =>
        {
            var (rows, total) = index.SearchDetailed(
                QueryParser.Parse("invoice [OR] report"), 1);
            Assert.Single(rows);
            Assert.Equal(2, total);
        });
    }
}
