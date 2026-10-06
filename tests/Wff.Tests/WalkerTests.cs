using Wff.Engine;

namespace Wff.Tests;

public sealed class WalkerTests
{
    [Fact]
    public void Enumerate_Skips_Blacklisted_Dirs()
    {
        var root = Path.Combine(Path.GetTempPath(), "wff_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "code"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules", "dep"));
        File.WriteAllText(Path.Combine(root, "code", "a.txt"), "x");
        File.WriteAllText(Path.Combine(root, "node_modules", "dep", "b.txt"), "x");
        try
        {
            var files = Walker.Enumerate(root, new[] { "node_modules" }).ToList();
            Assert.Contains(files, f => f.EndsWith("a.txt"));
            Assert.DoesNotContain(files, f => f.EndsWith("b.txt"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
