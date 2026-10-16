// win-fast-finder engine: zero-dependency in-memory filename index.
// Two-phase search: bigram buckets narrow (C#-speed loops) -> subsequence
// score only candidates -> top-N heap. Never scores all rows per keystroke.
namespace Wff.Engine;

public sealed record ResultRow(string Path, long SizeBytes, long ModifiedUtcTicks);

public sealed class FileIndex
{
    private readonly string[] _paths;
    private readonly string[] _names;
    private readonly long[] _sizes; // bytes, -1 when the file is gone/unreadable
    private readonly long[] _mtimes; // UTC ticks, 0 when unknown
    private readonly Dictionary<int, int[]> _buckets;

    private FileIndex(string[] paths, string[] names, long[] sizes, long[] mtimes,
        Dictionary<int, int[]> buckets)
    {
        _paths = paths;
        _names = names;
        _sizes = sizes;
        _mtimes = mtimes;
        _buckets = buckets;
    }

    public int Count => _paths.Length;

    public IReadOnlyList<string> Paths => _paths;

    public IReadOnlyList<long> Sizes => _sizes;

    public IReadOnlyList<long> Mtimes => _mtimes;

    public static FileIndex Build(IEnumerable<string> paths) =>
        Build(paths.ToArray(), null, null);

    // Direct build from cached metadata (no syscalls). Lengths must match.
    public static FileIndex Build(IReadOnlyList<string> paths, long[] sizes, long[] mtimes)
    {
        if (sizes.Length != paths.Count || mtimes.Length != paths.Count)
            throw new ArgumentException("metadata length must match path count");
        return Build(paths.ToArray(), sizes, mtimes);
    }

    private static FileIndex Build(string[] pathArr, long[]? sizes, long[]? mtimes)
    {
        var nameArr = new string[pathArr.Length];
        var sizeArr = sizes ?? new long[pathArr.Length];
        var timeArr = mtimes ?? new long[pathArr.Length];
        var tmp = new Dictionary<int, List<int>>();
        for (int i = 0; i < pathArr.Length; i++)
        {
            var name = ExtractName(pathArr[i]).ToLowerInvariant();
            nameArr[i] = name;
            if (sizes == null)
            {
                // One stat per file at index time (never per keystroke):
                // missing/unreadable files get unknown markers and are
                // excluded whenever a size/date filter is active.
                sizeArr[i] = -1;
                timeArr[i] = 0;
                if (File.Exists(pathArr[i]))
                {
                    try
                    {
                        var fi = new FileInfo(pathArr[i]);
                        sizeArr[i] = fi.Length;
                        timeArr[i] = fi.LastWriteTimeUtc.Ticks;
                    }
                    catch { }
                }
            }
            foreach (var bg in Bigrams(name))
            {
                if (!tmp.TryGetValue(bg, out var list))
                    tmp[bg] = list = new List<int>();
                list.Add(i);
            }
        }
        var buckets = new Dictionary<int, int[]>(tmp.Count);
        foreach (var kv in tmp)
            buckets[kv.Key] = kv.Value.ToArray();
        return new FileIndex(pathArr, nameArr, sizeArr, timeArr, buckets);
    }

    public List<string> Search(string query, int topN) =>
        Search(QueryParser.Parse(query), topN);

    public List<string> Search(Query query, int topN) =>
        SearchDetailed(query, topN).Rows.Select(r => r.Path).ToList();

    public (List<ResultRow> Rows, int Total) SearchDetailed(Query query, int topN) =>
        SearchDetailed(query, topN, CancellationToken.None);

    // Total = every match, not just the shown topN. Cancellation is
    // cooperative: checked every 4096 candidates so a stale keystroke's
    // full-index scan dies fast instead of burning CPU to completion.
    public (List<ResultRow> Rows, int Total) SearchDetailed(
        Query query, int topN, CancellationToken ct)
    {
        var none = (new List<ResultRow>(), 0);
        if (topN <= 0)
            return none;
        var q = query.Text.ToLowerInvariant();
        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0 && query.Extensions.Count == 0
            && query.Globs.Count == 0
            && query.MinSizeBytes == null && query.MaxSizeBytes == null
            && query.SizeDisplayTarget == null
            && query.ModifiedAfterUtc == null && query.ModifiedBeforeUtc == null)
            return none;

        // Text forms OR-groups: terms split on [OR] (case-insensitive),
        // [AND] is an explicit no-op (AND is the default, binding tighter
        // by construction). Directives stay global across groups: only the
        // free-text terms branch. A query matching no group matches nothing.
        var textGroups = SplitOrGroups(terms);
        if (textGroups.Count == 0)
        {
            if (!HasFilters(query))
                return none;
            textGroups.Add(new List<string>()); // filter-only: GlobRank path
        }
        var flatTerms = textGroups.SelectMany(g => g).ToArray();
        int[] candidates = PickCandidates(flatTerms, query);
        bool needFileInfo = query.MinSizeBytes != null || query.MaxSizeBytes != null
            || query.SizeDisplayTarget != null
            || query.ModifiedAfterUtc != null || query.ModifiedBeforeUtc != null;

