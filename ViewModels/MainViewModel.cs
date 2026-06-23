using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EKIPPP.Helpers;
using EKIPPP.Models;
using EKIPPP.Services;
using Microsoft.Win32;

namespace EKIPPP.ViewModels;

public record BackupInfo(string FilePath, string Name, string DateDisplay, string SizeDisplay);
public record InstalledFileInfo(string FilePath, string Name, string SizeDisplay, string Location);

public partial class MainViewModel : BaseViewModel
{
    private readonly FiveMService         _fiveM;
    private readonly CacheCleanerService  _cleaner;
    private readonly ModInstallerService  _modInst;
    private readonly PackInstallerService _packInst;
    private readonly BackupService        _backup;
    private readonly PcStatsService       _pc;
    private readonly SettingsService      _settings;
    private readonly GtaVService          _gtaV;

    // ── FiveM Stats ────────────────────────────────────────────────────
    [ObservableProperty] private string _cacheSizeDisplay = "–";
    [ObservableProperty] private int    _logFileCount     = 0;
    [ObservableProperty] private int    _crashFileCount   = 0;
    [ObservableProperty] private string _modsSizeDisplay  = "–";
    [ObservableProperty] private int    _modsFileCount    = 0;
    [ObservableProperty] private bool   _fiveMDetected    = false;
    [ObservableProperty] private bool   _fiveMRunning     = false;
    [ObservableProperty] private string _fiveMVersion     = "–";

    // ── Cumulative Stats ───────────────────────────────────────────────
    [ObservableProperty] private string _totalFreedDisplay = "–";
    [ObservableProperty] private int    _totalCleanCount   = 0;

    // ── Settings ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _launchOnStartup   = false;
    [ObservableProperty] private bool _autoCleanOnLaunch = false;

    // ── PC Stats ───────────────────────────────────────────────────────
    [ObservableProperty] private string _cpuDisplay  = "–";
    [ObservableProperty] private string _ramDisplay  = "–";
    [ObservableProperty] private string _diskDisplay = "–";
    [ObservableProperty] private double _cpuPercent  = 0;
    [ObservableProperty] private double _ramPercent  = 0;

    // ── Session / DaysUsed ─────────────────────────────────────────────
    [ObservableProperty] private string _daysUsedDisplay = "–";

    // ── UI State ───────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isBusy        = false;
    [ObservableProperty] private double _progressValue  = 0;
    [ObservableProperty] private string _statusMessage  = "Prêt";
    [ObservableProperty] private int    _selectedTab    = 0;

    // ── Logs viewer ────────────────────────────────────────────────────
    [ObservableProperty] private string _fiveMLogsContent = "Aucun log disponible.";

    // ── GTA V ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _gtaVPathDisplay = "Non détecté";
    [ObservableProperty] private bool   _gtaVDetected    = false;

    // ── Mise à jour ────────────────────────────────────────────────────
    private const string VersionCheckUrl = "https://gist.githubusercontent.com/ViniDevvv/9015cffc61473d0edc2c7e7a93a111c5/raw/version.json";
    private const string CurrentVersion  = "1.0";

    [ObservableProperty] private bool   _updateAvailable = false;
    [ObservableProperty] private string _updateVersion   = "";
    [ObservableProperty] private string _updateChangelog = "";
    [ObservableProperty] private string _updateUrl       = "";

    partial void OnSelectedTabChanged(int value)
    {
        for (int i = 0; i <= 4; i++)
            OnPropertyChanged($"IsTab{i}Active");
        if (value == 2) RefreshInstalledPacksCore();
        if (value == 3) RefreshBackups();
    }

    public bool IsTab0Active => SelectedTab == 0;
    public bool IsTab1Active => SelectedTab == 1;
    public bool IsTab2Active => SelectedTab == 2;
    public bool IsTab3Active => SelectedTab == 3;
    public bool IsTab4Active => SelectedTab == 4;

    public ObservableCollection<ActivityLogEntry> Logs    { get; } = [];
    public ObservableCollection<BackupInfo>       Backups { get; } = [];

    public ObservableCollection<InstalledFileInfo> InstalledRpfs    { get; } = [];
    public ObservableCollection<InstalledFileInfo> InstalledBloodFx { get; } = [];
    public ObservableCollection<InstalledFileInfo> InstalledKillFx  { get; } = [];
    public ObservableCollection<InstalledFileInfo> InstalledSounds  { get; } = [];

