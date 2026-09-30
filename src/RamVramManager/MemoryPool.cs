using System.Runtime.InteropServices;

namespace RamVramManager;

public sealed class MemoryPool : IDisposable
{
    private readonly object _sync = new();
    private readonly List<nint> _blocks = new();
    private long _allocatedBytes;
    private bool _disposed;

    public long AllocatedBytes { get { lock (_sync) return _allocatedBytes; } }

    public void SetTarget(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        lock (_sync)
        {
            ThrowIfDisposed();
            while (_allocatedBytes < bytes)
            {
                var chunk = (nuint)Math.Min(bytes - _allocatedBytes, 256L * 1024 * 1024);
                var p = VirtualAlloc(nint.Zero, chunk, AllocationType.Commit | AllocationType.Reserve, MemoryProtection.ReadWrite);
                if (p == nint.Zero) throw new OutOfMemoryException("Windows could not commit the requested RAM pool.");
                _blocks.Add(p);
                _allocatedBytes += (long)chunk;
            }
            while (_allocatedBytes > bytes && _blocks.Count > 0)
            {
                var p = _blocks[^1];
                _blocks.RemoveAt(_blocks.Count - 1);
                var size = GetRegionSize(p);
                VirtualFree(p, 0, FreeType.Release);
                _allocatedBytes -= size;
            }
        }
    }

    private static long GetRegionSize(nint address)
    {
        MEMORY_BASIC_INFORMATION mbi = default;
        if (VirtualQuery(address, ref mbi, (nuint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
            return 0;
        return (long)mbi.RegionSize;
    }

    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MemoryPool)); }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            foreach (var p in _blocks) VirtualFree(p, 0, FreeType.Release);
            _blocks.Clear();
            _allocatedBytes = 0;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    [Flags] private enum AllocationType : uint { Commit = 0x1000, Reserve = 0x2000 }
    private enum MemoryProtection : uint { ReadWrite = 0x04 }
    private enum FreeType : uint { Release = 0x8000 }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint lpAddress, nuint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(nint lpAddress, nuint dwSize, FreeType dwFreeType);

    [DllImport("kernel32.dll")]
    private static extern nuint VirtualQuery(nint lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, nuint dwLength);
}