        // Cheap filters first (no syscalls), then in-memory file filters
        // over ALL survivors. No examined-cap: a selective filter must
        // never silently drop matches past an arbitrary cutoff.
        var passed = new List<int>(Math.Min(candidates.Length, 4096));
        for (int n = 0; n < candidates.Length; n++)
        {
            if ((n & 4095) == 0)
                ct.ThrowIfCancellationRequested();
            int id = candidates[n];
            if (query.Extensions.Count > 0 && !MatchExt(_names[id], query.Extensions))
                continue; // free: extension check needs no syscalls
            if (query.Globs.Count > 0 && !MatchAllGlobs(_names[id], query.Globs))
                continue;
            if (needFileInfo && !MatchFileFilters(id, query))
                continue;
            passed.Add(id);
        }

        var scored = new List<(int Score, int Idx)>(passed.Count);
        if (terms.Length == 0)
        {
            for (int n = 0; n < passed.Count; n++)
            {
                if ((n & 4095) == 0)
                    ct.ThrowIfCancellationRequested();
                scored.Add((GlobRank(_names[passed[n]], query), passed[n]));
            }
        }
        else
        {
            // A candidate matches when ANY group fully matches (AND
            // inside, OR across); its score is the best group's sum.
            // Single-term exact-name boost is preserved inside Score().
            for (int n = 0; n < passed.Count; n++)
            {
                if ((n & 4095) == 0)
                    ct.ThrowIfCancellationRequested();
                int id = passed[n];
                int s = -1;
                foreach (var group in textGroups)
                {
                    int gs = 0;
                    bool ok = true;
                    foreach (var term in group)
                    {
                        int ts = Score(_names[id], term);
                        if (query.MatchPath)
                        {
                            // Full-path match scores lower so name hits keep
                            // ranking first.
                            int ps = Score(_paths[id].ToLowerInvariant(), term) - 50;
                            if (ps > ts) ts = ps;
                        }
                        if (ts < 0) { ok = false; break; }
                        gs += ts;
                    }
                    if (ok && gs > s) s = gs;
                }
                if (s >= 0)
                    scored.Add((s, id));
            }
        }
        scored.Sort((a, b) => b.Score.CompareTo(a.Score));
        int total = scored.Count;

