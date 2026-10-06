using Wff.Engine;

namespace Wff.Tests;

public sealed class StartupTests
{    [Fact]
    public void StartupRoots_Defaults_To_User_Profile()
    {
        var s = new Settings { WhitelistRoots = new List<string>() };
        var roots = s.StartupRoots();
        Assert.Single(roots);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), roots[0]);
    }

    [Fact]
    public void Parallel_Reports_Progress_Ending_At_Total()
    {
        var root = Path.Combine(Path.GetTempPath(), "wffg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        for (int f = 0; f < 30; f++)
            File.WriteAllText(Path.Combine(root, "sub", $"f{f}.txt"), "x");
        try
        {
            int last = -1;
            var prog = new InlineProgress(v => last = v);
            var files = Walker.EnumerateParallel(root, null, prog);
            Assert.Equal(30, files.Count);
            Assert.Equal(30, last);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // Synchronous reporter: avoids Progress<T>'s async post (flaky in tests).
    private sealed class InlineProgress : IProgress<int>
    {
        private readonly Action<int> _onReport;
        public InlineProgress(Action<int> onReport) => _onReport = onReport;
        public void Report(int value) => _onReport(value);
    }
}
