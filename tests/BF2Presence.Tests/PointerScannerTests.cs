using BF2Presence.Memory;

namespace BF2Presence.Tests;

public class PointerScannerTests
{
    private const long Module = 0x140000000;
    private const long HeapA = 0x20000000;
    private const long HeapB = 0x30000000;
    private const long Size = 0x1000;

    private static FakeMemory BuildMemory() => new FakeMemory()
        .Write(Module, new byte[Size])
        .Write(HeapA, new byte[Size])
        .Write(HeapB, new byte[Size])
        .WriteInt64(Module + 0x200, HeapA)
        .WriteInt64(HeapA + 0x48, HeapB)
        .WriteString(HeapB + 0x100, "Levels/MP/Hoth_01/Hoth_01");

    private static readonly (long, long)[] Regions = [(Module, Size), (HeapA, Size), (HeapB, Size)];

    [Fact]
    public void FindsTwoLevelStaticPath()
    {
        var mem = BuildMemory();
        var chains = new PointerScanner().Scan(mem, Regions, Module, Size, HeapB + 0x100);

        var best = Assert.Single(chains);
        Assert.Equal(0x200, best.ModuleOffset);
        Assert.Equal(new long[] { 0x48, 0x100 }, best.Offsets);
        Assert.Equal(HeapB + 0x100, PointerScanner.Resolve(mem, Module, best));
    }

    [Fact]
    public void RespectsMaxOffset()
    {
        var chains = new PointerScanner { MaxOffset = 0x80 }.Scan(BuildMemory(), Regions, Module, Size, HeapB + 0x100);
        Assert.Empty(chains);
    }

    [Fact]
    public void ShorterPathsComeFirst()
    {
        var mem = BuildMemory().WriteInt64(Module + 0x300, HeapB + 0x80);
        var chains = new PointerScanner().Scan(mem, Regions, Module, Size, HeapB + 0x100);

        Assert.Equal(2, chains.Count);
        Assert.Equal(new long[] { 0x80 }, chains[0].Offsets);
    }

    [Fact]
    public void MultipleTargets_FindsPathToWhicheverIsReachable()
    {
        var mem = BuildMemory();
        var unreachable = HeapA + 0x800;
        var chains = new PointerScanner { MaxOffset = 0x200 }.Scan(mem, Regions, Module, Size, [unreachable, HeapB + 0x100]);

        Assert.Equal(new long[] { 0x48, 0x100 }, Assert.Single(chains).Offsets);
    }
}
