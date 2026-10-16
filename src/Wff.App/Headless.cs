// Headless mode: index a directory, print file count + build ms,
// optionally run one query and print top hits + ms. Used for E2E/perf proof.
using Wff.Engine;

namespace Wff.App;

public static class Headless
{
    public static int Run(string[] args)
    {
        string dir = args[1];
        string query = "";
        for (int i = 2; i + 1 < args.Length; i += 2)
            if (args[i] == "--query")
                query = args[i + 1];

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var settings = Settings.Load();
        var files = Walker.EnumerateParallel(dir, settings.Blacklist,
            skipHiddenSystem: settings.HideHiddenAndSystem);
        var index = FileIndex.Build(files);
        sw.Stop();
        Console.WriteLine($"files={files.Count} index_ms={sw.ElapsedMilliseconds}");

        if (query.Length > 0)
        {
            sw.Restart();
            var parsed = QueryParser.Parse(query, new QueryOptions());
            foreach (var inv in parsed.Invalid)
                Console.WriteLine("WARN " + inv.Message);
            var (rows, total) = index.SearchDetailed(parsed, 10);
            sw.Stop();
            Console.WriteLine($"query_ms={sw.ElapsedMilliseconds} hits={rows.Count} total={total}");
            foreach (var r in rows)
                Console.WriteLine("HIT " + r.Path);
        }
        return 0;
    }
}
