using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BF2Presence.Memory;

namespace BF2Presence.Diagnostics;

[SupportedOSPlatform("windows")]
public static class ValueFinder
{
    private const int ChunkSize = 4 * 1024 * 1024;
    private const int MaxFirstScanHits = 50_000_000;
    private const int MaxListed = 60;
    public const string CandidatesFileName = "value-candidates.json";

    public static void Run(ProcessMemory mem, string baseDir)
    {
        Console.WriteLine("Finds a number in game memory, e.g. your kills, deaths or the player count.");
        Console.WriteLine("Tip: start when the value is 3 or more. 0 and 1 appear millions of times.");
        Console.WriteLine("If the value keeps changing (player count), enter a range like 38-41 instead of one number.");

        long[]? candidates = null;
        while (true)
        {
            Console.WriteLine();
            Console.Write(candidates is null
                ? "Enter the value as it is in game right now: "
                : "Change the value in game (get a kill, etc.), then enter the NEW value (q to stop): ");
            var input = Console.ReadLine()?.Trim();
            if (input is null || input.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                SaveCandidates(baseDir, candidates);
                return;
            }
            var parts = input.Split('-', 2, StringSplitOptions.TrimEntries);
            if (!int.TryParse(parts[0], out var min) || !int.TryParse(parts[^1], out var max) || min > max)
            {
                Console.WriteLine("That's not a whole number or a range like 38-41.");
                continue;
            }

            Console.WriteLine("Scanning...");
            var hits = ScanForValue(mem, min, max, candidates);
            if (hits is null)
            {
                Console.WriteLine($"More than {MaxFirstScanHits:N0} matches. Pick a less common value (e.g. wait until it's higher).");
                continue;
            }

            if (hits.Length == 0 && candidates is not null)
            {
                Console.WriteLine($"0 matches. Keeping the previous {candidates.Length:N0} address(es); try that value again (a range like 18-20 helps).");
                continue;
            }
            bool unchanged = candidates is not null && hits.Length == candidates.Length;
            candidates = hits;
            Console.WriteLine($"{candidates.Length:N0} address(es) match.");
            if (candidates.Length == 0)
            {
                Console.WriteLine("Nothing found. The value may be stored differently (e.g. as a float).");
                return;
            }
            if (candidates.Length <= MaxListed)
                foreach (var a in candidates) Console.WriteLine($"  0x{a:X}");
            if (unchanged)
            {
                Console.WriteLine("The count didn't drop: the game keeps this many copies that always change together.");
                Console.WriteLine("That's fine. Type q to save them, then (without restarting the game) run:");
                Console.WriteLine("  BF2Presence --pointer-scan Kills      (or Deaths / PlayerCount / ...)");
                Console.WriteLine("It scans all saved addresses at once.");
            }
        }
    }

    private static long[]? ScanForValue(ProcessMemory mem, int min, int max, long[]? previous)
    {
        var results = new List<long>();
        var buffer = new byte[ChunkSize];
        foreach (var (regionBase, regionSize) in mem.EnumerateReadableRegions(writableOnly: true))
        {
            for (long offset = 0; offset < regionSize; offset += ChunkSize)
            {
                long chunkBase = regionBase + offset;
                int size = (int)Math.Min(ChunkSize, regionSize - offset) & ~3;

                if (previous is not null)
                {
                    int first = LowerBound(previous, chunkBase);
                    if (first >= previous.Length || previous[first] >= chunkBase + size) continue;
                }

                var span = buffer.AsSpan(0, size);
                if (!mem.TryRead(chunkBase, span)) continue;
                var ints = MemoryMarshal.Cast<byte, int>(span);
                for (int i = 0; i < ints.Length; i++)
                {
                    if (ints[i] < min || ints[i] > max) continue;
                    long address = chunkBase + i * 4L;
                    if (previous is not null && Array.BinarySearch(previous, address) < 0) continue;
                    results.Add(address);
                    if (previous is null && results.Count > MaxFirstScanHits) return null;
                }
            }
        }
        return results.ToArray();
    }

    private static void SaveCandidates(string baseDir, long[]? candidates)
    {
        if (candidates is null || candidates.Length == 0) return;
        if (candidates.Length > 10_000)
        {
            Console.WriteLine($"Not saving {candidates.Length:N0} addresses; narrow it down further first.");
            return;
        }
        var path = Path.Combine(baseDir, CandidatesFileName);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(candidates.Select(a => $"0x{a:X}")));
        Console.WriteLine($"Saved {candidates.Length} address(es) to {CandidatesFileName}.");
        Console.WriteLine("Next, WITHOUT restarting the game: BF2Presence --pointer-scan Kills   (or whichever field this is)");
    }

    public static long[] LoadCandidates(string baseDir)
    {
        var path = Path.Combine(baseDir, CandidatesFileName);
        if (!File.Exists(path)) return [];
        return (System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [])
            .Select(Config.OffsetsConfig.ParseHex).ToArray();
    }

    private static int LowerBound(long[] sorted, long value)
    {
        int idx = Array.BinarySearch(sorted, value);
        return idx < 0 ? ~idx : idx;
    }
}
