using Wff.Engine;

namespace Wff.Tests;

public sealed class IgnoreOptionTests
{
    [Fact]
    public void Ignored_Drive_Removes_Whitelisted_Root()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wffig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var s = new Settings
            {
                WhitelistRoots = new List<string> { dir },
                IgnoredDrives = new List<string> { dir },
            };
            Assert.Empty(s.StartupRoots());
        }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void Full_Path_Blacklist_Skips_Only_That_Subtree()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffp_" + Guid.NewGuid().ToString("N"));
        var skip = Path.Combine(root, "skipme");
        var keep = Path.Combine(root, "keepme");
        Directory.CreateDirectory(skip);
        Directory.CreateDirectory(keep);
        File.WriteAllText(Path.Combine(skip, "a.txt"), "x");
        File.WriteAllText(Path.Combine(keep, "b.txt"), "x");
        try
        {
            var files = Walker.Enumerate(root, new[] { skip }).ToList();
            Assert.DoesNotContain(files, f => f.EndsWith("a.txt"));
            Assert.Contains(files, f => f.EndsWith("b.txt"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Ignored_Extensions_Drop_Matching_Files()
    {
        Assert.False(ExtensionFilter.Keep("C:\\d\\a.tmp", new[] { ".tmp", ".log" }));
        Assert.False(ExtensionFilter.Keep("C:\\d\\a.LOG", new[] { ".tmp", ".log" }));
        Assert.True(ExtensionFilter.Keep("C:\\d\\a.pdf", new[] { ".tmp", ".log" }));
        Assert.True(ExtensionFilter.Keep("C:\\d\\a.tmp", new List<string>()));
    }
}
