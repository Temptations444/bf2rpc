using System.Runtime.Versioning;
using System.Text;
using BF2Presence.Memory;

namespace BF2Presence.Diagnostics;

[SupportedOSPlatform("windows")]
public static class LevelFinder
{
    private const string Needle = "Levels/";
    private const int MaxListed = 40;

    public static void Run(ProcessMemory mem)
    {
        Console.WriteLine("Step 1: be loaded INTO A MATCH (not the menus). Scanning memory, this can take a minute...");
        var candidates = StringSearch.FindAsciiStrings(mem, Needle);
        Console.WriteLine($"Found {candidates.Count} level strings.");

        for (int round = 1; ; round++)
        {
            Console.WriteLine();
            Console.WriteLine($"Step {round + 1}: go to a DIFFERENT map (or back to the main menu), wait until it has fully loaded,");
            Console.Write("then press Enter. Type q + Enter to stop: ");
            var input = Console.ReadLine();
            if (input is null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) break;

            var next = new Dictionary<long, string>();
            foreach (var (address, oldText) in candidates)
            {
                var now = ReadString(mem, address);
                if (now != oldText && now.Contains(Needle, StringComparison.Ordinal))
                    next[address] = now;
            }

            if (next.Count == 0)
            {
                Console.WriteLine("No string changed in place. The game probably allocates a new string per level,");
                Console.WriteLine("so look for strings that only exist now. Newest-looking candidates:");
                var fresh = StringSearch.FindAsciiStrings(mem, Needle)
                    .Where(kv => !candidates.ContainsKey(kv.Key))
                    .GroupBy(kv => kv.Value)
                    .OrderBy(g => g.Count())
                    .Take(MaxListed);
                foreach (var g in fresh)
                    Console.WriteLine($"  {g.Count(),3}x  {g.Key}   e.g. 0x{g.First().Key:X}");
                return;
            }

            candidates = next;
            Console.WriteLine($"{candidates.Count} string(s) changed:");
            foreach (var (address, text) in candidates.Take(MaxListed))
                Console.WriteLine($"  0x{address:X}  {text}");
            if (candidates.Count > MaxListed) Console.WriteLine($"  ... and {candidates.Count - MaxListed} more. Switch map again to narrow down.");
            if (candidates.Count <= 3)
            {
                Console.WriteLine();
                Console.WriteLine("Down to a few candidates. Paste these addresses back to continue (next: pointer scan in Cheat Engine).");
            }
        }
    }

    private static string ReadString(ProcessMemory mem, long address)
    {
        var buf = new byte[256];
        if (!mem.TryRead(address, buf)) return "";
        int len = 0;
        while (len < buf.Length && StringSearch.IsPrintable(buf[len])) len++;
        return Encoding.ASCII.GetString(buf, 0, len);
    }
}
