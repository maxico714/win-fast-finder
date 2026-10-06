using Wff.Engine;

namespace Wff.Tests;

public sealed class GlobTests
{
    private static FileIndex IndexOf(params string[] names)
    {
        var paths = names.Select(n => $"C:\\d\\{n}").ToList();
        return FileIndex.Build(paths);
    }

    [Fact]
    public void Star_Pdf_Matches_Only_Pdfs()
    {
        var index = IndexOf("a.pdf", "b.txt", "c.PDF", "pdf-notes.docx");
        var hits = index.Search("*.pdf", 10);
        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.EndsWith(".pdf", h, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Prefix_Star_Matches_Substring()
    {
        var index = IndexOf("report-final.docx", "my-report.docx", "notes.txt");
        var hits = index.Search("rep*.docx", 10);
        Assert.Single(hits);
        var hits2 = index.Search("*rep*.docx", 10);
        Assert.Equal(2, hits2.Count);
    }

    [Fact]
    public void Question_Mark_Matches_Single_Char()
    {
        var index = IndexOf("file1.txt", "file12.txt", "file2.txt");
        var hits = index.Search("file?.txt", 10);
        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Glob_Combines_With_Ext_Filter()
    {
        var index = IndexOf("a.pdf", "b.pdf", "c.txt");
        var hits = index.Search("*.pdf ext:pdf", 10);
        Assert.Equal(2, hits.Count);
    }
}
