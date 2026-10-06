// Subsequence hit positions for UI highlighting. Same greedy walk as
// FileIndex.Score so highlighted chars are exactly the scored ones.
namespace Wff.Engine;

public static class MatchSpans
{
    public static List<int> Find(string name, string query)
    {
        var spans = new List<int>();
        if (string.IsNullOrEmpty(query))
            return spans;
        var n = name.ToLowerInvariant();
        var q = query.ToLowerInvariant();
        int qi = 0;
        for (int ni = 0; ni < n.Length && qi < q.Length; ni++)
        {
            if (n[ni] == q[qi])
            {
                spans.Add(ni);
                qi++;
            }
        }
        return qi == q.Length ? spans : new List<int>();
    }
}
