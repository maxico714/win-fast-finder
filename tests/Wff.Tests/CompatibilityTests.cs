using Wff.Engine;

namespace Wff.Tests;

public sealed class CompatibilityTests
{
    [Theory]
    [InlineData(6, 1, 7601, true)] // Win7 x64: too old
    [InlineData(10, 0, 10240, true)] // Win10 1507: too old
    [InlineData(10, 0, 14392, true)] // one build short of 1607
    [InlineData(10, 0, 14393, true)] // Win10 1607 x64: supported
    [InlineData(10, 0, 19045, true)] // Win10 22H2
    [InlineData(10, 0, 22000, true)] // Win11
    [InlineData(10, 0, 22631, true)] // Win11 23H2
    [InlineData(10, 0, 22631, false)] // 32-bit: unsupported
    public void IsOsSupported_Matches_Documented_Floor(
        int major, int minor, int build, bool x64)
    {
        bool expected = x64 && new Version(major, minor, build) >= new Version(10, 0, 14393);
        Assert.Equal(expected, Compatibility.IsOsSupported(new Version(major, minor, build), x64));
    }

    [Fact]
    public void Host_Machine_Passes_Os_Check()
    {
        Assert.True(Compatibility.CheckOs().Ok);
    }

    [Fact]
    public void CheckAll_Returns_Issue_List()
    {
        var issues = Compatibility.CheckAll();
        Assert.NotNull(issues);
    }
}
