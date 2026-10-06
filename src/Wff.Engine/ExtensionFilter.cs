// Index-time extension ignore: ".tmp" or "tmp", case-insensitive.
namespace Wff.Engine;

public static class ExtensionFilter
{
    public static bool Keep(string path, IEnumerable<string> ignored)
    {
        foreach (var raw in ignored)
        {
            var e = raw.StartsWith('.') ? raw : "." + raw;
            if (path.EndsWith(e, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}
