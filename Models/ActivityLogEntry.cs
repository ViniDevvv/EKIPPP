namespace EKIPPP.Models;

public enum LogType { Info, Success, Error, Warning }

public class ActivityLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
    public LogType Type { get; init; } = LogType.Info;

    public string TimeDisplay => Timestamp.ToString("HH:mm:ss");

    public string Icon => Type switch
    {
        LogType.Success => "✓",
        LogType.Error   => "✗",
        LogType.Warning => "⚠",
        _               => "•"
    };
}
