using System.Runtime.InteropServices;

namespace BF2Presence.Memory;

public sealed record PointerChain(long ModuleOffset, long[] Offsets)
{
    public override string ToString() =>
        $"\"module\"+0x{ModuleOffset:X} -> [{string.Join(", ", Offsets.Select(o => $"0x{o:X}"))}]";
}

public sealed class PointerScanner
{
    public int MaxDepth { get; init; } = 4;
    public int MaxOffset { get; init; } = 0x1000;
    public int MaxTargetsPerLevel { get; init; } = 20_000;
    public int MaxResults { get; init; } = 50_000;

    private const int ChunkSize = 4 * 1024 * 1024;

    public List<PointerChain> Scan(
        IMemoryReader mem,
        IReadOnlyList<(long Base, long Size)> regions,
        long moduleBase,
        long moduleSize,
        long target,
        Action<string>? log = null) => Scan(mem, regions, moduleBase, moduleSize, [target], log);

    public List<PointerChain> Scan(
        IMemoryReader mem,
        IReadOnlyList<(long Base, long Size)> regions,
        long moduleBase,
        long moduleSize,
        IReadOnlyCollection<long> targets,
        Action<string>? log = null)
    {
        var results = new List<PointerChain>();
        var visited = new HashSet<long>(targets);
        var current = targets.Distinct().ToDictionary(t => t, _ => Array.Empty<long>());
        var buffer = new byte[ChunkSize];

        for (int depth = 1; depth <= MaxDepth && current.Count > 0 && results.Count < MaxResults; depth++)
        {
            var sorted = current.Keys.Order().ToArray();
            long min = sorted[0] - MaxOffset;
            long max = sorted[^1];
            var next = new Dictionary<long, long[]>();
            int foundAtDepth = 0;

            foreach (var (regionBase, regionSize) in regions)
            {
                for (long offset = 0; offset < regionSize; offset += ChunkSize)
                {
                    int size = (int)Math.Min(ChunkSize, regionSize - offset) & ~7;
                    var span = buffer.AsSpan(0, size);
                    if (!mem.TryRead(regionBase + offset, span)) continue;

                    var values = MemoryMarshal.Cast<byte, long>(span);
                    for (int i = 0; i < values.Length; i++)
                    {
                        long v = values[i];
                        if (v < min || v > max) continue;

                        int idx = Array.BinarySearch(sorted, v);
                        if (idx < 0) idx = ~idx;
                        if (idx >= sorted.Length || sorted[idx] - v > MaxOffset) continue;

                        long address = regionBase + offset + i * 8L;
                        long[] chain = [sorted[idx] - v, .. current[sorted[idx]]];

                        if (address >= moduleBase && address < moduleBase + moduleSize)
                        {
                            results.Add(new PointerChain(address - moduleBase, chain));
                            foundAtDepth++;
                            if (results.Count >= MaxResults) break;
                        }
                        else if (next.Count < MaxTargetsPerLevel && visited.Add(address))
                        {
                            next[address] = chain;
                        }
                    }
                }
            }

            log?.Invoke($"  depth {depth}: {foundAtDepth} static path(s), {next.Count} heap pointer(s) to follow");
            current = next;
        }

        return results
            .OrderBy(c => c.Offsets.Length)
            .ThenBy(c => c.Offsets.Sum(o => Math.Abs(o)))
            .ToList();
    }

    public static long? Resolve(IMemoryReader mem, long moduleBase, PointerChain chain) =>
        Game.GameReader.FollowChain(mem, moduleBase + chain.ModuleOffset, chain.Offsets);
}
