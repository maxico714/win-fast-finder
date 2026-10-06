// win-fast-finder engine: zero-dependency in-memory filename index.
// Two-phase search: bigram buckets narrow (C#-speed loops) -> subsequence
// score only candidates -> top-N heap. Never scores all rows per keystroke.
namespace Wff.Engine;

public sealed class FileIndex
{
    private readonly string[] _paths;
    private readonly string[] _names;
    private readonly Dictionary<int, int[]> _buckets;

    private FileIndex(string[] paths, string[] names, Dictionary<int, int[]> buckets)
    {
        _paths = paths;
        _names = names;
        _buckets = buckets;
    }

    public int Count => _paths.Length;

    public IReadOnlyList<string> Paths => _paths;

    public static FileIndex Build(IEnumerable<string> paths)
    {
        var pathArr = paths.ToArray();
        var nameArr = new string[pathArr.Length];
        var tmp = new Dictionary<int, List<int>>();
        for (int i = 0; i < pathArr.Length; i++)
        {
            var name = ExtractName(pathArr[i]).ToLowerInvariant();
            nameArr[i] = name;
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
        return new FileIndex(pathArr, nameArr, buckets);
    }

    public List<string> Search(string query, int topN) =>
        Search(QueryParser.Parse(query), topN);

    public List<string> Search(Query query, int topN)
    {
        var result = new List<string>();
        if (topN <= 0)
            return result;
        var q = query.Text.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(q) && query.Extensions.Count == 0
            && query.Globs.Count == 0
            && query.MinSizeBytes == null && query.MaxSizeBytes == null
            && query.ModifiedAfterUtc == null && query.ModifiedBeforeUtc == null)
            return result;

        int[] candidates = PickCandidates(q, query);
        var scored = new List<(int Score, int Idx)>(Math.Min(candidates.Length, 4096));
        foreach (var id in candidates)
        {
            if (query.Extensions.Count > 0 && !MatchExt(_names[id], query.Extensions))
                continue; // free: extension check needs no syscalls
            if (query.Globs.Count > 0 && !MatchAllGlobs(_names[id], query.Globs))
                continue;
            int s = string.IsNullOrWhiteSpace(q) ? GlobRank(_names[id], query) : Score(_names[id], q);
            if (query.MatchPath && !string.IsNullOrWhiteSpace(q))
            {
                // Full-path match scores lower so name hits keep ranking first.
                int ps = Score(_paths[id].ToLowerInvariant(), q) - 50;
                if (ps > s) s = ps;
            }
            if (s >= 0)
                scored.Add((s, id));
            if (scored.Count >= 20000)
                break;
        }
        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Size/date need FileInfo syscalls: apply to top 500 only (ms-scale).
        int taken = 0;
        foreach (var (_, id) in scored)
        {
            if (taken >= 500)
                break;
            if (MatchFileFilters(_paths[id], query))
            {
                result.Add(_paths[id]);
                if (result.Count >= topN && query.Sort == "rank")
                    break;
            }
            taken++;
        }
        var sorted = Sorter.Apply(result, query.Sort);
        return sorted.Count > topN ? sorted.GetRange(0, topN) : sorted;
    }

    private int[] PickCandidates(string q, Query query)
    {
        if (!string.IsNullOrWhiteSpace(q) && !query.MatchPath)
            return Narrow(q);
        if (!string.IsNullOrWhiteSpace(q) && query.MatchPath)
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

    private static bool MatchFileFilters(string path, Query query)
    {
        if (query.MinSizeBytes == null && query.MaxSizeBytes == null
            && query.ModifiedAfterUtc == null && query.ModifiedBeforeUtc == null)
            return true;
        FileInfo fi;
        try { fi = new FileInfo(path); }
        catch { return false; }
        long len;
        DateTime wtime;
        try { len = fi.Length; wtime = fi.LastWriteTimeUtc; }
        catch { return false; }
        if (query.MinSizeBytes != null && len < query.MinSizeBytes) return false;
        if (query.MaxSizeBytes != null && len > query.MaxSizeBytes) return false;
        if (query.ModifiedAfterUtc != null && wtime < query.ModifiedAfterUtc) return false;
        if (query.ModifiedBeforeUtc != null && wtime > query.ModifiedBeforeUtc) return false;
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
