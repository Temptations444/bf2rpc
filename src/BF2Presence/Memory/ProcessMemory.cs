using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace BF2Presence.Memory;

[SupportedOSPlatform("windows")]
public sealed class ProcessMemory : IMemoryReader, IDisposable
{
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_GUARD = 0x100;
    private const uint PAGE_NOCACHE_OR_WRITECOMBINE = 0x200 | 0x400;
    private const uint PAGE_NOACCESS = 0x01;
    private const uint PAGE_ANY_WRITABLE = 0x04 | 0x08 | 0x40 | 0x80;

    private IntPtr _handle;

    public int ProcessId { get; }
    public long ModuleBase { get; }
    public int ModuleSize { get; }
    public string ModuleName { get; }

    public ProcessMemory(Process process)
    {
        ProcessId = process.Id;
        _handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, process.Id);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"OpenProcess failed (error {Marshal.GetLastWin32Error()}). " +
                "If the game runs as administrator, this tool must too.");

        var main = process.MainModule ?? throw new InvalidOperationException("Could not read the game's main module.");
        ModuleBase = main.BaseAddress.ToInt64();
        ModuleSize = main.ModuleMemorySize;
        ModuleName = main.ModuleName;
    }

    public bool TryRead(long address, Span<byte> buffer)
    {
        if (_handle == IntPtr.Zero || address <= 0) return false;
        unsafe
        {
            fixed (byte* p = buffer)
            {
                return ReadProcessMemory(_handle, new IntPtr(address), (IntPtr)p, buffer.Length, out var read)
                       && read > 0;
            }
        }
    }

    public IEnumerable<(long Base, long Size)> EnumerateReadableRegions(bool writableOnly = false)
    {
        long address = 0;
        while (address < 0x7FFF_FFFF_FFFF)
        {
            if (VirtualQueryEx(_handle, new IntPtr(address), out var mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                yield break;

            long regionBase = mbi.BaseAddress.ToInt64();
            long regionSize = mbi.RegionSize.ToInt64();
            if (mbi.State == MEM_COMMIT && (mbi.Protect & (PAGE_GUARD | PAGE_NOACCESS | PAGE_NOCACHE_OR_WRITECOMBINE)) == 0 &&
                (!writableOnly || (mbi.Protect & PAGE_ANY_WRITABLE) != 0))
                yield return (regionBase, regionSize);

            long next = regionBase + regionSize;
            if (next <= address) yield break;
            address = next;
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress, IntPtr buffer, nint size, out nint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualQueryEx(IntPtr process, IntPtr address, out MEMORY_BASIC_INFORMATION buffer, nint length);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
