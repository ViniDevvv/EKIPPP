namespace EKIPPP.Models;

public class FiveMStats
{
    public long   CacheSizeBytes { get; set; }
    public int    LogFileCount   { get; set; }
    public int    CrashFileCount { get; set; }
    public long   ModsSizeBytes  { get; set; }
    public int    ModsFileCount  { get; set; }
    public string FiveMPath      { get; set; } = string.Empty;
    public bool   IsInstalled    { get; set; }
    public string FiveMVersion   { get; set; } = "–";
}
