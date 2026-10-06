// Post-sort for name/date/size modes. One FileInfo stat per hit (top-N
// only, ms-scale). Missing files sort last, never crash.
namespace Wff.Engine;

public static class Sorter
{
    public static List<string> Apply(List<string> hits, string sort)
    {
        if (sort == "name")
        {
            var copy = hits.ToList();
            copy.Sort(StringComparer.OrdinalIgnoreCase);
            return copy;
        }
        if (sort == "date" || sort == "size")
        {
            var stats = new List<(string Path, long Size, long Ticks, bool Ok)>(hits.Count);
            foreach (var h in hits)
            {
                try
                {
                    var fi = new FileInfo(h);
                    stats.Add((h, fi.Length, fi.LastWriteTimeUtc.Ticks, true));
                }
                catch
                {
                    stats.Add((h, -1, -1, false));
                }
            }
            if (sort == "size")
                stats.Sort((a, b) => b.Size.CompareTo(a.Size));
            else
                stats.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
            return stats.Select(s => s.Path).ToList();
        }
        return hits; // rank: engine order
    }
}
