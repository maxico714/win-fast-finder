// Double-tap detector: two Ctrl key-downs within maxGapMs (no other key
// between) fire once. Pure logic — the OS hook only forwards Ctrl timestamps
// and never records keystrokes (privacy: no keylogging surface).
namespace Wff.Engine;

public sealed class DoubleTapDetector
{
    private readonly int _maxGapMs;
    private DateTime? _first;
    private bool _isDown;

    public DoubleTapDetector(int maxGapMs = 400)
    {
        _maxGapMs = maxGapMs;
    }

    public bool CtrlDown(DateTime utcNow)
    {
        if (_isDown)
            return false; // auto-repeat while held: ignore
        _isDown = true;
        if (_first != null && (utcNow - _first.Value).TotalMilliseconds <= _maxGapMs)
        {
            _first = null;
            return true;
        }
        _first = utcNow;
        return false;
    }

    public void CtrlUp() => _isDown = false;

    public void OtherKey()
    {
        _first = null;
        _isDown = false;
    }
}
