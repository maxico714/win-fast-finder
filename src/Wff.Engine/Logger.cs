// File logger: timestamped lines for diagnosing "nothing happens".
// Never throws; logging must not break the app. Path injectable for tests.
namespace Wff.Engine;

public static class Logger
{
    private static string _path = DefaultPath;
    private static readonly object _lock = new();

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "win-fast-finder", "log.txt");

    public static void Init(string? path = null)
    {
        lock (_lock)
        {
            _path = path ?? DefaultPath;
        }
    }

    public static void Shutdown()
    {
        lock (_lock)
        {
            _path = DefaultPath;
        }
    }

    public static void Info(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (dir != null)
                Directory.CreateDirectory(dir);
            lock (_lock)
            {
                File.AppendAllText(_path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging is best-effort by design
        }
    }
}
