// Hotkey strings like "Ctrl+Shift+F" / "Alt+Space" -> RegisterHotKey codes.
// A modifier is required (bare keys would hijack typing).
namespace Wff.Engine;

public static class Hotkey
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public static bool TryParse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win": case "windows": modifiers |= MOD_WIN; break;
                default: return false;
            }
        }
        if (modifiers == 0)
            return false;
        return TryKey(parts[^1], out vk);
    }

    private static bool TryKey(string key, out uint vk)
    {
        vk = 0;
        var k = key.ToUpperInvariant();
        if (k.Length == 1 && k[0] >= 'A' && k[0] <= 'Z') { vk = k[0]; return true; }
        if (k.Length == 1 && k[0] >= '0' && k[0] <= '9') { vk = (uint)k[0]; return true; }
        if (k == "SPACE") { vk = 0x20; return true; }
        if (k.StartsWith("F") && int.TryParse(k[1..], out int f) && f >= 1 && f <= 24)
        {
            vk = (uint)(0x6F + f);
            return true;
        }
        return false;
    }
}
