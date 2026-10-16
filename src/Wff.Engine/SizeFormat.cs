// Human-readable file sizes for result rows ("12.4 KB", "2 MB").
// Binary units (1024), same base the size: filter uses.
namespace Wff.Engine;

public static class SizeFormat
{
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            bytes = 0;
        if (bytes < 1024)
            return $"{bytes} B";
        double v = bytes / 1024.0;
        string unit = "KB";
        if (Math.Round(v, 1) >= 1024) { v /= 1024; unit = "MB"; }
        if (Math.Round(v, 1) >= 1024) { v /= 1024; unit = "GB"; }
        v = Math.Round(v, 1);
        return v % 1 == 0 ? $"{v:0} {unit}" : $"{v:0.#} {unit}";
    }
}
