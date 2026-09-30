using System.Management;

namespace RamVramManager;

public sealed record SystemStats(long TotalRam, long AvailableRam, string GpuName, long DedicatedGpuMemory, long SharedGpuMemory);

public static class SystemStatsReader
{
    public static SystemStats Read()
    {
        var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var available = GetAvailableRam();
        var gpu = GetGpu();
        return new SystemStats(total, available, gpu.name, gpu.dedicated, gpu.shared);
    }

    private static long GetAvailableRam()
    {
        try
        {
            using var cs = new Microsoft.VisualBasic.Devices.ComputerInfo();
            return (long)cs.AvailablePhysicalMemory;
        }
        catch { return 0; }
    }

    private static (string name, long dedicated, long shared) GetGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, SharedSystemMemory FROM Win32_VideoController");
            foreach (ManagementObject m in searcher.Get())
            {
                var name = m["Name"]?.ToString() ?? "Unknown GPU";
                var dedicated = Convert.ToInt64(m["AdapterRAM"] ?? 0);
                var shared = Convert.ToInt64(m["SharedSystemMemory"] ?? 0);
                return (name, dedicated, shared);
            }
        }
        catch { }
        return ("GPU information unavailable", 0, 0);
    }
}
