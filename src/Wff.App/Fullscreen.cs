// Fullscreen guard: ignore hotkeys while a borderless full-screen window
// (game/video) owns the foreground. Interactive-only; not unit-tested.
using System.Runtime.InteropServices;

namespace Wff.App;

public static class Fullscreen
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000; // title bar + border

    // True exclusive/borderless fullscreen (game, video) — NOT a merely
    // maximized window (those keep their caption and must not block us).
    public static bool IsForegroundFullscreen()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return false;
            if (!GetWindowRect(hwnd, out var r))
                return false;
            var work = System.Windows.SystemParameters.WorkArea;
            bool coversAll = r.Left <= work.Left && r.Top <= work.Top
                && r.Right >= work.Right && r.Bottom >= work.Bottom;
            if (!coversAll)
                return false;
            int style = GetWindowLong(hwnd, GWL_STYLE);
            return (style & WS_CAPTION) == 0;
        }
        catch
        {
            return false;
        }
    }
}
