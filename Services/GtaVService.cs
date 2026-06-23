using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace EKIPPP.Services;

public class GtaVService
{
    private static readonly string[] CommonSuffixes =
    [
        @"Steam\steamapps\common\Grand Theft Auto V",
        @"SteamLibrary\steamapps\common\Grand Theft Auto V",
        @"Program Files\Steam\steamapps\common\Grand Theft Auto V",
        @"Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V",
        @"Program Files\Rockstar Games\Grand Theft Auto V",
        @"Program Files (x86)\Rockstar Games\Grand Theft Auto V",
        @"Rockstar Games\Grand Theft Auto V",
        @"Program Files\Epic Games\GTAV",
        @"Epic Games\GTAV",
        @"Games\Grand Theft Auto V",
        @"Games\GTAV",
    ];

    public string? Detect()
    {
        // 1. Steam via registre + toutes les bibliothèques Steam
        var steamRoot = ReadReg(Registry.CurrentUser,  @"Software\Valve\Steam", "SteamPath")
                     ?? ReadReg(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
                     ?? ReadReg(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        if (steamRoot != null)
        {
            var candidate = Path.Combine(steamRoot, "steamapps", "common", "Grand Theft Auto V");
            if (IsValid(candidate)) return candidate;

            foreach (var lib in ReadSteamLibraries(steamRoot))
            {
                candidate = Path.Combine(lib, "steamapps", "common", "Grand Theft Auto V");
                if (IsValid(candidate)) return candidate;
            }
        }

        // 2. Rockstar Games Launcher
        var rockstar = ReadReg(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV",            "InstallFolder")
                    ?? ReadReg(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "InstallFolder")
                    ?? ReadReg(Registry.LocalMachine, @"SOFTWARE\Rockstar Games\Grand Theft Auto V",             "InstallFolder");
        if (rockstar != null && IsValid(rockstar)) return rockstar;

        // 3. Epic Games via manifests (couvre n'importe quel lecteur)
        var epicManifestDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (Directory.Exists(epicManifestDir))
        {
            foreach (var item in Directory.GetFiles(epicManifestDir, "*.item"))
            {
                try
                {
                    var json = File.ReadAllText(item);
                    if (!json.Contains("\"GTAV\"", StringComparison.OrdinalIgnoreCase) &&
                        !json.Contains("\"Grand Theft Auto V\"", StringComparison.OrdinalIgnoreCase)) continue;
                    var match = Regex.Match(json, @"""InstallLocation""\s*:\s*""([^""]+)""");
                    if (!match.Success) continue;
                    var loc = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (IsValid(loc)) return loc;
                }
                catch { }
            }
        }

        // 4. Scan tous les lecteurs fixes avec les suffixes courants
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            foreach (var suffix in CommonSuffixes)
            {
                var candidate = Path.Combine(drive.Name, suffix);
                if (IsValid(candidate)) return candidate;
            }
        }

        return null;
    }

    public string SfxPath(string gtaRoot) =>
        Path.Combine(gtaRoot, "x64", "audio", "sfx");

    public bool IsGtaValid(string? path) =>
        !string.IsNullOrWhiteSpace(path) && IsValid(path);

    public async Task InstallFilesAsync(IEnumerable<string> sourceFiles, string gtaRoot,
                                        IProgress<(string msg, double pct)>? progress = null)
    {
        var sfx = SfxPath(gtaRoot);
        if (!Directory.Exists(sfx))
            throw new DirectoryNotFoundException($"Dossier sfx introuvable :\n{sfx}");

        var list = sourceFiles.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var src  = list[i];
            var name = Path.GetFileName(src);
            progress?.Report(($"Copie de {name}…", (double)i / list.Count * 100));
            var dest = Path.Combine(sfx, name);
            await Task.Run(async () =>
            {
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    try
                    {
                        File.Copy(src, dest, overwrite: true);
                        return;
                    }
                    catch (IOException) when (attempt < 3)
                    {
                        await Task.Delay(1000);
                    }
                }
                // 4e tentative — laisse l'exception remonter avec message clair
                try { File.Copy(src, dest, overwrite: true); }
                catch (IOException)
                {
                    throw new IOException(
                        $"Impossible d'écrire '{name}' : le fichier est verrouillé.\n" +
                        "Fermez GTA V, le Rockstar Games Launcher et Steam, puis réessayez.");
                }
            });
        }
        progress?.Report(("Sons installés.", 100));
    }

    private static bool IsValid(string path) =>
        Directory.Exists(path) && File.Exists(Path.Combine(path, "GTA5.exe"));

    private static IEnumerable<string> ReadSteamLibraries(string steamRoot)
    {
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        foreach (var line in File.ReadLines(vdf))
        {
            var t = line.Trim();
            if (!t.Contains("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = t.Split('"', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                yield return parts[^1].Replace(@"\\", @"\");
        }
    }

    private static string? ReadReg(RegistryKey hive, string subKey, string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(subKey);
            return key?.GetValue(valueName)?.ToString();
        }
        catch { return null; }
    }
}
