using EKIPPP.Helpers;
using EKIPPP.Models;

namespace EKIPPP.Services;

public class FiveMService
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string FiveMAppPath => Path.Combine(LocalAppData, "FiveM", "FiveM.app");
    public string FiveMExePath => Path.Combine(LocalAppData, "FiveM", "FiveM.exe");
    public string DataPath     => Path.Combine(FiveMAppPath, "data");
    public string CrashesPath  => Path.Combine(FiveMAppPath, "crashes");
    public string LogsPath     => Path.Combine(FiveMAppPath, "logs");
    public string ModsPath     => Path.Combine(FiveMAppPath, "mods");
    public string PluginsPath  => Path.Combine(FiveMAppPath, "plugins");
    public string BloodFxPath  => Path.Combine(FiveMAppPath, "citizen", "common", "data", "effects");
    public string KillFxPath   => Path.Combine(FiveMAppPath, "citizen", "common", "data", "timecycle");

    public bool IsFiveMInstalled() => Directory.Exists(FiveMAppPath);

    public bool IsFiveMRunning() => ProcessHelper.IsFiveMRunning();

    public string GetFiveMVersion()
    {
        if (!File.Exists(FiveMExePath)) return "–";
        var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(FiveMExePath);
        return vi.ProductVersion ?? vi.FileVersion ?? "–";
    }

    public FiveMStats GetStats()
    {
        var stats = new FiveMStats
        {
            FiveMPath   = FiveMAppPath,
            IsInstalled = IsFiveMInstalled()
        };

        if (!stats.IsInstalled) return stats;

        stats.CacheSizeBytes = ComputeCacheSize();
        stats.LogFileCount   = CountFiles(LogsPath);
        stats.CrashFileCount = CountFiles(CrashesPath);
        stats.ModsSizeBytes  = FileSizeHelper.GetDirectorySize(ModsPath);
        stats.ModsFileCount  = CountFiles(ModsPath);
        stats.FiveMVersion   = GetFiveMVersion();

        return stats;
    }

    private long ComputeCacheSize()
    {
        if (!Directory.Exists(DataPath)) return 0;

        long total = 0;
        foreach (var entry in new DirectoryInfo(DataPath).EnumerateFileSystemInfos())
        {
            if (entry.Name.Equals("game-storage", StringComparison.OrdinalIgnoreCase)) continue;

            total += entry is DirectoryInfo di
                ? FileSizeHelper.GetDirectorySize(di.FullName)
                : ((FileInfo)entry).Length;
        }

        total += FileSizeHelper.GetDirectorySize(CrashesPath);
        total += FileSizeHelper.GetDirectorySize(LogsPath);
        return total;
    }

    private static int CountFiles(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
    }
}
