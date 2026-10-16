// Search-box intelligence: directive classification (for in-box keyword
// highlighting), prefix completion and value suggestions (ext: values come
// from the user's own index). UI-agnostic: MainWindow renders the results.
namespace Wff.Engine;

public sealed record Suggestion(string Insert, string Display, string Description);

public static class QueryHint
{
    public static readonly string[] Directives = { "ext:", "size:", "dm:", "path:" };

    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["ext:"] = "filter by extension, e.g. ext:pdf (repeatable)",
        ["size:"] = "filter by size, e.g. size:>10mb",
        ["dm:"] = "modified date, e.g. dm:<7d",
        ["path:"] = "match the full path, e.g. path:Downloads",
    };

    // "ext:pdf" -> ("ext:", "pdf"); "path:" -> ("path:", "");
    // "ex" / "report" -> null. Input must already be lowercased.
    public static (string Name, string Value)? SplitDirective(string tokenLower)
    {
        foreach (var d in Directives)
        {
            if (tokenLower.StartsWith(d, StringComparison.Ordinal))
                return (d, tokenLower[d.Length..]);
        }
        return null;
    }

    // First directive the prefix abbreviates ("ex" -> "ext:"). Only when the
    // prefix is a strict, non-empty prefix; complete names return null.
    public static string? CompleteName(string prefixLower)
    {
        if (prefixLower.Length == 0)
            return null;
        foreach (var d in Directives)
        {
            if (d.StartsWith(prefixLower, StringComparison.Ordinal)
                && prefixLower.Length < d.Length)
                return d;
        }
        return null;
    }

    // Logical operators for the "[" prefix: [AND] (explicit default)
    // and [OR] (either side may match).
    public static List<Suggestion> SuggestOperators(string prefixLower, int max = 8)
    {
        var out_ = new List<Suggestion>();
        var ops = new[]
        {
            ("[AND]", "explicit AND between words (the default)"),
            ("[OR]", "logical OR: either side may match"),
        };
        foreach (var (op, desc) in ops)
        {
            if (op.StartsWith(prefixLower, StringComparison.OrdinalIgnoreCase))
                out_.Add(new Suggestion(op, op, desc));
            if (out_.Count >= max)
                break;
        }
        return out_;
    }

    public static List<Suggestion> SuggestNames(string prefixLower, int max = 8)
    {
        var out_ = new List<Suggestion>();
        foreach (var d in Directives)
        {
            if (d.StartsWith(prefixLower, StringComparison.Ordinal))
                out_.Add(new Suggestion(d, d, Descriptions[d]));
            if (out_.Count >= max)
                break;
        }
        return out_;
    }

    // Distinct extensions across indexed paths, most frequent first.
    // Delegates to ExtensionScan (last-dot semantics: "archive.tar.gz"
    // suggests "gz"), so suggestions and the settings list always agree.
    public static List<(string Ext, int Count)> TopExtensions(IEnumerable<string> paths) =>
        ExtensionScan.TopExtensions(paths, 300)
            .Select(t => (t.Extension.TrimStart('.').ToLowerInvariant(), t.Count))
            .ToList();

    public static List<Suggestion> SuggestExtensions(
        IEnumerable<string> paths, string prefixLower, int max = 8)
        => FormatExtSuggestions(TopExtensions(paths), prefixLower, max);

    // Same, but over a precomputed TopExtensions list (the UI caches it so
    // typing never re-walks a 274k index).
    public static List<Suggestion> FormatExtSuggestions(
        IEnumerable<(string Ext, int Count)> top, string prefixLower, int max = 8)
    {
        var out_ = new List<Suggestion>();
        foreach (var (ext, count) in top)
        {
            if (ext.StartsWith(prefixLower, StringComparison.Ordinal))
                out_.Add(new Suggestion("ext:" + ext, ext, $"{count:N0} files"));
            if (out_.Count >= max)
                break;
        }
        return out_;
    }
}
