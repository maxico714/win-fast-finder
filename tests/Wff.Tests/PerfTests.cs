using Wff.Engine;

namespace Wff.Tests;

// Speed contract regression: keystroke -> results must stay interactive.
public sealed class PerfTests
{
    [Fact]
    public void Search_100k_Rows_Stays_Interactive()
    {
        var paths = new List<string>(100000);
        for (int i = 0; i < 100000; i++)
            paths.Add($"C:\\data\\project_{i % 1000:D4}_report_{i:D6}.docx");
        var index = FileIndex.Build(paths);

        var queries = new[] { "report", "proj042", "99999", "docx", "project_7" };
        long worst = 0;
        foreach (var q in queries)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var hits = index.Search(q, 50);
            sw.Stop();
            Assert.NotEmpty(hits);
            worst = Math.Max(worst, sw.ElapsedMilliseconds);
        }
        Assert.True(worst < 1000, $"worst query {worst}ms over 100k rows");
    }
}
