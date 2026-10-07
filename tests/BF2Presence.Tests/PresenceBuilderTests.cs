using BF2Presence.Config;
using BF2Presence.Discord;
using BF2Presence.Game;

namespace BF2Presence.Tests;

public class PresenceBuilderTests
{
    private static readonly DateTime Session = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Match = Session.AddMinutes(5);
    private static AppSettings FullWords => new() { Show = { Abbreviate = false } };

    private static readonly GameSnapshot Naboo = new()
    {
        LevelName = "Levels/MP/Naboo_01/Naboo_01",
        GameMode = "Galactic Assault",
        Kills = 10,
        Assists = 4,
        Deaths = 5,
        Score = 1000,
    };

    [Fact]
    public void NoLevel_ShowsMenus()
    {
        var m = PresenceBuilder.Build(GameSnapshot.Empty, new AppSettings(), Session, null)!;
        Assert.Equal("In the menus", m.Details);
        Assert.Equal(Session, m.StartTimeUtc);
    }

    [Fact]
    public void FrontEndLevel_ShowsMenus()
    {
        var snap = new GameSnapshot { LevelName = "Levels/FrontEnd/FrontEnd" };
        Assert.Equal("In the menus", PresenceBuilder.Build(snap, new AppSettings(), Session, null)!.Details);
    }

    [Fact]
    public void Menus_HiddenWhenInMenusOff() =>
        Assert.Null(PresenceBuilder.Build(GameSnapshot.Empty, new AppSettings { Show = { InMenus = false } }, Session, null));

    [Fact]
    public void InMatch_AllStatsOnSecondLine()
    {
        var m = PresenceBuilder.Build(Naboo, FullWords, Session, Match)!;

        Assert.Equal("Galactic Assault - Naboo", m.Details);
        Assert.Equal("10 Kills | 4 Assists | 5 Deaths | 1,000 Score | 71 SPE", m.State);
        Assert.Equal("Naboo", m.LargeImageText);
        Assert.Equal("naboo", m.LargeImageKey);
        Assert.Equal(("bf2rpc_clear", "BF2RPC"), (m.SmallImageKey, m.SmallImageText));
        Assert.Equal(Match, m.StartTimeUtc);
    }

    [Fact]
    public void Abbreviated()
    {
        var m = PresenceBuilder.Build(Naboo, new AppSettings(), Session, Match)!;
        Assert.Equal("10K/4A/5D | 1,000 Score | 71 SPE", m.State);
    }

    [Fact]
    public void Singular()
    {
        var snap = Naboo with { Kills = 1, Assists = 1, Deaths = 1 };
        Assert.StartsWith("1 Kill | 1 Assist | 1 Death |", PresenceBuilder.Build(snap, FullWords, Session, Match)!.State);
    }

    [Fact]
    public void ScoreOnlyWhenNoKad()
    {
        var settings = new AppSettings { Show = { Kills = false, Assists = false, Deaths = false } };
        var m = PresenceBuilder.Build(Naboo, settings, Session, Match)!;
        Assert.Equal("1,000 Score | 71 SPE", m.State);
        Assert.Equal("Naboo", m.LargeImageText);
    }

    [Fact]
    public void RespectsShowSettings()
    {
        var settings = new AppSettings { Show = { Map = false, Kills = false, Assists = false, Deaths = false, Score = false, Spe = false, ElapsedTime = false } };
        var m = PresenceBuilder.Build(Naboo with { GameMode = "Supremacy" }, settings, Session, Match)!;

        Assert.Equal("Supremacy", m.Details);
        Assert.Null(m.State);
        Assert.Equal("swbf2", m.LargeImageKey);
        Assert.Null(m.StartTimeUtc);
    }

    [Fact]
    public void Spe_NullWithoutEliminations() =>
        Assert.Null(PresenceBuilder.Spe(Naboo with { Kills = 0, Assists = 0 }));
}
