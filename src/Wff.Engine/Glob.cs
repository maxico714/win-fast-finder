// Fast glob (*, ?) matcher for filenames. Case-insensitive (feed lowercase).
// Iterative backtracking: linear-ish on typical patterns, no Regex engine.
namespace Wff.Engine;

public static class Glob
{
    public static bool IsGlob(string token) => token.Contains('*') || token.Contains('?');

    public static bool Match(string name, string pattern)
    {
        int ni = 0, pi = 0, star = -1, mark = 0;
        while (ni < name.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == '?' || pattern[pi] == name[ni]))
            {
                ni++;
                pi++;
            }
            else if (pi < pattern.Length && pattern[pi] == '*')
            {
                star = pi++;
                mark = ni;
            }
            else if (star != -1)
            {
                pi = star + 1;
                ni = ++mark;
            }
            else
            {
                return false;
            }
        }
        while (pi < pattern.Length && pattern[pi] == '*')
            pi++;
        return pi == pattern.Length;
    }

    // Longest run without wildcards: feeds bigram narrowing so `*.pdf`
    // scans only names containing "pdf" instead of everything.
    public static string LongestLiteral(string pattern)
    {
        string best = "", cur = "";
        foreach (char c in pattern)
        {
            if (c == '*' || c == '?')
            {
                if (cur.Length > best.Length) best = cur;
                cur = "";
            }
            else
            {
                cur += c;
            }
        }
        return cur.Length > best.Length ? cur : best;
    }
}