        List<int> ordered;
        if (query.Sort == "rank")
        {
            ordered = scored.Take(topN).Select(t => t.Idx).ToList();
        }
        else if (query.Sort == "name")
        {
            var byPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in scored)
                byPath[_paths[t.Idx]] = t.Idx;
            ordered = Sorter.Apply(byPath.Keys.ToList(), "name")
                .Take(topN)
                .Select(p => byPath[p])
                .ToList();
        }
        else if (query.Sort == "date")
        {
            // In-index timestamps: no syscalls, whole match set.
            ordered = scored.OrderByDescending(t => _mtimes[t.Idx])
                .Take(topN).Select(t => t.Idx).ToList();
        }
        else // size
        {
            ordered = scored.OrderByDescending(t => _sizes[t.Idx])
                .Take(topN).Select(t => t.Idx).ToList();
        }
        var rows = ordered
            .Select(id => new ResultRow(_paths[id], _sizes[id], _mtimes[id]))
            .ToList();
        return (rows, total);
    }

    // "[OR]" (any case) splits free text into alternative groups;
    // "[AND]" is skipped: AND is already the default within a group.
    // Empty groups are dropped, so a trailing "[OR]" changes nothing.
    private static List<List<string>> SplitOrGroups(string[] terms)
    {
        var groups = new List<List<string>> { new() };
        foreach (var t in terms)
        {
            if (t.Equals("[or]", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add(new List<string>());
                continue;
            }
            if (t.Equals("[and]", StringComparison.OrdinalIgnoreCase))
                continue;
            groups[^1].Add(t);
        }
        return groups.Where(g => g.Count > 0).ToList();
    }

    private static bool HasFilters(Query query) =>
        query.Extensions.Count > 0 || query.Globs.Count > 0
        || query.MinSizeBytes != null || query.MaxSizeBytes != null
        || query.SizeDisplayTarget != null
        || query.ModifiedAfterUtc != null || query.ModifiedBeforeUtc != null;

    private int[] PickCandidates(string[] terms, Query query)
    {
        if (terms.Length > 0 && !query.MatchPath)
        {
            // Union of each term's narrowing: recall for multi-term
            // queries; the scoring loop enforces AND afterwards.
            var seen = new HashSet<int>();
            var union = new List<int>();
            foreach (var t in terms)
                foreach (var id in Narrow(t))
                    if (seen.Add(id))
                        union.Add(id);
            return union.ToArray();
        }
        if (terms.Length > 0 && query.MatchPath)
            return AllIds(); // path matching needs full scan (opt-in mode)
        // Glob-only query: narrow by longest literal run, else full scan.
        string best = "";
        foreach (var g in query.Globs)
        {
            var lit = Glob.LongestLiteral(g);
            if (lit.Length > best.Length)
                best = lit;
        }
        if (best.Length >= 2)
            return Narrow(best);
        return AllIds();
    }

    private static bool MatchAllGlobs(string lowerName, List<string> globs)
    {
        foreach (var g in globs)
            if (!Glob.Match(lowerName, g))
                return false;
        return true;
    }

    private static int GlobRank(string lowerName, Query query)
    {
        // Scores must stay >= 0 (callers drop negatives). Exact filename
        // floats to the top; otherwise shorter names first: deterministic.
        foreach (var g in query.Globs)
        {
            var plain = g.Replace("*", "").Replace("?", "");
            if (lowerName.Equals(plain, StringComparison.Ordinal))
                return 100000 - Math.Min(lowerName.Length, 99999);
        }
        return 50000 - Math.Min(lowerName.Length, 49999);
    }

    private int[] AllIds()
    {
        var all = new int[_paths.Length];
        for (int i = 0; i < all.Length; i++)
            all[i] = i;
        return all;
    }

    private static bool MatchExt(string lowerName, List<string> exts)
    {
        foreach (var e in exts)
            if (lowerName.EndsWith("." + e, StringComparison.Ordinal))
                return true;
        return false;
    }

    // Pure in-memory comparison against index-time metadata: zero syscalls
    // per keystroke. Unknown metadata (-1/0, file vanished after indexing)
    // fails any active size/date constraint, like a stat failure used to.
    private bool MatchFileFilters(int id, Query query)
    {
        long size = _sizes[id];
        if (query.SizeDisplayTarget != null)
        {
            if (size < 0 || SizeFormat.FormatBytes(size, query.SizeDecimalUnits)
                    != query.SizeDisplayTarget)
                return false;
        }
        else
        {
            if (query.MinSizeBytes != null && (size < 0 || size < query.MinSizeBytes))
                return false;
            if (query.MaxSizeBytes != null && (size < 0 || size > query.MaxSizeBytes))
                return false;
        }
        long ticks = _mtimes[id];
        if (query.ModifiedAfterUtc != null
            && (ticks == 0 || ticks < query.ModifiedAfterUtc.Value.Ticks))
            return false;
        if (query.ModifiedBeforeUtc != null
            && (ticks == 0 || ticks > query.ModifiedBeforeUtc.Value.Ticks))
            return false;
        return true;
    }

    private int[] Narrow(string q)
    {
        // Union of the 3 smallest bigram buckets. A single rarest bucket
        // drops valid gappy subsequence matches (e.g. "t." missing in
        // "my-report-final.docx"); union keeps recall while staying small.
        var small = new List<int[]>();
        foreach (var bg in Bigrams(q))
        {
            if (_buckets.TryGetValue(bg, out var arr))
                small.Add(arr);
        }
        if (small.Count == 0)
        {
            // 1-char query: fall back to full scan (cheap for warm caches).
            var all = new int[_paths.Length];
            for (int i = 0; i < all.Length; i++)
                all[i] = i;
            return all;
        }
        small.Sort((a, b) => a.Length.CompareTo(b.Length));
        var seen = new HashSet<int>();
        var union = new List<int>();
        for (int k = 0; k < Math.Min(3, small.Count); k++)
            foreach (var id in small[k])
                if (seen.Add(id))
                    union.Add(id);
        return union.ToArray();
    }

    private static int Score(string name, string q)
    {
        // Subsequence match with bonuses: prefix > word-boundary > early.
        // Exact filename match floats above everything (still deterministic).
        if (name.Equals(q, StringComparison.Ordinal))
            return 50000 - name.Length;
        int ni = 0, qi = 0, score = 0, lastHit = -2;
        while (ni < name.Length && qi < q.Length)
        {
            if (name[ni] == q[qi])
            {
                int bonus = ni == 0 ? 100 : 0;
                if (ni > 0 && (name[ni - 1] is '_' or '-' or ' ' or '.'))
                    bonus += 50;
                bonus += Math.Max(0, 20 - (ni - lastHit));
                score += 10 + bonus;
                lastHit = ni;
                qi++;
            }
            ni++;
        }
        return qi == q.Length ? score - name.Length : -1;
    }

    private static IEnumerable<int> Bigrams(string s)
    {
        if (s.Length < 2)
            yield break;
        int prev = -1;
        for (int i = 0; i < s.Length; i++)
        {
            int c = s[i];
            if (prev >= 0)
                yield return (prev << 16) | c;
            prev = c;
        }
    }

    private static string ExtractName(string path)
    {
        int k = path.LastIndexOfAny(new[] { '\\', '/' });
        return k >= 0 ? path[(k + 1)..] : path;
    }
}
