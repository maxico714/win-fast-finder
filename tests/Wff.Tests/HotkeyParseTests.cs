using Wff.Engine;

namespace Wff.Tests;

public sealed class HotkeyParseTests
{
    [Fact]
    public void Parses_Ctrl_Shift_F()
    {
        Assert.True(Hotkey.TryParse("Ctrl+Shift+F", out uint mods, out uint vk));
        Assert.Equal((uint)0x06, mods); // Ctrl|Shift
        Assert.Equal((uint)0x46, vk);
    }

    [Fact]
    public void Parses_Alt_Space()
    {
        Assert.True(Hotkey.TryParse("Alt+Space", out _, out uint vk));
        Assert.Equal((uint)0x20, vk);
    }

    [Fact]
    public void Rejects_Empty_And_Keyless()
    {
        Assert.False(Hotkey.TryParse("", out _, out _));
        Assert.False(Hotkey.TryParse("Ctrl+Shift+", out _, out _));
        Assert.False(Hotkey.TryParse("F", out _, out _)); // modifier required
    }
}
