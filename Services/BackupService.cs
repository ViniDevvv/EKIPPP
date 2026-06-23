using System.IO.Compression;

namespace EKIPPP.Services;

public class BackupService(FiveMService fiveM)
{
    private static readonly string BackupsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EKIPPP", "Backups");

    public string BackupsFolderPath => BackupsFolder;

    public async Task<string> CreateBackupAsync(IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(BackupsFolder);
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string zipPath = Path.Combine(BackupsFolder, $"FiveM_backup_{stamp}.zip");

        progress?.Report("Création de la sauvegarde en cours…");
        await Task.Run(() => ZipFile.CreateFromDirectory(fiveM.FiveMAppPath, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false));
        progress?.Report($"Sauvegarde créée : {zipPath}");
        return zipPath;
    }

    public async Task RestoreBackupAsync(string zipPath, IProgress<string>? progress = null)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Fichier de sauvegarde introuvable.", zipPath);

        progress?.Report("Restauration en cours…");
        await Task.Run(() =>
        {
            if (Directory.Exists(fiveM.FiveMAppPath))
                Directory.Delete(fiveM.FiveMAppPath, recursive: true);

            ZipFile.ExtractToDirectory(zipPath, fiveM.FiveMAppPath);
        });
        progress?.Report("Restauration terminée.");
    }

    public string[] ListBackups()
    {
        if (!Directory.Exists(BackupsFolder)) return [];
        return Directory.GetFiles(BackupsFolder, "*.zip")
                        .OrderByDescending(f => f)
                        .ToArray();
    }
}
