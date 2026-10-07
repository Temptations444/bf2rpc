using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using BF2Presence.Config;
using BF2Presence.Game;
using BF2Presence.Memory;

namespace BF2Presence.Diagnostics;

[SupportedOSPlatform("windows")]
public static class PointerTools
{
    private const string LevelMarker = "Levels/";

    private sealed record ChainDto(string ModuleOffset, List<string> Offsets);

    public static string? ParseField(string? name) =>
        name is null ? FieldNames.LevelName
        : FieldNames.All.FirstOrDefault(f => f.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool IsStringField(string field) => field is FieldNames.LevelName or FieldNames.GameMode;

    private static string CandidatesFile(string baseDir, string field) => Path.Combine(baseDir,
        field == FieldNames.LevelName ? "pointer-candidates.json" : $"pointer-candidates-{field}.json");

    public static void Scan(ProcessMemory mem, IReadOnlyList<long> targets, string field, string baseDir)
    {
        if (targets.Count == 0)
        {
            Console.WriteLine($"No addresses to scan. Run --find-value (or --find-level) first, then q to save.");
            return;
        }

        if (IsStringField(field))
        {
            var current = ReadString(mem, targets[0]);
            Console.WriteLine($"Target 0x{targets[0]:X} currently holds: \"{current}\"");
            if (field == FieldNames.LevelName && !current.Contains(LevelMarker))
            {
                Console.WriteLine("That doesn't look like a level path. The address is only valid until the game restarts:");
                Console.WriteLine("run --find-level again in this game session and use the address it gives you.");
                return;
            }
        }
        else
        {
            var readable = targets.Select(t => (t, ok: mem.TryReadInt32(t, out var v), v)).Where(x => x.ok).ToList();
            if (readable.Count == 0)
            {
                Console.WriteLine("Can't read those addresses. They are only valid until the game restarts:");
                Console.WriteLine("run --find-value again in this game session.");
                return;
            }
            var values = readable.Select(x => x.v).Distinct().ToList();
            Console.WriteLine($"{targets.Count} target address(es), current value(s): {string.Join(", ", values.Take(5))}");
            if (values.Count > 1)
                Console.WriteLine("Note: the saved addresses no longer all hold the same value; some may be stale. Continuing anyway.");
        }

        var regions = mem.EnumerateReadableRegions(writableOnly: true).ToList();
        double gb = regions.Sum(r => r.Size) / 1024.0 / 1024 / 1024;
        Console.WriteLine($"Scanning {gb:F1} GB of game memory, up to 4 levels deep. This can take a few minutes...");

        var sw = Stopwatch.StartNew();
        var chains = new PointerScanner().Scan(mem, regions, mem.ModuleBase, mem.ModuleSize, targets, Console.WriteLine);
        Console.WriteLine($"Done in {sw.Elapsed:mm\\:ss}. {chains.Count} candidate path(s).");
        if (chains.Count == 0)
        {
            Console.WriteLine("No static path found. Paste this output back so we can try a deeper/wider scan.");
            return;
        }

        foreach (var c in chains.Take(10)) Console.WriteLine($"  {c}");
        var file = CandidatesFile(baseDir, field);
        Save(file, chains);
        Console.WriteLine();
        Console.WriteLine($"Saved to {Path.GetFileName(file)}. Now:");
        Console.WriteLine("  1. Close the game completely and start it again.");
        Console.WriteLine(IsStringField(field)
            ? $"  2. Once you're at the main menu (or in a match), run: BF2Presence --pointer-check {field}"
            : $"  2. Get into a match, make the in-game value something other than 0, then run: BF2Presence --pointer-check {field} <current value>");
        Console.WriteLine("  3. Repeat the check once more (another restart or another value) to filter out lucky paths.");
    }

    public static void Check(ProcessMemory mem, string field, int? expected, string baseDir)
    {
        var file = CandidatesFile(baseDir, field);
        if (!File.Exists(file))
        {
            Console.WriteLine($"No {Path.GetFileName(file)} found. Run --pointer-scan first.");
            return;
        }
        if (!IsStringField(field) && expected is null)
        {
            Console.WriteLine($"Tell me what the value is in-game right now, e.g.: BF2Presence --pointer-check {field} 7");
            return;
        }
        if (expected is 0 or 1)
            Console.WriteLine("Warning: 0 and 1 are very common values, so wrong paths may survive. A value of 3+ filters much better.");

        var chains = Load(file);
        var survivors = new List<(PointerChain Chain, string Value)>();
        foreach (var chain in chains)
        {
            var address = PointerScanner.Resolve(mem, mem.ModuleBase, chain);
            if (address is null) continue;

            if (IsStringField(field))
            {
                var value = ReadString(mem, address.Value);
                bool ok = field == FieldNames.LevelName ? value.Contains(LevelMarker) : value.Length > 0;
                if (ok) survivors.Add((chain, value));
            }
            else if (mem.TryReadInt32(address.Value, out var i) && i == expected)
            {
                survivors.Add((chain, i.ToString()));
            }
        }

        Console.WriteLine($"{survivors.Count} of {chains.Count} path(s) still match.");
        foreach (var (chain, value) in survivors.Take(10))
            Console.WriteLine($"  {chain}  =>  {value}");

        if (survivors.Count == 0)
        {
            Console.WriteLine("None survived. Find the address again and re-run --pointer-scan.");
            return;
        }

        Save(file, survivors.Select(s => s.Chain).ToList());
        var best = survivors[0].Chain;
        WriteField(Path.Combine(baseDir, "offsets.json"), field, best);
        Console.WriteLine();
        Console.WriteLine($"Wrote the best path into offsets.json as {field} ({best}).");
        Console.WriteLine("Run BF2Presence --diag to see it live, or just run BF2Presence normally.");
    }

    private static void WriteField(string offsetsPath, string field, PointerChain chain)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var root = File.Exists(offsetsPath)
            ? JsonNode.Parse(File.ReadAllText(offsetsPath), documentOptions: options) as JsonObject ?? new JsonObject()
            : new JsonObject();

        var roots = root["roots"] as JsonObject ?? new JsonObject();
        var fields = root["fields"] as JsonObject ?? new JsonObject();
        root["roots"] = roots;
        root["fields"] = fields;

        var rootName = field == FieldNames.LevelName ? "LevelBase" : $"{field}Base";
        roots[rootName] = new JsonObject { ["moduleOffset"] = Hex(chain.ModuleOffset) };
        fields[field] = new JsonObject
        {
            ["root"] = rootName,
            ["offsets"] = new JsonArray(chain.Offsets.Select(o => (JsonNode)JsonValue.Create(Hex(o))!).ToArray()),
            ["type"] = IsStringField(field) ? "string" : "int32",
        };

        File.WriteAllText(offsetsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Save(string file, List<PointerChain> chains)
    {
        var dtos = chains.Select(c => new ChainDto(Hex(c.ModuleOffset), c.Offsets.Select(Hex).ToList())).ToList();
        File.WriteAllText(file, JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static List<PointerChain> Load(string path) =>
        (JsonSerializer.Deserialize<List<ChainDto>>(File.ReadAllText(path)) ?? [])
            .Select(d => new PointerChain(OffsetsConfig.ParseHex(d.ModuleOffset), d.Offsets.Select(OffsetsConfig.ParseHex).ToArray()))
            .ToList();

    private static string Hex(long v) => v < 0 ? $"-0x{-v:X}" : $"0x{v:X}";

    private static string ReadString(ProcessMemory mem, long address)
    {
        var buf = new byte[256];
        if (!mem.TryRead(address, buf)) return "";
        int len = 0;
        while (len < buf.Length && StringSearch.IsPrintable(buf[len])) len++;
        return System.Text.Encoding.ASCII.GetString(buf, 0, len);
    }
}
