using Wff.Engine;

namespace Wff.Tests;

public sealed class RuntimeCheckTests
{
    [Fact]
    public void Satisfies_Accepts_8x_And_9x()
    {
        Assert.True(RuntimeCheck.Satisfies(new[] { "8.0.28", "7.0.20" }));
        Assert.True(RuntimeCheck.Satisfies(new[] { "9.0.5" }));
        Assert.False(RuntimeCheck.Satisfies(new[] { "7.0.20", "6.0.36" }));
        Assert.False(RuntimeCheck.Satisfies(Array.Empty<string>()));
    }

    [Fact]
    public void Host_Machine_Has_Desktop_Runtime()
    {
        // Guard: this repo only runs/tests on .NET 8+.
        Assert.True(RuntimeCheck.IsDotNet8DesktopPresent());
    }
}
