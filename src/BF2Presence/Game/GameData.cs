using System.Text.RegularExpressions;

namespace BF2Presence.Game;

public sealed record MapInfo(string Name, string ImageKey);

public static partial class GameData
{
    private static readonly (string Keyword, MapInfo Map)[] Maps =
    [
        ("Jabba", new("Jabba's Palace, Tatooine", "tatooine")),
        ("MosEisley", new("Mos Eisley, Tatooine", "tatooine")),
        ("Tatooine", new("Tatooine", "tatooine")),
        ("Theed", new("Theed, Naboo", "naboo")),
        ("Naboo", new("Naboo", "naboo")),
        ("Kamino", new("Kamino", "kamino")),
        ("Kashyyyk", new("Kashyyyk", "kashyyyk")),
        ("Geonosis", new("Geonosis", "geonosis")),
        ("Felucia", new("Felucia", "felucia")),
        ("Hoth", new("Hoth", "hoth")),
        ("Endor", new("Endor", "endor")),
        ("Yavin", new("Yavin 4", "yavin")),
        ("DeathStar", new("Death Star II", "deathstar")),
        ("CloudCity", new("Cloud City, Bespin", "bespin")),
        ("Bespin", new("Bespin", "bespin")),
        ("Scarif", new("Scarif", "scarif")),
        ("Starkiller", new("Starkiller Base", "starkiller")),
        ("Takodana", new("Takodana", "takodana")),
        ("Crait", new("Crait", "crait")),
        ("Jakku", new("Jakku", "jakku")),
        ("AjanKloss", new("Ajan Kloss", "ajankloss")),
        ("Kessel", new("Kessel", "kessel")),
        ("Fondor", new("Fondor", "fondor")),
        ("Ryloth", new("Ryloth", "ryloth")),
        ("SpaceBear", new("D'Qar", "dqar")),
        ("DQar", new("D'Qar", "dqar")),
    ];

    private static readonly (string Keyword, string Name)[] Modes =
    [
        ("HeroStarfighter", "Hero Starfighters"),
        ("StarfighterAssault", "Starfighter Assault"),
        ("Starfighter", "Starfighter Assault"),
        ("HeroesVsVillains", "Heroes vs. Villains"),
        ("HeroesVillains", "Heroes vs. Villains"),
        ("HvV", "Heroes vs. Villains"),
        ("HeroShowdown", "Hero Showdown"),
        ("CapitalSupremacy", "Capital Supremacy"),
        ("Supremacy", "Supremacy"),
        ("GalacticAssault", "Galactic Assault"),
        ("EwokHunt", "Ewok Hunt"),
        ("JetpackCargo", "Jetpack Cargo"),
        ("Extraction", "Extraction"),
        ("Strike", "Strike"),
        ("Blast", "Blast"),
        ("Onslaught", "Co-Op"),
        ("Coop", "Co-Op"),
        ("Arcade", "Arcade"),
        ("Campaign", "Campaign"),
        ("Assault", "Galactic Assault"),
    ];

    public static MapInfo ResolveMap(string levelName, string fallbackImageKey)
    {
        var map = FindMap(levelName) ?? new MapInfo(Prettify(levelName), fallbackImageKey);
        return IsSpaceLevel(levelName) ? map with { Name = $"{map.Name} (Space)" } : map;
    }

    private static MapInfo? FindMap(string levelName)
    {
        foreach (var (keyword, map) in Maps)
        {
            if (Normalize(levelName).Contains(Normalize(keyword), StringComparison.OrdinalIgnoreCase))
                return map;
        }
        return null;
    }

    public static bool IsSpaceLevel(string levelName) =>
        levelName.Contains("/Space/", StringComparison.OrdinalIgnoreCase);

    public static string? InferMode(string levelName) =>
        IsSpaceLevel(levelName) ? "Starfighter Assault" : null;

    public static string? ModeForMap(IEnumerable<string> presenceTexts, string mapName) =>
        presenceTexts
            .Select(t => PresenceText().Match(t))
            .Where(m => m.Success && SameMap(m.Groups[2].Value, mapName))
            .GroupBy(m => m.Groups[1].Value.Trim())
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    private static bool SameMap(string presenceMap, string mapName)
    {
        var a = Normalize(presenceMap.Trim());
        var b = Normalize(mapName);
        return a.Length > 0 && (b.Contains(a, StringComparison.OrdinalIgnoreCase) || a.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveMode(string mode)
    {
        foreach (var (keyword, name) in Modes)
        {
            if (Normalize(mode).Contains(Normalize(keyword), StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return Prettify(mode);
    }

    public static string Prettify(string raw)
    {
        var last = raw.Replace('\\', '/').TrimEnd('/').Split('/')[^1];
        last = MpPrefix().Replace(last, "");
        last = TrailingNumber().Replace(last, "");
        last = last.Replace('_', ' ');
        last = CamelBoundary().Replace(last, " ");
        return MultiSpace().Replace(last, " ").Trim();
    }

    private static string Normalize(string s) => s.Replace("_", "").Replace(" ", "").Replace("-", "").Replace("'", "");

    [GeneratedRegex("^(MP|SP|COOP|SB)_", RegexOptions.IgnoreCase)]
    private static partial Regex MpPrefix();

    [GeneratedRegex(@"_\d+$")]
    private static partial Regex TrailingNumber();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z0-9])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex(@"Playing (.+?) on (.+)$")]
    private static partial Regex PresenceText();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();
}
