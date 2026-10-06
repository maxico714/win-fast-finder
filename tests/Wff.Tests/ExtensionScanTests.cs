using Wff.Engine;

namespace Wff.Tests;

public sealed class ExtensionScanTests
{
    [Fact]
    public void TopExtensions_Counts_And_Sorts()
    {
        var paths = new List<string>
        {
            "C:\\d\\a.pdf", "C:\\d\\b.pdf", "C:\\d\\c.PDF",
            "C:\\d\\d.txt", "C:\\d\\noext", "C:\\d\\e.tmp",
        };
        var top = ExtensionScan.TopExtensions(paths, 10);
        Assert.Equal(".pdf", top[0].Extension);
        Assert.Equal(3, top[0].Count);
        Assert.DoesNotContain(top, t => t.Extension == "");
    }

    [Fact]
    public void TopExtensions_Respects_Limit()
    {
        var paths = new List<string> { "C:\\d\\a.pdf", "C:\\d\\b.txt", "C:\\d\\c.log" };
        Assert.Equal(2, ExtensionScan.TopExtensions(paths, 2).Count);
    }
}
