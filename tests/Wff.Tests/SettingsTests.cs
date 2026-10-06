using Wff.Engine;

namespace Wff.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void Save_Load_Roundtrip_Preserves_Lists()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffs_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var s = new Settings
            {
                Blacklist = new List<string> { "node_modules", "C:\\secret" },
                WhitelistRoots = new List<string> { "C:\\work" },
            };
            Settings.Save(s, file);
            var back = Settings.Load(file);
            Assert.Equal(s.Blacklist, back.Blacklist);
            Assert.Equal(s.WhitelistRoots, back.WhitelistRoots);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ResolveRoots_Uses_Whitelist_When_Set()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wffr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var s = new Settings { WhitelistRoots = new List<string> { dir } };
            Assert.Equal(new[] { dir }, s.ResolveRoots());
        }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void Load_Corrupt_File_Returns_Defaults()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffc_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, "{not json{{{");
        try
        {
            var back = Settings.Load(file);
            Assert.Empty(back.WhitelistRoots);
            Assert.Contains("node_modules", back.Blacklist);
        }
        finally { File.Delete(file); }
    }
    [Fact]
    public void Load_Missing_File_Returns_Defaults()
    {
        var back = Settings.Load(Path.Combine(Path.GetTempPath(), "wff_nope_" + Guid.NewGuid().ToString("N") + ".json"));
        Assert.NotNull(back.Blacklist);
        Assert.Empty(back.WhitelistRoots);
    }
}
