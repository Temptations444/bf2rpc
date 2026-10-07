using System.Runtime.Versioning;
using System.Text;
using BF2Presence.Memory;

namespace BF2Presence.Diagnostics;

[SupportedOSPlatform("windows")]
public static class StringSearch
{
    private const int ChunkSize = 4 * 1024 * 1024;
    private const int MaxResults = 200;

    public static void Run(ProcessMemory mem, string text)
    {
        var patterns = new[]
        {
            ("ascii", Encoding.ASCII.GetBytes(text)),
            ("utf16", Encoding.Unicode.GetBytes(text)),
        };

        int found = 0;
        foreach (var (regionBase, regionSize) in mem.EnumerateReadableRegions())
        {
            for (long offset = 0; offset < regionSize; offset += ChunkSize)
            {
                int size = (int)Math.Min(ChunkSize, regionSize - offset);
                var buf = new byte[size];
                if (!mem.TryRead(regionBase + offset, buf)) continue;

                foreach (var (encoding, needle) in patterns)
                {
                    int start = 0;
                    int idx;
                    while ((idx = buf.AsSpan(start).IndexOf(needle)) >= 0)
                    {
                        long address = regionBase + offset + start + idx;
                        string context = encoding == "ascii"
                            ? ReadAsciiContext(buf, start + idx)
                            : text;
                        Console.WriteLine($"0x{address:X}  [{encoding}]  {context}");
                        if (++found >= MaxResults)
                        {
                            Console.WriteLine($"Stopped after {MaxResults} results - use a more specific search text.");
                            return;
                        }
                        start += idx + needle.Length;
                    }
                }
            }
        }
        Console.WriteLine(found == 0 ? "No matches." : $"{found} match(es).");
    }

    public static Dictionary<long, string> FindAsciiStrings(ProcessMemory mem, string needle)
    {
        var bytes = Encoding.ASCII.GetBytes(needle);
        var results = new Dictionary<long, string>();
        var chunk = new byte[ChunkSize];
        foreach (var (regionBase, regionSize) in mem.EnumerateReadableRegions(writableOnly: true))
        {
            for (long offset = 0; offset < regionSize; offset += ChunkSize)
            {
                int size = (int)Math.Min(ChunkSize, regionSize - offset);
                var buf = size == ChunkSize ? chunk : new byte[size];
                if (!mem.TryRead(regionBase + offset, buf)) continue;

                int start = 0;
                int idx;
                while ((idx = buf.AsSpan(start).IndexOf(bytes)) >= 0)
                {
                    int hit = start + idx;
                    int begin = hit;
                    while (begin > 0 && hit - begin < 200 && IsPrintable(buf[begin - 1])) begin--;
                    results.TryAdd(regionBase + offset + begin, ReadAsciiContext(buf, hit));
                    start = hit + bytes.Length;
                }
            }
        }
        return results;
    }

    internal static string ReadAsciiContext(byte[] buf, int index)
    {
        int begin = index;
        while (begin > 0 && index - begin < 200 && IsPrintable(buf[begin - 1])) begin--;
        int end = index;
        while (end < buf.Length && end - index < 200 && IsPrintable(buf[end])) end++;
        return Encoding.ASCII.GetString(buf, begin, end - begin);
    }

    internal static bool IsPrintable(byte b) => b is >= 0x20 and < 0x7F;
}
