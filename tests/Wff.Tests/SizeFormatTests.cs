using Wff.Engine;

namespace Wff.Tests;

public sealed class SizeFormatTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1L, "1 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(12 * 1024L, "12 KB")]
    [InlineData(500 * 1024L, "500 KB")]
    [InlineData(1048575L, "1 MB")]
    [InlineData(2 * 1024 * 1024L, "2 MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3 GB")]
    [InlineData(-5L, "0 B")]
    public void Formats_Binary_Units(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormat.FormatBytes(bytes));
    }
}
