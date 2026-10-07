using BF2Presence.Config;
using BF2Presence.Game;

namespace BF2Presence.Discord;

public sealed record PresenceModel
{
    public string? Details { get; init; }
    public string? State { get; init; }
    public string LargeImageKey { get; init; } = "";
    public string? LargeImageText { get; init; }
    public string SmallImageKey { get; init; } = "bf2rpc_clear";
    public string SmallImageText { get; init; } = "BF2RPC";
    public DateTime? StartTimeUtc { get; init; }
}

public static class PresenceBuilder
{
    private const int MaxTextLength = 100;
    private const string GameTitle = "Star Wars Battlefront II";

    public static bool IsInMatch(GameSnapshot s, AppSettings settings) =>
        !string.IsNullOrWhiteSpace(s.LevelName) &&
        !settings.MenuLevelKeywords.Any(k => s.LevelName.Contains(k, StringComparison.OrdinalIgnoreCase));

    public static PresenceModel? Build(GameSnapshot s, AppSettings settings, DateTime sessionStartUtc, DateTime? matchStartUtc)
    {
        var show = settings.Show;

        if (!IsInMatch(s, settings))
        {
            if (!show.InMenus) return null;
            return new PresenceModel
            {
                Details = "In the menus",
                LargeImageKey = settings.DefaultImageKey,
                LargeImageText = GameTitle,
                StartTimeUtc = show.ElapsedTime ? sessionStartUtc : null,
            };
        }

        var map = GameData.ResolveMap(s.LevelName!, settings.DefaultImageKey);
        string? mode = !show.Mode ? null
            : !string.IsNullOrWhiteSpace(s.GameMode) ? GameData.ResolveMode(s.GameMode)
            : GameData.InferMode(s.LevelName!);

        string details = (mode, show.Map) switch
        {
            (not null, true) => $"{mode} - {map.Name}",
            (not null, false) => mode,
            (null, true) => map.Name,
            _ => "In a match",
        };

        bool abbr = show.Abbreviate;
        var kad = new List<string>();
        if (show.Kills && s.Kills is { } k) kad.Add(abbr ? $"{k}K" : Count(k, "Kill"));
        if (show.Assists && s.Assists is { } a) kad.Add(abbr ? $"{a}A" : Count(a, "Assist"));
        if (show.Deaths && s.Deaths is { } d) kad.Add(abbr ? $"{d}D" : Count(d, "Death"));

        var perf = new List<string>();
        if (show.Score && s.Score is { } sc) perf.Add($"{sc:N0} Score");
        if (show.Spe && Spe(s) is { } spe) perf.Add($"{spe:N0} SPE");

        if (kad.Count > 0 && abbr) kad = [string.Join("/", kad)];
        var stats = kad.Concat(perf).ToList();
        string? state = stats.Count > 0 ? string.Join(" | ", stats) : null;

        return new PresenceModel
        {
            Details = Clamp(details),
            State = Clamp(state),
            LargeImageKey = show.Map ? map.ImageKey : settings.DefaultImageKey,
            LargeImageText = show.Map ? map.Name : GameTitle,
            StartTimeUtc = show.ElapsedTime ? matchStartUtc ?? sessionStartUtc : null,
        };
    }

    public static int? Spe(GameSnapshot s)
    {
        int elims = (s.Kills ?? 0) + (s.Assists ?? 0);
        return s.Score is { } score && elims > 0 ? (int)Math.Round((double)score / elims) : null;
    }

    private static string Count(int n, string word) => $"{n} {word}{(n == 1 ? "" : "s")}";

    private static string? Clamp(string? text) =>
        text is null ? null : text.Length <= MaxTextLength ? text : text[..(MaxTextLength - 3)] + "...";
}
