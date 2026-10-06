using Wff.Engine;

namespace Wff.Tests;

public sealed class DoubleTapTests
{
    [Fact]
    public void Two_Quick_Ctrl_Taps_Fire()
    {
        var d = new DoubleTapDetector(maxGapMs: 400);
        var t = DateTime.UtcNow;
        Assert.False(d.CtrlDown(t));
        d.CtrlUp();
        Assert.True(d.CtrlDown(t.AddMilliseconds(200)));
    }

    [Fact]
    public void Slow_Second_Tap_Does_Not_Fire()
    {
        var d = new DoubleTapDetector(maxGapMs: 400);
        var t = DateTime.UtcNow;
        Assert.False(d.CtrlDown(t));
        d.CtrlUp();
        Assert.False(d.CtrlDown(t.AddMilliseconds(900)));
    }

    [Fact]
    public void Other_Key_Resets_Sequence()
    {
        var d = new DoubleTapDetector(maxGapMs: 400);
        var t = DateTime.UtcNow;
        Assert.False(d.CtrlDown(t));
        d.OtherKey();
        Assert.False(d.CtrlDown(t.AddMilliseconds(100)));
    }
}
