using BF2Presence.Memory;

namespace BF2Presence.Tests;

public class PatternScannerTests
{
    [Fact]
    public void Parse_HandlesWildcards()
    {
        var p = PatternScanner.Parse("48 8B ? ?? 05");
        Assert.Equal(new byte?[] { 0x48, 0x8B, null, null, 0x05 }, p);
    }

    [Fact]
    public void Parse_RejectsGarbage() =>
        Assert.Throws<FormatException>(() => PatternScanner.Parse("48 ZZ"));

    [Fact]
    public void Find_MatchesWithWildcards()
    {
        byte[] data = [0x00, 0x48, 0x8B, 0x05, 0x11, 0x22, 0x33, 0x44, 0x90];
        Assert.Equal(1, PatternScanner.Find(data, PatternScanner.Parse("48 8B 05 ? ? ? ? 90")));
        Assert.Equal(-1, PatternScanner.Find(data, PatternScanner.Parse("48 8B 0D")));
    }

    [Fact]
    public void ScanAndResolveRipRelative()
    {
        const long module = 0x140000000;
        var mem = new FakeMemory()
            .Write(module, new byte[0x10])
            .Write(module + 0x10, [0x48, 0x8B, 0x05, 0x00, 0x01, 0x00, 0x00, 0xC3]);

        var match = PatternScanner.Scan(mem, module, 0x18, PatternScanner.Parse("48 8B 05 ? ? ? ? C3"));
        Assert.Equal(module + 0x10, match);
        Assert.Equal(module + 0x10 + 7 + 0x100, PatternScanner.ResolveRipRelative(mem, match!.Value, 3, 7));
    }
}
