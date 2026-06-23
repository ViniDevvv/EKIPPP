using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EKIPPP.Services;

public sealed class PcStatsService : IDisposable
{
    private readonly PerformanceCounter _cpu;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public PcStatsService()
    {
        _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
        _cpu.NextValue(); // première valeur toujours 0 — warm-up
    }

    public float GetCpuPercent() => Math.Min(100f, _cpu.NextValue());

    public (ulong available, ulong total) GetRam()
    {
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref ms);
        return (ms.ullAvailPhys, ms.ullTotalPhys);
    }

    public (long free, long total) GetDisk(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path) ?? "C:\\";
            var d = new DriveInfo(root);
            return (d.AvailableFreeSpace, d.TotalSize);
        }
        catch { return (0, 0); }
    }

    public void Dispose() => _cpu.Dispose();
}
