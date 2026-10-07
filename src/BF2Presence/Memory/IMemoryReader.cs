namespace BF2Presence.Memory;

public interface IMemoryReader
{
    bool TryRead(long address, Span<byte> buffer);
}

public static class MemoryReaderExtensions
{
    public static bool TryReadInt64(this IMemoryReader mem, long address, out long value)
    {
        Span<byte> buf = stackalloc byte[8];
        value = 0;
        if (!mem.TryRead(address, buf)) return false;
        value = BitConverter.ToInt64(buf);
        return true;
    }

    public static bool TryReadInt32(this IMemoryReader mem, long address, out int value)
    {
        Span<byte> buf = stackalloc byte[4];
        value = 0;
        if (!mem.TryRead(address, buf)) return false;
        value = BitConverter.ToInt32(buf);
        return true;
    }

    public static bool TryReadFloat(this IMemoryReader mem, long address, out float value)
    {
        Span<byte> buf = stackalloc byte[4];
        value = 0;
        if (!mem.TryRead(address, buf)) return false;
        value = BitConverter.ToSingle(buf);
        return true;
    }

    public static bool TryReadPointer(this IMemoryReader mem, long address, out long pointer)
    {
        if (!mem.TryReadInt64(address, out pointer)) return false;
        return IsPlausiblePointer(pointer);
    }

    public static bool IsPlausiblePointer(long p) => p >= 0x10000 && p < 0x7FFF_FFFF_FFFF;

    public static bool TryReadCString(this IMemoryReader mem, long address, int maxLength, out string value)
    {
        value = "";
        var buf = new byte[maxLength];
        if (!mem.TryRead(address, buf)) return false;
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = buf.Length;
        value = System.Text.Encoding.UTF8.GetString(buf, 0, len);
        return true;
    }
}
