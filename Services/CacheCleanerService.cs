using System.IO;

namespace EKIPPP.Services;

public record CleanResult(int FilesDeleted, long BytesFreed);

public class CacheCleanerService(FiveMService fiveM)
{
    public async Task<CleanResult> CleanAsync(IProgress<string>? progress = null)
    {
        int files = 0; long bytes = 0;
        await Task.Run(() =>
        {
            CleanDirectory(fiveM.DataPath,    skipName: "game-storage", ref files, ref bytes, progress);
            CleanDirectory(fiveM.CrashesPath, skipName: null,           ref files, ref bytes, progress);
            CleanDirectory(fiveM.LogsPath,    skipName: null,           ref files, ref bytes, progress);
        });
        return new CleanResult(files, bytes);
    }

    public async Task<CleanResult> CleanCacheOnlyAsync(IProgress<string>? progress = null)
    {
        int files = 0; long bytes = 0;
        await Task.Run(() => CleanDirectory(fiveM.DataPath, skipName: "game-storage", ref files, ref bytes, progress));
        return new CleanResult(files, bytes);
    }

    public async Task<CleanResult> DeleteLogsAsync(IProgress<string>? progress = null)
    {
        int files = 0; long bytes = 0;
        await Task.Run(() => CleanDirectory(fiveM.LogsPath, skipName: null, ref files, ref bytes, progress));
        return new CleanResult(files, bytes);
    }

    public async Task<CleanResult> DeleteCrashesAsync(IProgress<string>? progress = null)
    {
        int files = 0; long bytes = 0;
        await Task.Run(() => CleanDirectory(fiveM.CrashesPath, skipName: null, ref files, ref bytes, progress));
        return new CleanResult(files, bytes);
    }

    private static void CleanDirectory(string path, string? skipName, ref int files, ref long bytes, IProgress<string>? progress)
    {
        if (!Directory.Exists(path)) return;
        foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            if (skipName != null && entry.Name.Equals(skipName, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (entry is FileInfo fi)      { bytes += fi.Length; fi.Delete(); files++; progress?.Report($"Supprimé : {fi.Name}"); }
                else if (entry is DirectoryInfo di) { bytes += GetDirSize(di); files += di.GetFiles("*", SearchOption.AllDirectories).Length; di.Delete(true); progress?.Report($"Dossier supprimé : {di.Name}"); }
            }
            catch (Exception ex) { progress?.Report($"Ignoré ({entry.Name}) : {ex.Message}"); }
        }
    }

    private static long GetDirSize(DirectoryInfo dir)
        => dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
}
