using Wff.Engine;

namespace Wff.Tests;

public sealed class RuntimeCheckTests
{
    [Fact]
    public void Satisfies_Accepts_Only_Major_8()
    {
        Assert.True(RuntimeCheck.Satisfies(new[] { "8.0.28", "7.0.20" }));
        Assert.True(RuntimeCheck.Satisfies(new[] { "8.0.28", "9.0.5" }));
        // The app targets .NET 8 with no rollForward: 9+ cannot run it.
        Assert.False(RuntimeCheck.Satisfies(new[] { "9.0.5" }));
        Assert.False(RuntimeCheck.Satisfies(new[] { "10.0.0" }));
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
