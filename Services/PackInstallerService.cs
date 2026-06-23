using System.IO.Compression;

namespace EKIPPP.Services;

public class PackInstallerService(FiveMService fiveM)
{
    public async Task InstallFromFolderAsync(string sourcePath, IProgress<(string msg, double pct)>? progress = null)
    {
        var (src, dest) = ResolvePackPaths(sourcePath)
            ?? throw new InvalidOperationException("Dossier 'citizen' ou 'data' introuvable dans le pack sélectionné.");

        await CopyPackAsync(src, dest, progress);
    }

    public async Task InstallFromZipAsync(string zipPath, IProgress<(string msg, double pct)>? progress = null)
    {
        string temp = Path.Combine(Path.GetTempPath(), $"ekippp_{Guid.NewGuid():N}");
        try
        {
            progress?.Report(("Extraction de l'archive…", 0));
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, temp, overwriteFiles: true));

            var (src, dest) = ResolvePackPaths(temp)
                ?? throw new InvalidOperationException("Dossier 'citizen' ou 'data' introuvable dans le ZIP.");

            await CopyPackAsync(src, dest, progress);
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    // Retourne (source à copier, destination dans FiveM.app)
    private (string src, string dest)? ResolvePackPaths(string basePath)
    {
        // --- Pack citizen : toujours copier uniquement citizen → FiveM.app\citizen\ ---
        string? citizenSrc = FindNamedFolder(basePath, "citizen");
        if (citizenSrc != null)
            return (citizenSrc, Path.Combine(fiveM.FiveMAppPath, "citizen"));

        // --- Pack data → FiveM.app\citizen\common\data\ ---
        string? dataSrc = FindNamedFolder(basePath, "data");
        if (dataSrc != null)
            return (dataSrc, Path.Combine(fiveM.FiveMAppPath, "citizen", "common", "data"));

        return null;
    }

    private static string? FindNamedFolder(string basePath, string folderName)
    {
        // L'utilisateur a sélectionné ce dossier directement
        if (Path.GetFileName(basePath).Equals(folderName, StringComparison.OrdinalIgnoreCase))
            return basePath;

        // Le dossier est à la racine du chemin sélectionné
        string direct = Path.Combine(basePath, folderName);
        if (Directory.Exists(direct))
            return direct;

        // Recherche récursive
        return Directory.GetDirectories(basePath, folderName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static async Task CopyPackAsync(string src, string dest, IProgress<(string msg, double pct)>? progress)
    {
        var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
        int total = files.Length, done = 0;

        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(src, file);
            string target   = Path.Combine(dest, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await Task.Run(() => File.Copy(file, target, overwrite: true));

            done++;
            progress?.Report(($"Copie : {relative}", (double)done / total * 100));
        }
    }
}
