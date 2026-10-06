// Binary cache: magic + version + count + length-prefixed UTF-8 paths.
// Buffered 64KB sequential IO; load validates magic, throws on corruption.
namespace Wff.Engine;

public static class Cache
{
    private const uint Magic = 0x31464657; // "WFF1"
    private const int Version = 1;

    public static void Save(IReadOnlyList<string> paths, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write,
            FileShare.None, 65536, FileOptions.SequentialScan);
        using var w = new BinaryWriter(fs, System.Text.Encoding.UTF8);
        w.Write(Magic);
        w.Write(Version);
        w.Write(paths.Count);
        foreach (var p in paths)
            w.Write(p);
    }

    public static List<string> Load(string file)
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
        var list = new List<string>(Math.Min(n, 1_000_000));
        for (int i = 0; i < n; i++)
            list.Add(r.ReadString());
        return list;
    }
}
