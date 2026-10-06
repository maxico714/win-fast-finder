// Distinct extensions present in the index, most frequent first.
// Feeds the settings checkbox list: real data, no full-drive rescan.
namespace Wff.Engine;

public static class ExtensionScan
{
    public static List<(string Extension, int Count)> TopExtensions(
        IEnumerable<string> paths, int topN)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            var ext = Path.GetExtension(p);
            if (string.IsNullOrEmpty(ext))
                continue;
            ext = ext.ToLowerInvariant();
            counts.TryGetValue(ext, out int c);
            counts[ext] = c + 1;
        }
        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topN)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }
}
