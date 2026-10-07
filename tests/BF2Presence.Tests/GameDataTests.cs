using BF2Presence.Game;

namespace BF2Presence.Tests;

public class GameDataTests
{
    [Theory]
    [InlineData("Levels/MP/Hoth_01/Hoth_01", "Hoth")]
    [InlineData("Levels/MP/DeathStar02_01/DeathStar02_01", "Death Star II")]
    [InlineData("Levels/MP/StarKiller_01/StarKiller_01", "Starkiller Base")]
    [InlineData("S2/Levels/CloudCity_01/CloudCity_01", "Cloud City, Bespin")]
    [InlineData("S2_2/Levels/JabbasPalace_01/JabbasPalace_01", "Jabba's Palace, Tatooine")]
    [InlineData("S5_1/Levels/MP/Geonosis_01/Geonosis_01", "Geonosis")]
    [InlineData("S9_3/Scarif/Levels/MP/Scarif_02/Scarif_02", "Scarif")]
    [InlineData("Levels/MP/Yavin_01/Lobby", "Yavin 4")]
    [InlineData("Levels/Space/SB_Endor_01/SB_Endor_01", "Endor (Space)")]
    [InlineData("Levels/Space/SB_SpaceBear_01/SB_SpaceBear_01", "D'Qar (Space)")]
    [InlineData("Levels/Space/SB_DroidBattleShip_01/SB_DroidBattleShip_01", "Droid Battle Ship (Space)")]
    public void KnownMaps(string path, string expected) =>
        Assert.Equal(expected, GameData.ResolveMap(path, "x").Name);

    [Fact]
    public void UnknownMap_IsPrettifiedWithFallbackImage()
    {
        var map = GameData.ResolveMap("Levels/MP/SomeNewPlace/MP_SomeNewPlace_01", "swbf2");
        Assert.Equal("Some New Place", map.Name);
        Assert.Equal("swbf2", map.ImageKey);
    }

    [Fact]
    public void SpaceLevels_InferStarfighterAssault()
    {
        Assert.Equal("Starfighter Assault", GameData.InferMode("Levels/Space/SB_Fondor_01/SB_Fondor_01"));
        Assert.Null(GameData.InferMode("Levels/MP/Hoth_01/Hoth_01"));
    }

    [Theory]
    [InlineData("StarfighterAssault", "Starfighter Assault")]
    [InlineData("GalacticAssault", "Galactic Assault")]
    [InlineData("Capital_Supremacy", "Capital Supremacy")]
    [InlineData("HeroesVsVillains", "Heroes vs. Villains")]
    [InlineData("SomethingElse", "Something Else")]
    public void Modes(string raw, string expected) =>
        Assert.Equal(expected, GameData.ResolveMode(raw));

    [Fact]
    public void ModeForMap_IgnoresTextsAboutOtherMaps()
    {
        string[] texts =
        [
            " II Playing Blast on Geonosis",
            " II Playing Galactic Assault on Tatooine",
            " II Playing Galactic Assault on Tatooine",
        ];
        Assert.Equal("Galactic Assault", GameData.ModeForMap(texts, "Mos Eisley, Tatooine"));
        Assert.Equal("Blast", GameData.ModeForMap(texts, "Geonosis"));
        Assert.Null(GameData.ModeForMap(texts, "Naboo"));
    }}
