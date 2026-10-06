using Wff.Engine;

namespace Wff.Tests;

public sealed class IndexTests
{
    [Fact]
    public void Search_Returns_Best_Match_First()
    {
        var paths = new List<string>();
        for (int i = 0; i < 20000; i++)
            paths.Add($"C:\\proj\\file_{i:D5}_notes.txt");
        paths.Add("C:\\proj\\Project_Report_Final.docx");

        var index = FileIndex.Build(paths);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var hits = index.Search("report", 10);
        sw.Stop();

        Assert.NotEmpty(hits);
        Assert.Contains("Report", hits[0], StringComparison.OrdinalIgnoreCase);
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"too slow: {sw.ElapsedMilliseconds}ms for 20k rows");
    }

    [Fact]
    public void Search_Empty_Query_Returns_Empty()
    {
        var index = FileIndex.Build(new[] { "C:\\a\\b.txt" });
        Assert.Empty(index.Search("", 10));
    }
}
