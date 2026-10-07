using BF2Presence.Config;
using BF2Presence.Memory;

namespace BF2Presence.Game;

public sealed class GameReader
{
    private const int MaxStringLength = 256;
    private static readonly TimeSpan PatternRetryInterval = TimeSpan.FromSeconds(30);

    private readonly IMemoryReader _mem;
    private readonly OffsetsConfig _offsets;
    private readonly long _moduleBase;
    private readonly long _moduleSize;

    private readonly Dictionary<string, long> _resolvedRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastPatternAttempt = new(StringComparer.OrdinalIgnoreCase);

    public GameReader(IMemoryReader mem, OffsetsConfig offsets, long moduleBase, long moduleSize)
    {
        _mem = mem;
        _offsets = offsets;
        _moduleBase = moduleBase;
        _moduleSize = moduleSize;
    }

    public GameSnapshot Read() => new()
    {
        LevelName = ReadField(FieldNames.LevelName).Value as string,
        GameMode = ReadField(FieldNames.GameMode).Value as string,
        PlayerCount = AsInt(ReadField(FieldNames.PlayerCount).Value),
        MaxPlayers = AsInt(ReadField(FieldNames.MaxPlayers).Value),
        Kills = AsInt(ReadField(FieldNames.Kills).Value),
        Deaths = AsInt(ReadField(FieldNames.Deaths).Value),
        Assists = AsInt(ReadField(FieldNames.Assists).Value),
        Score = AsInt(ReadField(FieldNames.Score).Value),
    };

    public (object? Value, string? Error) ReadField(string name)
    {
        if (!_offsets.Fields.TryGetValue(name, out var field))
            return (null, "not configured in offsets.json");

        var root = ResolveRoot(field.Root);
        if (root is null)
            return (null, $"root '{field.Root}' could not be resolved");

        long? address;
        try
        {
            address = FollowChain(_mem, root.Value, field.Offsets.Select(OffsetsConfig.ParseHex).ToList());
        }
        catch (FormatException ex)
        {
            return (null, $"bad offset: {ex.Message}");
        }
        if (address is null)
            return (null, "pointer chain broke (null/unreadable pointer) - not in a match yet, or offsets are outdated");

        return field.Type.ToLowerInvariant() switch
        {
            "int32" => _mem.TryReadInt32(address.Value, out var i) ? (i, null) : (null, "unreadable int32"),
            "float" => _mem.TryReadFloat(address.Value, out var f) ? (f, null) : (null, "unreadable float"),
            "string" => ReadString(address.Value),
            "stringptr" => _mem.TryReadPointer(address.Value, out var sp) ? ReadString(sp) : (null, "string pointer is null"),
            _ => (null, $"unknown type '{field.Type}'"),
        };
    }

    private (object? Value, string? Error) ReadString(long address)
    {
        if (!_mem.TryReadCString(address, MaxStringLength, out var s)) return (null, "unreadable string");
        return string.IsNullOrWhiteSpace(s) ? (null, "empty string") : (s, null);
    }

    internal static long? FollowChain(IMemoryReader mem, long baseAddress, IReadOnlyList<long> offsets)
    {
        if (offsets.Count == 0) return baseAddress;
        if (!mem.TryReadPointer(baseAddress, out var p)) return null;
        for (int i = 0; i < offsets.Count - 1; i++)
        {
            if (!mem.TryReadPointer(p + offsets[i], out p)) return null;
        }
        return p + offsets[^1];
    }

    public long? ResolveRoot(string name)
    {
        if (_resolvedRoots.TryGetValue(name, out var cached)) return cached;
        if (!_offsets.Roots.TryGetValue(name, out var def)) return null;

        long? resolved = null;
        if (!string.IsNullOrWhiteSpace(def.ModuleOffset))
        {
            resolved = _moduleBase + OffsetsConfig.ParseHex(def.ModuleOffset);
        }
        else if (!string.IsNullOrWhiteSpace(def.Pattern))
        {
            if (_lastPatternAttempt.TryGetValue(name, out var last) && DateTime.UtcNow - last < PatternRetryInterval)
                return null;
            _lastPatternAttempt[name] = DateTime.UtcNow;

            var match = PatternScanner.Scan(_mem, _moduleBase, _moduleSize, PatternScanner.Parse(def.Pattern));
            if (match is not null)
                resolved = PatternScanner.ResolveRipRelative(_mem, match.Value, def.RipOffset, def.InstructionLength);
        }

        if (resolved is not null) _resolvedRoots[name] = resolved.Value;
        return resolved;
    }

    private const int MaxPlausible = 1_000_000;

    private static int? AsInt(object? value) => value switch
    {
        int i when i is >= 0 and <= MaxPlausible => i,
        float f when f is >= 0 and <= MaxPlausible => (int)f,
        _ => null,
    };
}
