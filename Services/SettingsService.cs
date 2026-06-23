using System.Text.Json;
using Microsoft.Win32;

namespace EKIPPP.Services;

public class AppSettings
{
    public bool   LaunchOnStartup    { get; set; } = false;
    public bool   AutoCleanOnLaunch  { get; set; } = false;
    public bool   FirstRun           { get; set; } = true;
    public long   TotalBytesFreed    { get; set; } = 0;
    public int    TotalFilesDeleted  { get; set; } = 0;
    public int    CleanCount         { get; set; } = 0;
    public int    DaysUsed           { get; set; } = 0;
    public string FirstUsedDate      { get; set; } = string.Empty;
    public string       GtaVPath            { get; set; } = string.Empty;
    public List<string> InstalledSoundFiles { get; set; } = [];
}

public class SettingsService
{
    private static readonly string SettingsDir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EKIPPP");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly string EkipppExePath =
        Environment.ProcessPath
        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EKIPPP.exe");

    public AppSettings Current { get; private set; } = new AppSettings();

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                Current = new AppSettings();
                return;
            }
            var json = File.ReadAllText(SettingsFile);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { /* silent */ }
    }

    public void SetWindowsStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            if (enable)
                key.SetValue("EKIPPP", $"\"{EkipppExePath}\"");
            else
                key.DeleteValue("EKIPPP", throwOnMissingValue: false);
        }
        catch { /* silent */ }
    }
}
