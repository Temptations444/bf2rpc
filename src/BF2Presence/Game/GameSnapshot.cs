namespace BF2Presence.Game;

public static class FieldNames
{
    public const string LevelName = "LevelName";
    public const string GameMode = "GameMode";
    public const string PlayerCount = "PlayerCount";
    public const string MaxPlayers = "MaxPlayers";
    public const string Kills = "Kills";
    public const string Deaths = "Deaths";
    public const string Assists = "Assists";
    public const string Score = "Score";

    public static readonly string[] All = [LevelName, GameMode, PlayerCount, MaxPlayers, Kills, Deaths, Assists, Score];
}

public sealed record GameSnapshot
{
    public string? LevelName { get; init; }
    public string? GameMode { get; init; }
    public int? PlayerCount { get; init; }
    public int? MaxPlayers { get; init; }
    public int? Kills { get; init; }
    public int? Deaths { get; init; }
    public int? Assists { get; init; }
    public int? Score { get; init; }

    public static readonly GameSnapshot Empty = new();
}
