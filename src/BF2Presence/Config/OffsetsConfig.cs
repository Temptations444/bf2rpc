using System.Globalization;

namespace BF2Presence.Config;

public sealed class OffsetsConfig
{
    public string GameVersion { get; set; } = "";

    public Dictionary<string, RootDefinition> Roots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, FieldDefinition> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static OffsetsConfig Load(string path)
    {
        var json = File.Exists(path) ? File.ReadAllText(path) : ReadEmbedded();
        var cfg = System.Text.Json.JsonSerializer.Deserialize<OffsetsConfig>(json, JsonFile.Options) ?? new();
        cfg.Roots = new(cfg.Roots, StringComparer.OrdinalIgnoreCase);
        cfg.Fields = new(cfg.Fields.Where(kv => kv.Value is not null), StringComparer.OrdinalIgnoreCase);
        return cfg;
    }

    private static string ReadEmbedded()
    {
        using var stream = typeof(OffsetsConfig).Assembly.GetManifestResourceStream("offsets.json")!;
        return new StreamReader(stream).ReadToEnd();
    }

    public static long ParseHex(string value)
    {
        var s = value.Trim();
        bool negative = s.StartsWith('-');
        if (negative) s = s[1..];
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        var parsed = long.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return negative ? -parsed : parsed;
    }
}

public sealed class RootDefinition
{
    public string? ModuleOffset { get; set; }

    public string? Pattern { get; set; }

    public int RipOffset { get; set; } = 3;

    public int InstructionLength { get; set; } = 7;
}

public sealed class FieldDefinition
{
    public string Root { get; set; } = "";

    public List<string> Offsets { get; set; } = [];

    public string Type { get; set; } = "int32";
}
