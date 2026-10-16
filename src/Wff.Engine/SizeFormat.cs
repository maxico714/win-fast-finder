// Human-readable file sizes for result rows ("12.4 KB", "2 MB").
// Binary units (1024), same base the size: filter uses.
namespace Wff.Engine;

public static class SizeFormat
{
    public static string FormatBytes(long bytes) => FormatBytes(bytes, false);

    public static string FormatBytes(long bytes, bool decimalUnits)
    {
        if (bytes < 0)
            bytes = 0;
        long step = decimalUnits ? 1000 : 1024;
        if (bytes < step)
            return $"{bytes} B";
        double v = bytes / (double)step;
        string unit = "KB";
        if (Math.Round(v, 1) >= step) { v /= step; unit = "MB"; }
        if (Math.Round(v, 1) >= step) { v /= step; unit = "GB"; }
        v = Math.Round(v, 1);
        return v % 1 == 0 ? $"{v:0} {unit}" : $"{v:0.#} {unit}";
    }
}
