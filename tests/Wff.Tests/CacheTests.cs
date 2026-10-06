using Wff.Engine;

namespace Wff.Tests;

public sealed class CacheTests
{
    [Fact]
    public void Save_Load_Roundtrip_Preserves_Paths_And_Searches()
    {
        var paths = new List<string>();
        for (int i = 0; i < 5000; i++)
            paths.Add($"C:\\d\\report_{i:D5}.txt");
        paths.Add("C:\\d\\final Notes ünïcode.docx");
        var file = Path.Combine(Path.GetTempPath(), "wff_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var index = FileIndex.Build(paths);
            Cache.Save(index.Paths, file);
            var loaded = FileIndex.Build(Cache.Load(file));
            Assert.Equal(paths.Count, loaded.Count);
            var hits = loaded.Search("report", 5);
            Assert.NotEmpty(hits);
            Assert.Contains(loaded.Search("ünïcode", 5), h => h.EndsWith(".docx"));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
