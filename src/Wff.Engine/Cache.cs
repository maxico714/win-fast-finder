// Binary cache v2: magic + version + count + (path, size bytes, mtime ticks).
// v1 caches are rejected ("unsupported cache version") so the caller falls
// back to a full walk, which re-saves in v2. Buffered 64KB sequential IO.
namespace Wff.Engine;

public static class Cache
{
    private const uint Magic = 0x31464657; // "WFF1"
    private const int Version = 2;

    public static void Save(FileIndex index, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write,
            FileShare.None, 65536, FileOptions.SequentialScan);
        using var w = new BinaryWriter(fs, System.Text.Encoding.UTF8);
        w.Write(Magic);
        w.Write(Version);
        w.Write(index.Count);
        for (int i = 0; i < index.Count; i++)
        {
            w.Write(index.Paths[i]);
            w.Write(index.Sizes[i]);
            w.Write(index.Mtimes[i]);
        }
    }

    public static (List<string> Paths, long[] Sizes, long[] Mtimes) Load(string file)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
            FileShare.Read, 65536, FileOptions.SequentialScan);
        using var r = new BinaryReader(fs, System.Text.Encoding.UTF8);
        if (r.ReadUInt32() != Magic)
            throw new InvalidDataException("not a win-fast-finder cache");
        if (r.ReadInt32() != Version)
            throw new InvalidDataException("unsupported cache version");
        int n = r.ReadInt32();
        if (n < 0 || n > 20_000_000)
            throw new InvalidDataException("corrupt entry count");
        var paths = new List<string>(Math.Min(n, 1_000_000));
        var sizes = new long[n];
        var mtimes = new long[n];
        for (int i = 0; i < n; i++)
        {
            paths.Add(r.ReadString());
            sizes[i] = r.ReadInt64();
            mtimes[i] = r.ReadInt64();
        }
        return (paths, sizes, mtimes);
    }
}
