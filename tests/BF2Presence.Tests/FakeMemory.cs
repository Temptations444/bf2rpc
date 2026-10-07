using BF2Presence.Memory;

namespace BF2Presence.Tests;

internal sealed class FakeMemory : IMemoryReader
{
    private readonly Dictionary<long, byte> _bytes = new();

    public FakeMemory Write(long address, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++) _bytes[address + i] = data[i];
        return this;
    }

    public FakeMemory WriteInt64(long address, long value) => Write(address, BitConverter.GetBytes(value));
    public FakeMemory WriteInt32(long address, int value) => Write(address, BitConverter.GetBytes(value));
    public FakeMemory WriteString(long address, string value) =>
        Write(address, System.Text.Encoding.ASCII.GetBytes(value + "\0"));

    public bool TryRead(long address, Span<byte> buffer)
    {
        if (!_bytes.ContainsKey(address)) return false;
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = _bytes.TryGetValue(address + i, out var b) ? b : (byte)0;
        return true;
    }
}
