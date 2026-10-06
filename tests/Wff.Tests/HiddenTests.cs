using Wff.Engine;

namespace Wff.Tests;

public sealed class HiddenTests
{
    private static string MakeTree(out string hiddenFile, out string normalFile)
    {
        var root = Path.Combine(Path.GetTempPath(), "wffh_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        hiddenFile = Path.Combine(root, "secret.txt");
        normalFile = Path.Combine(root, "normal.txt");
        File.WriteAllText(hiddenFile, "x");
        File.WriteAllText(normalFile, "x");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);
        return root;
    }

    [Fact]
    public void Sequential_Skips_Hidden_By_Default()
    {
        var root = MakeTree(out var hidden, out var normal);
        try
        {
            var files = Walker.Enumerate(root).ToList();
            Assert.DoesNotContain(hidden, files);
            Assert.Contains(normal, files);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Parallel_Includes_Hidden_When_Opted_Out()
    {
        var root = MakeTree(out var hidden, out var normal);
        try
        {
            var files = Walker.EnumerateParallel(root, null, null, skipHiddenSystem: false);
            Assert.Contains(hidden, files);
            Assert.Contains(normal, files);
        }
        finally { Directory.Delete(root, true); }
    }
}
