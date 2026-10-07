using System.Globalization;

namespace BF2Presence.Memory;

public static class PatternScanner
{
    private const int ChunkSize = 4 * 1024 * 1024;

    public static byte?[] Parse(string pattern)
    {
        var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) throw new FormatException("Pattern is empty.");
        var result = new byte?[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t is "?" or "??")
                result[i] = null;
            else if (byte.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                result[i] = b;
            else
                throw new FormatException($"Invalid pattern token '{t}'.");
        }
        return result;
    }

    public static int Find(ReadOnlySpan<byte> data, byte?[] pattern)
    {
        int last = data.Length - pattern.Length;
        for (int i = 0; i <= last; i++)
        {
            int j = 0;
            for (; j < pattern.Length; j++)
            {
                var p = pattern[j];
                if (p.HasValue && data[i + j] != p.Value) break;
            }
            if (j == pattern.Length) return i;
        }
        return -1;
    }

    public static long? Scan(IMemoryReader mem, long start, long size, byte?[] pattern)
    {
        int overlap = pattern.Length - 1;
        var buffer = new byte[ChunkSize + overlap];
        for (long offset = 0; offset < size; offset += ChunkSize)
        {
            int toRead = (int)Math.Min(buffer.Length, size - offset);
            var span = buffer.AsSpan(0, toRead);
            if (!mem.TryRead(start + offset, span)) continue;
            int idx = Find(span, pattern);
            if (idx >= 0) return start + offset + idx;
        }
        return null;
    }

    public static long? ResolveRipRelative(IMemoryReader mem, long instructionAddress, int dispOffset, int instructionLength)
    {
        if (!mem.TryReadInt32(instructionAddress + dispOffset, out var disp)) return null;
        return instructionAddress + instructionLength + disp;
    }
}
