// Low-level keyboard hook for Ctrl-Ctrl double-tap.
//
// HONEST AV NOTE (accepted risk, see docs/PRODUCT.md): this installs a
// WH_KEYBOARD_LL hook, which heuristics flag as keylogger-like. By design it
// forwards ONLY Ctrl key-down timestamps to DoubleTapDetector and drops
// everything else immediately — it never records, stores, or transmits
// keystrokes. If SmartScreen/Defender flags the binary, that is expected for
// this feature; the Ctrl+Shift+F hotkey path needs no hook.
using System.Diagnostics;
using System.Runtime.InteropServices;
using Wff.Engine;

namespace Wff.App;

public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_CONTROL = 0x11;

    private static IntPtr _hook;
    private static NativeHookProc? _proc;
    private static DoubleTapDetector? _detector;
    private static Action? _onDoubleTap;

    private delegate IntPtr NativeHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, NativeHookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    public static KeyboardHook Start(DoubleTapDetector detector, Action onDoubleTap)
    {
        _detector = detector;
        _onDoubleTap = onDoubleTap;
        _proc = HookProc;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("could not install keyboard hook");
        return new KeyboardHook();
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);
            if (vk == VK_LCONTROL || vk == VK_RCONTROL || vk == VK_CONTROL)
            {
                if (_detector != null && _detector.CtrlDown(DateTime.UtcNow))
                    _onDoubleTap?.Invoke();
            }
            else
            {
                _detector?.OtherKey();
            }
        }
        else if (nCode >= 0 && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
        {
            int vk = Marshal.ReadInt32(lParam);
            if (vk == VK_LCONTROL || vk == VK_RCONTROL || vk == VK_CONTROL)
                _detector?.CtrlUp();
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}
