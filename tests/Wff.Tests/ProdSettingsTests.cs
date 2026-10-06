using Wff.Engine;

namespace Wff.Tests;

public sealed class ProdSettingsTests
{
    [Fact]
    public void Defaults_Are_Sane()
    {
        var s = new Settings();
        Assert.Equal("Ctrl+Shift+F", s.MainHotkey);
        Assert.True(s.CtrlDoubleTap);
        Assert.Equal(50, s.MaxResults);
        Assert.False(s.MatchPath);
        Assert.Equal("rank", s.SortMode);
        Assert.True(s.HideHiddenAndSystem);
        Assert.Equal("System", s.Theme);
        Assert.False(s.LaunchAtLogin);
    }

    [Fact]
    public void Export_Import_Roundtrip()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffexp_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var s = new Settings { MaxResults = 25, Theme = "Dark", IgnoredExtensions = new List<string> { ".log" } };
            Settings.Export(s, file);
            var back = Settings.Import(file);
            Assert.Equal(25, back.MaxResults);
            Assert.Equal("Dark", back.Theme);
            Assert.Contains(".log", back.IgnoredExtensions);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Import_Bad_File_Throws_With_Message()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffbad_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, "{nope");
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => Settings.Import(file));
            Assert.NotEmpty(ex.Message);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Import_Null_File_Throws()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffnull_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, "null");
        try
        {
            Assert.Throws<InvalidDataException>(() => Settings.Import(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Autostart_Set_And_Clear_Test_Key()
    {
        // Uses an isolated test value name: never touches production keys.
        const string testValue = "win-fast-finder-TEST";
        Autostart.SetEnabled(true, @"C:\fake\app.exe", testValue);
        Assert.True(Autostart.IsEnabled(testValue));
        Autostart.SetEnabled(false, @"C:\fake\app.exe", testValue);
        Assert.False(Autostart.IsEnabled(testValue));
    }
}
