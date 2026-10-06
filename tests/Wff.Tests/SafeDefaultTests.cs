using Wff.Engine;

namespace Wff.Tests;

public sealed class SafeDefaultTests
{
    [Fact]
    public void Defaults_Skip_AppData_Cache_Noise()
    {
        var s = new Settings();
        Assert.Contains("AppData", s.Blacklist);
    }

    [Fact]
    public void Old_Settings_File_Gets_AppData_Migrated()
    {
        var file = Path.Combine(Path.GetTempPath(), "wffmig_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, "{\"Blacklist\":[\"node_modules\"],\"WhitelistRoots\":[]}");
        try
        {
            var back = Settings.Load(file);
            Assert.Contains("AppData", back.Blacklist);
            Assert.Contains("node_modules", back.Blacklist);
        }
        finally
        {
            File.Delete(file);
        }
    }
    [Fact]
    public void Logger_Writes_Timestamped_Lines_Without_Throwing()
    {
        var file = Path.Combine(Path.GetTempPath(), "wfflog_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            Logger.Init(file);
            Logger.Info("hello-test");
            var text = File.ReadAllText(file);
            Assert.Contains("hello-test", text);
        }
        finally
        {
            Logger.Shutdown();
            File.Delete(file);
        }
    }
}
