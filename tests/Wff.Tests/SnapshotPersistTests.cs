using Wff.Engine;

namespace Wff.Tests;

public sealed class SnapshotPersistTests
{
    [Fact]
    public void Snapshot_Save_Load_Roundtrip()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffsnap_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var snap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["C:\\work"] = 123456789L,
                ["D:\\media"] = 987654321L,
            };
            SnapshotStore.Save(snap, file);
            var back = SnapshotStore.Load(file);
            Assert.Equal(2, back.Count);
            Assert.Equal(123456789L, back["C:\\work"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Snapshot_Load_Missing_Returns_Empty()
    {
        var back = SnapshotStore.Load(Path.Combine(Path.GetTempPath(), "wffnosnap_" + Guid.NewGuid().ToString("N") + ".bin"));
        Assert.Empty(back);
    }
}
