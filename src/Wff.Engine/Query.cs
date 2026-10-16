// Query syntax: free text + ext: + size: + dm: + path: directives.
// Free-text terms are ANDed (any order). A token that LOOKS like a
// directive but does not parse is reported in Invalid (with a hint)
// instead of silently becoming filename text: ':' is illegal in Windows
// filenames, so such a token could never match anything real.
using System.Globalization;

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
    // DisplayedSize bare values: match when the file's displayed size
    // (SizeFormat, same unit base) equals this string.
    public string? SizeDisplayTarget { get; init; }
    public bool SizeDecimalUnits { get; init; }
    public DateTime? ModifiedAfterUtc { get; init; }
    public DateTime? ModifiedBeforeUtc { get; init; }
    // Tokens that look like directives but failed to parse, with hints.
    public List<InvalidToken> Invalid { get; } = new();
}

public sealed record InvalidToken(string Token, string Message);

public sealed class QueryOptions
{
    // ExactBytes: bare size: means that exact byte count.
    // DisplayedSize: bare size: means "displays as", like Explorer.
    // Percent1: bare size: means within +/-1%.
    public string ExactSizeMode { get; set; } = "DisplayedSize";
    public bool SizeDecimalUnits { get; set; } = false;
    public bool DateUseLocalTime { get; set; } = true;
    public static QueryOptions Default => new();
}

public static class QueryParser
{
    public static Query Parse(string input) => Parse(input, null);

