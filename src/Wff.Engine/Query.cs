// Query syntax: free text + `ext:pdf` (repeatable, OR) + `size:>1kb` /
// `size:<2mb` + `dm:<7d` (modified within 7 days) / `dm:>30d` (older).
namespace Wff.Engine;

public sealed record Query
{
    public string Text { get; init; } = "";
    public List<string> Globs { get; } = new();
    public List<string> Extensions { get; } = new();
    public bool MatchPath { get; init; } = false;
    public string Sort { get; init; } = "rank"; // rank|name|date|size
    public long? MinSizeBytes { get; init; }
    public long? MaxSizeBytes { get; init; }
    public DateTime? ModifiedAfterUtc { get; init; }
    public DateTime? ModifiedBeforeUtc { get; init; }
}

public static class QueryParser
{
    public static Query Parse(string input)
    {
        var text = new List<string>();
        var q = new Query();
        var exts = new List<string>();
        var globList = new List<string>();
        bool matchPath = false;
        long? min = null, max = null;
        DateTime? after = null, before = null;

        foreach (var tok in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = tok.ToLowerInvariant();
            if (t == "path:")
                matchPath = true;
            else if (t.StartsWith("ext:") && t.Length > 4)
                exts.Add(t[4..].TrimStart('.'));
            else if (t.StartsWith("size:") && TrySize(t[5..], out var lo, out var hi))
            { if (lo != null) min = lo; if (hi != null) max = hi; }
            else if (t.StartsWith("dm:") && TryDays(t[3..], out var dafter, out var dbefore))
            { if (dafter != null) after = dafter; if (dbefore != null) before = dbefore; }
            else if (Glob.IsGlob(t))
                globList.Add(t);
            else
                text.Add(tok);
        }
        q.Extensions.AddRange(exts);
        q.Globs.AddRange(globList);
        return q with
        {
            Text = string.Join(' ', text),
            MinSizeBytes = min,
            MaxSizeBytes = max,
            ModifiedAfterUtc = after,
            ModifiedBeforeUtc = before,
            MatchPath = matchPath,
        };
    }

    private static bool TrySize(string s, out long? lo, out long? hi)
    {
        lo = hi = null;
        bool gt = s.StartsWith(">");
        bool lt = s.StartsWith("<");
        var num = gt || lt ? s[1..] : s;
        long mult = 1;
        if (num.EndsWith("kb")) { mult = 1024; num = num[..^2]; }
        else if (num.EndsWith("mb")) { mult = 1024 * 1024; num = num[..^2]; }
        else if (num.EndsWith("gb")) { mult = 1024L * 1024 * 1024; num = num[..^2]; }
        else if (num.EndsWith("b")) { num = num[..^1]; }
        if (!long.TryParse(num, out var v) || v < 0)
            return false;
        v *= mult;
        if (gt) lo = v;
        else if (lt) hi = v;
        else { lo = v; hi = v; }
        return true;
    }

    private static bool TryDays(string s, out DateTime? after, out DateTime? before)
    {
        after = before = null;
        bool lt = s.StartsWith("<"); // dm:<7d = within last 7 days
        bool gt = s.StartsWith(">");
        var num = lt || gt ? s[1..] : s;
        if (num.EndsWith("d")) num = num[..^1];
        if (!int.TryParse(num, out var d) || d < 0)
            return false;
        var cut = DateTime.UtcNow - TimeSpan.FromDays(d);
        if (lt) after = cut;
        else if (gt) before = cut;
        else after = cut;
        return true;
    }
}
