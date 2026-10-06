using Wff.Engine;

namespace Wff.Tests;

// L5 adversarial: garbage in, no crash out, bounded time.
public sealed class AdversarialTests
{
    private static FileIndex SmallIndex() =>
        FileIndex.Build(new List<string> { "C:\\d\\report.pdf", "C:\\d\\notes ünïcode.txt" });

    [Fact]
    public void Garbage_Directives_Fall_Back_To_Text()
    {
        var index = SmallIndex();
        Assert.Empty(index.Search("size:abc", 10)); // no size named that
        Assert.Empty(index.Search("dm:xyz", 10));
        var hits = index.Search("ext:", 10); // bare prefix = literal text
        Assert.NotNull(hits);
    }

    [Fact]
    public void Huge_Query_Returns_Quickly()
    {
        var index = SmallIndex();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var hits = index.Search(new string('a', 10000), 10);
        sw.Stop();
        Assert.Empty(hits);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Unicode_Query_Does_Not_Crash()
    {
        var index = SmallIndex();
        var hits = index.Search("ünïcode", 10);
        Assert.Single(hits);
        Assert.Empty(index.Search("日本語クエリ🔍", 10));
    }

    [Fact]
    public void Corrupt_Cache_Throws_For_Caller_To_Handle()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffcor_" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(file, new byte[] { 1, 2, 3, 4, 5 });
        try
        {
            Assert.ThrowsAny<Exception>(() => Cache.Load(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Null_Bytes_And_Newlines_In_Query_Are_Safe()
    {
        var index = SmallIndex();
        Assert.Empty(index.Search("a\0b", 10));
        Assert.Empty(index.Search("a\nb", 10));
    }
}
