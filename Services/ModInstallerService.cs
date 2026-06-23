namespace EKIPPP.Services;

public class ModInstallerService(FiveMService fiveM)
{
    public async Task<string> InstallRpfAsync(string sourcePath, IProgress<string>? progress = null)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Fichier RPF introuvable.", sourcePath);

        Directory.CreateDirectory(fiveM.ModsPath);

        string dest = Path.Combine(fiveM.ModsPath, Path.GetFileName(sourcePath));
        progress?.Report($"Copie de {Path.GetFileName(sourcePath)}…");

        await Task.Run(() => File.Copy(sourcePath, dest, overwrite: true));

        progress?.Report($"Mod installé → {dest}");
        return dest;
    }
}
