using Wff.Engine;

namespace Wff.Tests;

public sealed class ParallelWalkerTests
{
    [Fact]
    public void Parallel_Matches_Sequential_And_Respects_Blacklist()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffp_" + Guid.NewGuid().ToString("N"));
        for (int d = 0; d < 20; d++)
        {
            Directory.CreateDirectory(Path.Combine(root, $"dir{d}", "sub"));
            for (int f = 0; f < 25; f++)
                File.WriteAllText(Path.Combine(root, $"dir{d}", "sub", $"f{f}.txt"), "x");
        }
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        File.WriteAllText(Path.Combine(root, "node_modules", "skip.txt"), "x");
        try
        {
            var seq = Walker.Enumerate(root, new[] { "node_modules" }).OrderBy(x => x).ToList();
            var par = Walker.EnumerateParallel(root, new[] { "node_modules" }).OrderBy(x => x).ToList();
            Assert.Equal(500, seq.Count);
            Assert.Equal(seq, par);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