    public static Query Parse(string input, QueryOptions? options)
    {
        options ??= QueryOptions.Default;
        var text = new List<string>();
        var q = new Query { SizeDecimalUnits = options.SizeDecimalUnits };
        var exts = new List<string>();
        var globList = new List<string>();
        bool matchPath = false;
        long? min = null, max = null;
        string? displayTarget = null;
        DateTime? after = null, before = null;

        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string tok = tokens[i];
            string t = tok.ToLowerInvariant();
            if (t == "path:")
            {
                matchPath = true;
                continue;
            }
            if (t.StartsWith("path:") && t.Length > 5)
            {
                // path:Downloads == Downloads + full-path mode. Value is
                // plain text (no wildcards); matched as subsequence.
                matchPath = true;
                text.Add(tok[5..]);
                continue;
            }
            if (t.StartsWith("ext:"))
            {
                string v = t[4..].TrimStart('.');
                if (v.Length == 0)
                    q.Invalid.Add(new InvalidToken(tok, $"{tok} — try ext:pdf"));
                else if (v.Contains('*') || v.Contains('?'))
                    q.Invalid.Add(new InvalidToken(tok, $"{tok} — extensions can't contain wildcards"));
                else
                    exts.Add(v);
                continue;
            }
            if (t.StartsWith("size:"))
            {
                string v = t[5..];
                // "size:>1 gb": a bare-unit next token belongs to this value.
                if (i + 1 < tokens.Length && IsBareNumber(v)
                    && IsPureUnit(tokens[i + 1].ToLowerInvariant()))
                {
                    v += tokens[i + 1].ToLowerInvariant();
                    i++;
                }
                if (TrySize(v, options, out var lo, out var hi, out var disp, out var err))
                {
                    if (lo != null) min = lo;
                    if (hi != null) max = hi;
                    if (disp != null) displayTarget = disp;
                }
                else
                {
                    q.Invalid.Add(new InvalidToken(tok, $"{tok} — {err}"));
                }
                continue;
            }
            if (t.StartsWith("dm:"))
            {
                string v = t[3..];
                if (TryDays(v, options, out var dafter, out var dbefore, out var err))
                {
                    if (dafter != null) after = dafter;
                    if (dbefore != null) before = dbefore;
                }
                else
                {
                    q.Invalid.Add(new InvalidToken(tok, $"{tok} — {err}"));
                }
                continue;
            }
            if (Glob.IsGlob(t))
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
            SizeDisplayTarget = displayTarget,
            ModifiedAfterUtc = after,
            ModifiedBeforeUtc = before,
            MatchPath = matchPath,
        };
    }

    private static bool IsBareNumber(string v)
    {
        // Operator + digits, no unit yet: ">1", "<=2.5", "=3".
        var s = v;
        if (s.StartsWith(">=") || s.StartsWith("<="))
            s = s[2..];
        else if (s.StartsWith('>') || s.StartsWith('<') || s.StartsWith('='))
            s = s.Length > 1 ? s[1..] : "";
        return s.Length > 0 && double.TryParse(s,
            NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsPureUnit(string t) =>
        t is "b" or "k" or "kb" or "m" or "mb" or "g" or "gb" or "t" or "tb";

    private static double UnitMultiplier(string unit, bool decimalUnits) => unit switch
    {
        "b" => 1,
        "k" or "kb" => decimalUnits ? 1000 : 1024,
        "m" or "mb" => decimalUnits ? 1000 * 1000 : 1024 * 1024,
        "g" or "gb" => decimalUnits ? 1000.0 * 1000 * 1000 : 1024.0 * 1024 * 1024,
        "t" or "tb" => decimalUnits ? 1000.0 * 1000 * 1000 * 1000 : 1024.0 * 1024 * 1024 * 1024,
        _ => -1,
    };

    private static bool TrySize(string s, QueryOptions options,
        out long? lo, out long? hi, out string? display, out string error)
    {
        lo = hi = null;
        display = null;
        error = "try size:>10mb, size:1mb..5mb or size:500kb";
        if (s.Length == 0)
            return false;
        // Range: "1mb..5mb" (plain values, inclusive, order-free).
        int dots = s.IndexOf("..", StringComparison.Ordinal);
        if (dots >= 0)
        {
            string left = s[..dots], right = s[(dots + 2)..];
            if (left.Length == 0 || right.Length == 0
                || !TryPlainSize(left, options, out var a, out _)
                || !TryPlainSize(right, options, out var b, out _))
                return false;
            lo = Math.Min(a, b);
            hi = Math.Max(a, b);
            return true;
        }
        string op = "";
        string rest = s;
        foreach (var cand in new[] { ">=", "<=", "=", ">", "<" })
        {
            if (rest.StartsWith(cand, StringComparison.Ordinal))
            {
                op = cand;
                rest = rest[cand.Length..];
                break;
            }
        }
        if (!TryPlainSize(rest, options, out var n, out _))
            return false;
        switch (op)
        {
            case ">":
                if (n >= long.MaxValue) return false;
                lo = n + 1;
                break;
            case "<":
                if (n <= 0) return false;
                hi = n - 1;
                break;
            case ">=": lo = n; break;
            case "<=": hi = n; break;
            case "=": lo = hi = n; break;
            default:
                // Bare value: meaning comes from the ExactSizeMode setting.
                if (options.ExactSizeMode == "Percent1")
                {
                    lo = (long)Math.Floor(n * 0.99);
                    hi = (long)Math.Ceiling(n * 1.01);
                }
                else if (options.ExactSizeMode == "DisplayedSize")
                {
                    display = SizeFormat.FormatBytes(n, options.SizeDecimalUnits);
                }
                else
                {
                    lo = hi = n;
                }
                break;
        }
        return true;
    }

    // Plain value: digits with optional decimal and unit, default bytes.
    private static bool TryPlainSize(string s, QueryOptions options,
        out long n, out string error)
    {
        n = 0;
        error = "try size:>10mb, size:1mb..5mb or size:500kb";
        if (s.Length == 0)
            return false;
        string num = s, unit = "b";
        int k = 0;
        while (k < s.Length && (char.IsAsciiDigit(s[k]) || s[k] == '.'))
            k++;
        if (k == 0)
            return false;
        num = s[..k];
        if (k < s.Length)
            unit = s[k..].ToLowerInvariant();
        double mult = UnitMultiplier(unit, options.SizeDecimalUnits);
        if (mult < 0)
            return false;
        if (!double.TryParse(num, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var v) || v < 0)
            return false;
        double bytes = v * mult;
        if (double.IsInfinity(bytes) || double.IsNaN(bytes) || bytes > long.MaxValue)
            return false;
        n = (long)Math.Round(bytes, MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool TryDays(string s, QueryOptions options,
        out DateTime? after, out DateTime? before, out string error)
    {
        after = before = null;
        error = "try dm:<7d, dm:today or dm:2024-05-10";
        if (s.Length == 0)
            return false;
        // Range: "2024-05-01..2024-05-31" or "7d..30d".
        int dots = s.IndexOf("..", StringComparison.Ordinal);
        if (dots >= 0)
        {
            string left = s[..dots], right = s[(dots + 2)..];
            if (!TryDayBound(left, options, out var a0, out var a1, out _)
                || !TryDayBound(right, options, out var b0, out var b1, out _))
                return false;
            // Range covers every bound instant: after = earliest,
            // before = latest. Works for absolute, relative and mixed sides.
            var bounds = new[] { a0, a1, b0, b1 }
                .Where(d => d != null).Select(d => d!.Value).ToList();
            if (bounds.Count < 2)
                return false;
            after = bounds.Min();
            before = bounds.Max();
            return true;
        }
        string op = "";
        string rest = s;
        foreach (var cand in new[] { ">=", "<=", "=", ">", "<" })
        {
            if (rest.StartsWith(cand, StringComparison.Ordinal))
            {
                op = cand;
                rest = rest[cand.Length..];
                break;
            }
        }
        if (!TryDayBound(rest, options, out var start, out var end, out _))
            return false;
        if (start != null && end != null)
        {
            // Whole-day bound (absolute date, today, yesterday).
            switch (op)
            {
                case ">": case ">=": after = end; break;
                case "<": case "<=": before = start; break;
                default: after = start; before = end; break;
            }
        }
        else if (start == null && end != null)
        {
            // Relative cutoff instant (end set, start null).
            switch (op)
            {
                case ">": case ">=": before = end; break;
                default: after = end; break; // "<", "<=", "=", bare: within
            }
        }
        else
        {
            return false;
        }
        return true;
    }

    // One date bound: returns (rangeStart, rangeEnd). Relative cutoffs come
    // back as (null, instant); whole days as (dayStartUtc, dayEndUtc).
    private static bool TryDayBound(string s, QueryOptions options,
        out DateTime? start, out DateTime? end, out string error)
    {
        start = end = null;
        error = "try dm:<7d, dm:today or dm:2024-05-10";
        if (s.Length == 0)
            return false;
        bool local = options.DateUseLocalTime;
        string low = s.ToLowerInvariant();
        if (low is "today" or "yesterday")
        {
            DateTime baseDay = local ? DateTime.Today : DateTime.UtcNow.Date;
            if (low == "yesterday")
                baseDay = baseDay.AddDays(-1);
            DateTime sUtc = local
                ? new DateTime(baseDay.Year, baseDay.Month, baseDay.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime()
                : new DateTime(baseDay.Year, baseDay.Month, baseDay.Day, 0, 0, 0, DateTimeKind.Utc);
            start = sUtc;
            end = sUtc.AddDays(1);
            return true;
        }
        if (low.Length == 10 && low[4] == '-' && low[7] == '-'
            && DateTime.TryParseExact(low, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day))
        {
            DateTime sUtc = local
                ? new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime()
                : new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Utc);
            start = sUtc;
            end = sUtc.AddDays(1);
            return true;
        }
        // Relative: number + unit (h/d/w/m/y), bare digits mean days.
        string num = low, unit = "d";
        int k = 0;
        while (k < low.Length && (char.IsAsciiDigit(low[k]) || low[k] == '.'))
            k++;
        if (k == 0)
            return false;
        num = low[..k];
        if (k < low.Length)
            unit = low[k..];
        double hours = unit switch
        {
            "h" => 1,
            "d" => 24,
            "w" => 24 * 7,
            "m" => 24 * 30,
            "y" => 24 * 365,
            _ => -1,
        };
        if (hours < 0)
            return false;
        if (!double.TryParse(num, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var v) || v < 0)
            return false;
        end = DateTime.UtcNow - TimeSpan.FromHours(v * hours);
        return true;
    }
}
