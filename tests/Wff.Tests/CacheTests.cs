using Wff.Engine;

namespace Wff.Tests;

public sealed class CacheTests
{
    [Fact]
    public void Save_Load_Roundtrip_Preserves_Paths_Metadata_And_Searches()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var big = Path.Combine(root, "big.bin");
        var small = Path.Combine(root, "small.txt");
        File.WriteAllText(big, new string('x', 20000));
        File.WriteAllText(small, "x");
        var file = Path.Combine(Path.GetTempPath(), "wff_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var index = FileIndex.Build(Walker.Enumerate(root).ToList());
            Cache.Save(index, file);
            var (paths, sizes, mtimes) = Cache.Load(file);
            var loaded = FileIndex.Build(paths, sizes, mtimes);
            Assert.Equal(index.Count, loaded.Count);
            Assert.Equal(index.Sizes, loaded.Sizes);
            Assert.Equal(index.Mtimes, loaded.Mtimes);
            // Metadata survived: size filter works without touching disk.
            var hits = loaded.Search("bin size:>10kb", 5);
            Assert.Single(hits);
            Assert.EndsWith("big.bin", hits[0]);
        }
        finally
        {
            Directory.Delete(root, true);
            File.Delete(file);
        }
    }

    [Fact]
    public void Load_Rejects_V1_Cache_So_Caller_Rebuilds()
    {
        var file = Path.Combine(Path.GetTempPath(), "wff_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs, System.Text.Encoding.UTF8))
            {
                w.Write(0x31464657u); // magic "WFF1"
                w.Write(1); // v1: paths only
                w.Write(1);
                w.Write(@"C:\d\a.txt");
            }
            Assert.Throws<InvalidDataException>(() => Cache.Load(file));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