    public Action<string, string>? ShowToast { get; set; }

    public MainViewModel()
    {
        _fiveM    = new FiveMService();
        _cleaner  = new CacheCleanerService(_fiveM);
        _modInst  = new ModInstallerService(_fiveM);
        _packInst = new PackInstallerService(_fiveM);
        _backup   = new BackupService(_fiveM);
        _pc       = new PcStatsService();
        _gtaV     = new GtaVService();

        _settings = new SettingsService();
        _settings.Load();
        LaunchOnStartup    = _settings.Current.LaunchOnStartup;
        AutoCleanOnLaunch  = _settings.Current.AutoCleanOnLaunch;

        var savedGta = _settings.Current.GtaVPath;
        if (_gtaV.IsGtaValid(savedGta))
        {
            GtaVPathDisplay = savedGta!;
            GtaVDetected    = true;
        }

        if (_settings.Current.FirstRun)
        {
            _settings.Current.FirstRun = false;
            _settings.Save();
        }

        UpdateDaysUsed();
        RefreshStatsCore();
        RefreshPcStats();
        _ = CheckForUpdatesAsync();

        if (AutoCleanOnLaunch && !FiveMRunning)
            _ = CleanSilentAsync();

        var fivemTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        fivemTimer.Tick += (_, _) => RefreshStatsCore(silent: true);
        fivemTimer.Start();

        var pcTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        pcTimer.Tick += (_, _) => RefreshPcStats();
        pcTimer.Start();
    }

    // ── Navigation ─────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectTab(string? tabStr)
    {
        if (int.TryParse(tabStr, out int tab))
            SelectedTab = tab;
    }

    // ── Stats FiveM ────────────────────────────────────────────────────
    [RelayCommand]
    private void RefreshStats() => RefreshStatsCore(silent: false);

    private void RefreshStatsCore(bool silent = false)
    {
        try
        {
            var s = _fiveM.GetStats();
            FiveMDetected    = s.IsInstalled;
            CacheSizeDisplay = FileSizeHelper.Format(s.CacheSizeBytes);
            LogFileCount     = s.LogFileCount;
            CrashFileCount   = s.CrashFileCount;
            ModsSizeDisplay  = FileSizeHelper.Format(s.ModsSizeBytes);
            ModsFileCount    = s.ModsFileCount;
            FiveMRunning     = _fiveM.IsFiveMRunning();
            FiveMVersion     = s.FiveMVersion;
            TotalFreedDisplay = FileSizeHelper.Format(_settings.Current.TotalBytesFreed);
            TotalCleanCount   = _settings.Current.CleanCount;
            if (!silent) AddLog("Statistiques actualisées.", LogType.Info);
        }
        catch (Exception ex) { AddLog($"Erreur stats : {ex.Message}", LogType.Error); }
    }

    // ── Stats PC ───────────────────────────────────────────────────────
    private void RefreshPcStats()
    {
        try
        {
            var cpu = _pc.GetCpuPercent();
            CpuDisplay = $"{cpu:F0}%";
            CpuPercent = Math.Clamp(cpu, 0, 100);

            var (ramFree, ramTotal) = _pc.GetRam();
            double ramUsed = (ramTotal - ramFree) / 1_073_741_824.0;
            double ramTot  = ramTotal / 1_073_741_824.0;
            RamDisplay  = $"{ramUsed:F1} / {ramTot:F1} Go";
            RamPercent  = ramTotal > 0 ? Math.Clamp((ramTotal - ramFree) * 100.0 / ramTotal, 0, 100) : 0;

            var (diskFree, _) = _pc.GetDisk(_fiveM.FiveMAppPath);
            double diskFreeGb = diskFree / 1_073_741_824.0;
            DiskDisplay = $"{diskFreeGb:F0} Go libres";

            FiveMRunning = _fiveM.IsFiveMRunning();
        }
        catch { /* silent */ }
    }

