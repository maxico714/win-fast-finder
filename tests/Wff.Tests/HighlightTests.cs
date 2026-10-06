using Wff.Engine;

namespace Wff.Tests;

public sealed class HighlightTests
{
    [Fact]
    public void Exact_Filename_Ranks_First()
    {
        var index = FileIndex.Build(new List<string>
        {
            "C:\\d\\my-report-final.docx",
            "C:\\d\\report.docx",
        });
        var hits = index.Search("report.docx", 10);
        Assert.Equal(2, hits.Count);
        Assert.EndsWith("report.docx", hits[0]);
    }

    [Fact]
    public void MatchSpans_Mark_Subsequence_Hits()
    {
        var spans = MatchSpans.Find("my-report.docx", "report");
        // r-e-p-o-r-t at indices 3..8
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8 }, spans);
    }

    [Fact]
    public void MatchSpans_Empty_When_No_Match()
    {
        Assert.Empty(MatchSpans.Find("notes.txt", "xyz"));
    }
}
