using BF2Presence.Config;
using BF2Presence.Game;

namespace BF2Presence.Tests;

public class GameReaderTests
{
    private const long Module = 0x140000000;
    private const long Context = 0x20000000;
    private const long Level = 0x30000000;
    private const long LevelNameChars = 0x40000000;

    private static FakeMemory BuildMemory() => new FakeMemory()
        .WriteInt64(Module + 0x100, Context)
        .WriteInt64(Context + 0x30, Level)
        .WriteInt64(Level + 0x38, LevelNameChars)
        .WriteString(LevelNameChars, "Levels/MP/Naboo_01/Naboo_01")
        .WriteInt32(Context + 0x80, 37);

    private static OffsetsConfig BuildOffsets() => new()
    {
        Roots = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctx"] = new RootDefinition { ModuleOffset = "0x100" },
        },
        Fields = new(StringComparer.OrdinalIgnoreCase)
        {
            [FieldNames.LevelName] = new FieldDefinition { Root = "Ctx", Offsets = ["0x30", "0x38"], Type = "stringPtr" },
            [FieldNames.PlayerCount] = new FieldDefinition { Root = "Ctx", Offsets = ["0x80"], Type = "int32" },
            [FieldNames.Kills] = new FieldDefinition { Root = "Ctx", Offsets = ["0x999", "0x10"], Type = "int32" },
        },
    };

    [Fact]
    public void Read_FollowsCheatEngineStyleChains()
    {
        var reader = new GameReader(BuildMemory(), BuildOffsets(), Module, 0x1000);
        var snap = reader.Read();

        Assert.Equal("Levels/MP/Naboo_01/Naboo_01", snap.LevelName);
        Assert.Equal(37, snap.PlayerCount);
    }

    [Fact]
    public void Read_BrokenOrMissingFieldsAreNull()
    {
        var reader = new GameReader(BuildMemory(), BuildOffsets(), Module, 0x1000);
        var snap = reader.Read();

        Assert.Null(snap.Kills);
        Assert.Null(snap.Deaths);
        Assert.Contains("not configured", reader.ReadField(FieldNames.Deaths).Error);
    }

    [Fact]
    public void Read_ImplausibleNumbersAreNull()
    {
        var mem = BuildMemory().WriteInt32(Context + 0x80, 615_496_704);
        Assert.Null(new GameReader(mem, BuildOffsets(), Module, 0x1000).Read().PlayerCount);
    }

    [Fact]
    public void FollowChain_EmptyOffsetsReturnsBase() =>
        Assert.Equal(Module, GameReader.FollowChain(new FakeMemory(), Module, []));

    [Theory]
    [InlineData("0x30", 0x30)]
    [InlineData("30", 0x30)]
    [InlineData("-0x8", -8)]
    public void ParseHex(string input, long expected) =>
        Assert.Equal(expected, OffsetsConfig.ParseHex(input));
}