    // ── Lancer FiveM ───────────────────────────────────────────────────
    [RelayCommand]
    private void LaunchFiveM()
    {
        var exe = _fiveM.FiveMExePath;
        if (!File.Exists(exe))
        {
            AddLog("FiveM.exe introuvable.", LogType.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        AddLog("FiveM lancé.", LogType.Success);
    }

    // ── Nettoyage ──────────────────────────────────────────────────────
    [RelayCommand]
    private async Task CleanCacheAsync()
    {
        if (!CheckFiveMClosed()) return;
        if (!Confirm("Voulez-vous nettoyer le cache FiveM (data, logs, crashes) ?")) return;
        await RunBusyAsync("Nettoyage complet…", async () =>
        {
            var r = await _cleaner.CleanAsync(Progress(null));
            UpdateCumulativeStats(r.BytesFreed, r.FilesDeleted);
            Done($"Nettoyage terminé : {r.FilesDeleted} fichiers, {FileSizeHelper.Format(r.BytesFreed)} récupérés.");
            RefreshStatsCore(silent: true);
        });
    }

    [RelayCommand]
    private async Task DeleteLogsAsync()
    {
        if (!CheckFiveMClosed()) return;
        if (!Confirm($"Supprimer les {LogFileCount} fichier(s) de logs ?")) return;
        await RunBusyAsync("Suppression des logs…", async () =>
        {
            var r = await _cleaner.DeleteLogsAsync(Progress(null));
            UpdateCumulativeStats(r.BytesFreed, r.FilesDeleted);
            Done($"Logs supprimés : {r.FilesDeleted} fichier(s), {FileSizeHelper.Format(r.BytesFreed)} libérés.");
            RefreshStatsCore(silent: true);
        });
    }

    [RelayCommand]
    private async Task DeleteCrashesAsync()
    {
        if (!CheckFiveMClosed()) return;
        if (!Confirm($"Supprimer les {CrashFileCount} rapport(s) de crash ?")) return;
        await RunBusyAsync("Suppression des crashes…", async () =>
        {
            var r = await _cleaner.DeleteCrashesAsync(Progress(null));
            UpdateCumulativeStats(r.BytesFreed, r.FilesDeleted);
            Done($"Crashes supprimés : {r.FilesDeleted} fichier(s), {FileSizeHelper.Format(r.BytesFreed)} libérés.");
            RefreshStatsCore(silent: true);
        });
    }

    // ── Nettoyage silencieux (auto) ────────────────────────────────────
    private async Task CleanSilentAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Nettoyage automatique…";
            var r = await _cleaner.CleanAsync(null);
            UpdateCumulativeStats(r.BytesFreed, r.FilesDeleted);
            StatusMessage = $"Nettoyage auto : {FileSizeHelper.Format(r.BytesFreed)} libérés.";
            RefreshStatsCore(silent: true);
        }
        catch { }
        finally { IsBusy = false; }
    }

    // ── Stats cumulées ─────────────────────────────────────────────────
    private void UpdateCumulativeStats(long bytes, int files)
    {
        _settings.Current.TotalBytesFreed   += bytes;
        _settings.Current.TotalFilesDeleted += files;
        _settings.Current.CleanCount++;
        _settings.Save();
        TotalFreedDisplay = FileSizeHelper.Format(_settings.Current.TotalBytesFreed);
        TotalCleanCount   = _settings.Current.CleanCount;
    }

    // ── Paramètres ─────────────────────────────────────────────────────
    [RelayCommand]
    private void ToggleLaunchOnStartup()
    {
        LaunchOnStartup = !LaunchOnStartup;
        _settings.Current.LaunchOnStartup = LaunchOnStartup;
        _settings.SetWindowsStartup(LaunchOnStartup);
        _settings.Save();
    }

    [RelayCommand]
    private void ToggleAutoClean()
    {
        AutoCleanOnLaunch = !AutoCleanOnLaunch;
        _settings.Current.AutoCleanOnLaunch = AutoCleanOnLaunch;
        _settings.Save();
    }

    // ── Logs viewer ────────────────────────────────────────────────────
    [RelayCommand]
    private void LoadFiveMLog()
    {
        try
        {
            var logDir = _fiveM.LogsPath;
            if (!Directory.Exists(logDir))
            {
                FiveMLogsContent = "Dossier logs introuvable.";
                return;
            }
            var latest = Directory.GetFiles(logDir, "*.log")
                                  .OrderByDescending(File.GetLastWriteTime)
                                  .FirstOrDefault();
            if (latest == null)
            {
                FiveMLogsContent = "Aucun fichier log trouvé.";
                return;
            }
            var lines = File.ReadLines(latest).TakeLast(300).ToList();
            FiveMLogsContent = string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            FiveMLogsContent = $"Erreur : {ex.Message}";
        }
    }

