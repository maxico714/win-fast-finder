using Wff.Engine;

namespace Wff.Tests;

public sealed class DoubleTapHoldTests
{
    [Fact]
    public void Holding_Ctrl_Down_Does_Not_Fire()
    {
        var d = new DoubleTapDetector(maxGapMs: 400);
        var t = DateTime.UtcNow;
        Assert.False(d.CtrlDown(t)); // press
        Assert.False(d.CtrlDown(t.AddMilliseconds(50))); // auto-repeat while held
        Assert.False(d.CtrlDown(t.AddMilliseconds(100)));
        Assert.False(d.CtrlDown(t.AddMilliseconds(500))); // still held: never fires
    }

    [Fact]
    public void Press_Release_Press_Fires()
    {
        var d = new DoubleTapDetector(maxGapMs: 400);
        var t = DateTime.UtcNow;
        Assert.False(d.CtrlDown(t));
        d.CtrlUp();
        Assert.True(d.CtrlDown(t.AddMilliseconds(200)));
    }

    [Fact]
    public void Press_Release_Wait_Press_Does_Not_Fire()
    {
        var d = new DoubleTapDetector(maxGapMs: 400);
        var t = DateTime.UtcNow;
        Assert.False(d.CtrlDown(t));
        d.CtrlUp();
        Assert.False(d.CtrlDown(t.AddMilliseconds(900)));
    }
}