    // ── GTA V ─────────────────────────────────────────────────────────
    [RelayCommand]
    private void DetectGtaV()
    {
        StatusMessage = "Recherche de GTA V…";
        var path = _gtaV.Detect();
        if (path != null)
        {
            GtaVPathDisplay = path;
            GtaVDetected    = true;
            _settings.Current.GtaVPath = path;
            _settings.Save();
            AddLog($"GTA V détecté : {path}", LogType.Success);
            StatusMessage = "GTA V trouvé !";
        }
        else
        {
            GtaVPathDisplay = "Non trouvé – utilisez Parcourir";
            GtaVDetected    = false;
            AddLog("GTA V introuvable automatiquement. Utilisez 'Parcourir'.", LogType.Warning);
            StatusMessage   = "GTA V non trouvé";
        }
    }

    [RelayCommand]
    private void BrowseGtaV()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
            { Title = "Sélectionner le dossier GTA V (contenant GTA5.exe)" };
        if (dlg.ShowDialog() != true) return;
        if (!_gtaV.IsGtaValid(dlg.FolderName))
        {
            System.Windows.MessageBox.Show(
                "GTA5.exe introuvable dans ce dossier.\nVeuillez sélectionner le dossier racine de GTA V.",
                "EKIPPP – Dossier invalide",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        GtaVPathDisplay = dlg.FolderName;
        GtaVDetected    = true;
        _settings.Current.GtaVPath = dlg.FolderName;
        _settings.Save();
        AddLog($"Chemin GTA V défini manuellement : {dlg.FolderName}", LogType.Success);
    }

    [RelayCommand]
    private async Task InstallSoundPackAsync()
    {
        if (!GtaVDetected)
        {
            System.Windows.MessageBox.Show(
                "Veuillez d'abord détecter ou sélectionner votre dossier GTA V.",
                "EKIPPP", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        var dlg = new OpenFileDialog
        {
            Title      = "Sélectionner les fichiers audio du pack sons",
            Filter     = "Fichiers audio|*.awc;*.oac;*.dat;*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;
        await RunBusyAsync("Installation du pack sons…", async () =>
        {
            string sfxRoot = Path.Combine(GtaVPathDisplay, "x64", "audio", "sfx");
            await _gtaV.InstallFilesAsync(dlg.FileNames, GtaVPathDisplay, ProgressPct());
            _settings.Current.InstalledSoundFiles ??= [];
            foreach (var f in dlg.FileNames)
            {
                var dest = Path.Combine(sfxRoot, Path.GetFileName(f));
                if (!_settings.Current.InstalledSoundFiles.Contains(dest))
                    _settings.Current.InstalledSoundFiles.Add(dest);
            }
            _settings.Save();
            Done($"{dlg.FileNames.Length} fichier(s) copiés dans x64\\audio\\sfx.");
            RefreshInstalledPacksCore();
        });
    }

    // ── Installation ───────────────────────────────────────────────────
    [RelayCommand]
    private async Task InstallModAsync()
    {
        if (!CheckFiveMClosed()) return;
        var dlg = new OpenFileDialog { Title = "Sélectionner un mod .rpf", Filter = "Fichiers RPF (*.rpf)|*.rpf" };
        if (dlg.ShowDialog() != true) return;
        await RunBusyAsync("Installation du mod…", async () =>
        {
            string dest = await _modInst.InstallRpfAsync(dlg.FileName, new Progress<string>(msg => { StatusMessage = msg; AddLog(msg, LogType.Info); }));
            Done($"Mod installé → {dest}");
            RefreshStatsCore(silent: true);
        });
    }

    [RelayCommand]
    private async Task InstallPackAsync()
    {
        if (!CheckFiveMClosed()) return;
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Sélectionnez le dossier citizen à installer" };
        if (dlg.ShowDialog() != true) return;
        await RunBusyAsync("Installation du pack citizen…", async () =>
        {
            await _packInst.InstallFromFolderAsync(dlg.FolderName, ProgressPct());
            Done("Pack Citizen installé avec succès.");
        });
    }

    [RelayCommand]
    private async Task InstallDataAsync()
    {
        if (!CheckFiveMClosed()) return;
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Sélectionnez le dossier data à installer" };
        if (dlg.ShowDialog() != true) return;
        await RunBusyAsync("Installation du pack data…", async () =>
        {
            await _packInst.InstallFromFolderAsync(dlg.FolderName, ProgressPct());
            Done("Pack Data installé avec succès.");
        });
    }

    [RelayCommand]
    private async Task InstallReShadePresetAsync()
    {
        var dlg = new OpenFileDialog { Title = "Sélectionner un preset ReShade (.ini)", Filter = "Preset ReShade (*.ini)|*.ini" };
        if (dlg.ShowDialog() != true) return;
        await RunBusyAsync("Installation du preset ReShade…", async () =>
        {
            Directory.CreateDirectory(_fiveM.PluginsPath);
            string dest = Path.Combine(_fiveM.PluginsPath, Path.GetFileName(dlg.FileName));
            await Task.Run(() => File.Copy(dlg.FileName, dest, overwrite: true));
            Done($"Preset ReShade installé → {Path.GetFileName(dest)}");
            RefreshInstalledPacksCore();
        });
    }

    [RelayCommand]
    private async Task InstallBloodFxAsync()
    {
        if (!CheckFiveMClosed()) return;
        var dlg = new OpenFileDialog { Title = "Sélectionner les fichiers BloodFX", Filter = "Tous les fichiers (*.*)|*.*", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        await RunBusyAsync("Installation BloodFX…", async () =>
        {
            Directory.CreateDirectory(_fiveM.BloodFxPath);
            foreach (var f in dlg.FileNames)
                await Task.Run(() => File.Copy(f, Path.Combine(_fiveM.BloodFxPath, Path.GetFileName(f)), overwrite: true));
            Done($"{dlg.FileNames.Length} fichier(s) BloodFX installé(s).");
            RefreshInstalledPacksCore();
        });
    }

    [RelayCommand]
    private async Task InstallKillFxAsync()
    {
        if (!CheckFiveMClosed()) return;
        var dlg = new OpenFileDialog { Title = "Sélectionner les fichiers KillFX", Filter = "Tous les fichiers (*.*)|*.*", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        await RunBusyAsync("Installation KillFX…", async () =>
        {
            Directory.CreateDirectory(_fiveM.KillFxPath);
            foreach (var f in dlg.FileNames)
                await Task.Run(() => File.Copy(f, Path.Combine(_fiveM.KillFxPath, Path.GetFileName(f)), overwrite: true));
            Done($"{dlg.FileNames.Length} fichier(s) KillFX installé(s).");
            RefreshInstalledPacksCore();
        });
    }

    // ── Gestion des packs installés ────────────────────────────────────
    [RelayCommand]
    private void RefreshInstalledPacks() => RefreshInstalledPacksCore();

    private void RefreshInstalledPacksCore()
    {
        InstalledRpfs.Clear();
        foreach (var dir in new[] { _fiveM.PluginsPath, _fiveM.ModsPath })
        {
            if (!Directory.Exists(dir)) continue;
            string loc = dir == _fiveM.PluginsPath ? "plugins\\" : "mods\\";
            foreach (var f in Directory.GetFiles(dir, "*.rpf").OrderBy(Path.GetFileName))
            {
                var fi = new FileInfo(f);
                InstalledRpfs.Add(new InstalledFileInfo(f, fi.Name, FileSizeHelper.Format(fi.Length), loc));
            }
        }

        InstalledBloodFx.Clear();
        if (Directory.Exists(_fiveM.BloodFxPath))
            foreach (var f in Directory.GetFiles(_fiveM.BloodFxPath).OrderBy(Path.GetFileName))
            {
                var fi = new FileInfo(f);
                InstalledBloodFx.Add(new InstalledFileInfo(f, fi.Name, FileSizeHelper.Format(fi.Length), "effects\\"));
            }

        InstalledKillFx.Clear();
        if (Directory.Exists(_fiveM.KillFxPath))
            foreach (var f in Directory.GetFiles(_fiveM.KillFxPath).OrderBy(Path.GetFileName))
            {
                var fi = new FileInfo(f);
                InstalledKillFx.Add(new InstalledFileInfo(f, fi.Name, FileSizeHelper.Format(fi.Length), "timecycle\\"));
            }

        InstalledSounds.Clear();
        var validSounds = new List<string>();
        foreach (var path in _settings.Current.InstalledSoundFiles ?? [])
        {
            if (!File.Exists(path)) continue;
            validSounds.Add(path);
            var fi = new FileInfo(path);
            InstalledSounds.Add(new InstalledFileInfo(path, fi.Name, FileSizeHelper.Format(fi.Length), "x64\\audio\\sfx\\"));
        }
        if (validSounds.Count != (_settings.Current.InstalledSoundFiles?.Count ?? 0))
        {
            _settings.Current.InstalledSoundFiles = validSounds;
            _settings.Save();
        }
    }

    [RelayCommand]
    private void DeleteInstalledFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        string name = Path.GetFileName(filePath);
        if (!Confirm($"Supprimer '{name}' ?")) return;
        try
        {
            File.Delete(filePath);
            if (_settings.Current.InstalledSoundFiles?.Remove(filePath) == true)
                _settings.Save();
            AddLog($"Supprimé : {name}", LogType.Info);
        }
        catch (Exception ex) { AddLog($"Erreur suppression : {ex.Message}", LogType.Error); }
        RefreshInstalledPacksCore();
    }

    // ── Sauvegarde ─────────────────────────────────────────────────────
    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (!CheckFiveMClosed()) return;
        await RunBusyAsync("Création de la sauvegarde…", async () =>
        {
            string path = await _backup.CreateBackupAsync(new Progress<string>(msg => { StatusMessage = msg; AddLog(msg, LogType.Info); }));
            Done($"Sauvegarde créée : {System.IO.Path.GetFileName(path)}");
            RefreshBackups();
        });
    }

    [RelayCommand]
    private async Task RestoreSelectedBackupAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        if (!CheckFiveMClosed()) return;
        if (!Confirm($"Restaurer '{System.IO.Path.GetFileName(filePath)}' ?\nCela remplacera les fichiers FiveM actuels.")) return;
        await RunBusyAsync("Restauration en cours…", async () =>
        {
            await _backup.RestoreBackupAsync(filePath, new Progress<string>(msg => { StatusMessage = msg; AddLog(msg, LogType.Info); }));
            Done("Restauration terminée avec succès.");
        });
    }

    [RelayCommand]
    private void DeleteBackup(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        if (!Confirm($"Supprimer '{System.IO.Path.GetFileName(filePath)}' ?")) return;
        try { File.Delete(filePath); } catch { }
        AddLog($"Sauvegarde supprimée : {System.IO.Path.GetFileName(filePath)}", LogType.Info);
        RefreshBackups();
    }

    [RelayCommand]
    private void RefreshBackups()
    {
        Backups.Clear();
        foreach (var path in _backup.ListBackups())
        {
            var fi = new FileInfo(path);
            Backups.Add(new BackupInfo(
                FilePath:    path,
                Name:        fi.Name,
                DateDisplay: fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm"),
                SizeDisplay: Helpers.FileSizeHelper.Format(fi.Length)
            ));
        }
    }

    [RelayCommand]
    private void OpenBackupsFolder()
    {
        Directory.CreateDirectory(_backup.BackupsFolderPath);
        System.Diagnostics.Process.Start("explorer.exe", _backup.BackupsFolderPath);
    }

    // ── Actions rapides ────────────────────────────────────────────────
    [RelayCommand]
    private void OpenFiveMFolder()
    {
        if (Directory.Exists(_fiveM.FiveMAppPath)) Process.Start("explorer.exe", _fiveM.FiveMAppPath);
        else AddLog("Dossier FiveM introuvable.", LogType.Warning);
    }

    // ── Drag & Drop depuis l'UI ────────────────────────────────────────
    public async Task InstallFromDropAsync(string[] paths, string dropType)
    {
        if (paths.Length == 0) return;
        var first = paths[0];

        switch (dropType)
        {
            case "rpf" when File.Exists(first) && System.IO.Path.GetExtension(first).Equals(".rpf", StringComparison.OrdinalIgnoreCase):
                if (!CheckFiveMClosed()) return;
                await RunBusyAsync("Installation du mod…", async () =>
                {
                    string dest = await _modInst.InstallRpfAsync(first, Progress(null));
                    Done($"Mod installé → {System.IO.Path.GetFileName(dest)}");
                    RefreshStatsCore(silent: true);
                });
                break;
            case "citizen":
                if (!CheckFiveMClosed()) return;
                await RunBusyAsync("Installation du pack citizen…", async () =>
                {
                    await _packInst.InstallFromFolderAsync(first, ProgressPct());
                    Done("Pack Citizen installé avec succès.");
                });
                break;
            case "data":
                if (!CheckFiveMClosed()) return;
                await RunBusyAsync("Installation du pack data…", async () =>
                {
                    await _packInst.InstallFromFolderAsync(first, ProgressPct());
                    Done("Pack Data installé avec succès.");
                });
                break;
            case "sounds":
                if (!GtaVDetected)
                {
                    System.Windows.MessageBox.Show("Veuillez d'abord détecter GTA V.", "EKIPPP",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (!CheckGtaVClosed()) return;
                var soundFiles = paths.Where(File.Exists).ToArray();
                if (soundFiles.Length == 0) return;
                await RunBusyAsync("Installation des sons…", async () =>
                {
                    string sfxRoot = Path.Combine(GtaVPathDisplay, "x64", "audio", "sfx");
                    await _gtaV.InstallFilesAsync(soundFiles, GtaVPathDisplay, ProgressPct());
                    _settings.Current.InstalledSoundFiles ??= [];
                    foreach (var f in soundFiles)
                    {
                        var dest = Path.Combine(sfxRoot, Path.GetFileName(f));
                        if (!_settings.Current.InstalledSoundFiles.Contains(dest))
                            _settings.Current.InstalledSoundFiles.Add(dest);
                    }
                    _settings.Save();
                    Done($"{soundFiles.Length} fichier(s) audio installé(s) dans x64\\audio\\sfx.");
                    RefreshInstalledPacksCore();
                });
                break;
            case "bloodfx":
                if (!CheckFiveMClosed()) return;
                var bloodFiles = paths.Where(File.Exists).ToArray();
                if (bloodFiles.Length == 0) return;
                await RunBusyAsync("Installation BloodFX…", async () =>
                {
                    Directory.CreateDirectory(_fiveM.BloodFxPath);
                    foreach (var f in bloodFiles)
                        await Task.Run(() => File.Copy(f, Path.Combine(_fiveM.BloodFxPath, Path.GetFileName(f)), overwrite: true));
                    Done($"{bloodFiles.Length} fichier(s) BloodFX installé(s).");
                    RefreshInstalledPacksCore();
                });
                break;
            case "killfx":
                if (!CheckFiveMClosed()) return;
                var killFiles = paths.Where(File.Exists).ToArray();
                if (killFiles.Length == 0) return;
                await RunBusyAsync("Installation KillFX…", async () =>
                {
                    Directory.CreateDirectory(_fiveM.KillFxPath);
                    foreach (var f in killFiles)
                        await Task.Run(() => File.Copy(f, Path.Combine(_fiveM.KillFxPath, Path.GetFileName(f)), overwrite: true));
                    Done($"{killFiles.Length} fichier(s) KillFX installé(s).");
                    RefreshInstalledPacksCore();
                });
                break;
            case "reshade" when File.Exists(first) && System.IO.Path.GetExtension(first).Equals(".ini", StringComparison.OrdinalIgnoreCase):
                await RunBusyAsync("Installation du preset ReShade…", async () =>
                {
                    Directory.CreateDirectory(_fiveM.PluginsPath);
                    string dest = Path.Combine(_fiveM.PluginsPath, Path.GetFileName(first));
                    await Task.Run(() => File.Copy(first, dest, overwrite: true));
                    Done($"Preset ReShade installé → {Path.GetFileName(dest)}");
                    RefreshInstalledPacksCore();
                });
                break;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────
    private bool CheckFiveMClosed()
    {
        if (!_fiveM.IsFiveMRunning()) return true;
        var result = System.Windows.MessageBox.Show(
            "FiveM est en cours d'exécution.\nVoulez-vous le fermer automatiquement ?",
            "EKIPPP – FiveM actif",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("FiveM"))
                    proc.Kill();
                System.Threading.Thread.Sleep(600);
                AddLog("FiveM fermé automatiquement.", LogType.Info);
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"Impossible de fermer FiveM : {ex.Message}", LogType.Error);
            }
        }
        AddLog("Action annulée : FiveM est en cours d'exécution.", LogType.Warning);
        return false;
    }

    private bool CheckGtaVClosed()
    {
        var gtaProcs = System.Diagnostics.Process.GetProcessesByName("GTA5")
            .Concat(System.Diagnostics.Process.GetProcessesByName("GTAVLauncher"))
            .Concat(System.Diagnostics.Process.GetProcessesByName("PlayGTAV"))
            .ToArray();
        if (gtaProcs.Length == 0) return true;
        var result = System.Windows.MessageBox.Show(
            "GTA V est en cours d'exécution.\nVoulez-vous le fermer automatiquement ?",
            "EKIPPP – GTA V actif",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                foreach (var proc in gtaProcs)
                    proc.Kill();
                System.Threading.Thread.Sleep(800);
                AddLog("GTA V fermé automatiquement.", LogType.Info);
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"Impossible de fermer GTA V : {ex.Message}", LogType.Error);
            }
        }
        AddLog("Action annulée : GTA V est en cours d'exécution.", LogType.Warning);
        return false;
    }

    private void UpdateDaysUsed()
    {
        if (string.IsNullOrEmpty(_settings.Current.FirstUsedDate))
        {
            _settings.Current.FirstUsedDate = DateTime.Today.ToString("yyyy-MM-dd");
            _settings.Save();
        }
        if (DateTime.TryParse(_settings.Current.FirstUsedDate, out var firstDate))
        {
            int days = (int)(DateTime.Today - firstDate).TotalDays + 1;
            _settings.Current.DaysUsed = days;
            DaysUsedDisplay = days == 1 ? "1 jour" : $"{days} jours";
        }
    }

    private bool Confirm(string msg)
        => System.Windows.MessageBox.Show(msg, "EKIPPP – Confirmation",
               System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
           == System.Windows.MessageBoxResult.Yes;

    private void Done(string msg)
    {
        AddLog(msg, LogType.Success);
        ShowToast?.Invoke("EKIPPP", msg);
    }

    private IProgress<string> Progress(string? _)
        => new Progress<string>(msg => { StatusMessage = msg; AddLog(msg, LogType.Info); });

    private IProgress<(string msg, double pct)> ProgressPct()
        => new Progress<(string msg, double pct)>(p => { StatusMessage = p.msg; ProgressValue = p.pct; AddLog(p.msg, LogType.Info); });

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        IsBusy = true; StatusMessage = status; ProgressValue = 0;
        try   { await action(); ProgressValue = 100; StatusMessage = "Terminé"; }
        catch (Exception ex) { AddLog($"Erreur : {ex.Message}", LogType.Error); StatusMessage = "Erreur"; System.Windows.MessageBox.Show(ex.Message, "EKIPPP – Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    private void AddLog(string message, LogType type)
    {
        var entry = new ActivityLogEntry { Message = message, Type = type };
        System.Windows.Application.Current?.Dispatcher.Invoke(() => Logs.Insert(0, entry));
    }

    // ── Mise à jour ────────────────────────────────────────────────────
    private async Task CheckForUpdatesAsync()
    {
        if (VersionCheckUrl.StartsWith("REMPLACER")) return;
        var info = await UpdateService.CheckAsync(VersionCheckUrl);
        if (info is null) return;
        if (!System.Version.TryParse(info.Version, out var remote) ||
            !System.Version.TryParse(CurrentVersion, out var current) ||
            remote <= current) return;

        UpdateAvailable = true;
        UpdateVersion   = info.Version;
        UpdateChangelog = info.Changelog;
        UpdateUrl       = info.DownloadUrl;
        AddLog($"Mise à jour v{info.Version} disponible !", LogType.Success);
    }

    [RelayCommand]
    private void OpenUpdateUrl() =>
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(UpdateUrl) { UseShellExecute = true });

    [RelayCommand]
    private void DismissUpdate() => UpdateAvailable = false;

}
