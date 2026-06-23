namespace EKIPPP.Helpers;

public static class FileSizeHelper
{
    private static readonly string[] Units = { "o", "Ko", "Mo", "Go", "To" };

    public static string Format(long bytes)
    {
        if (bytes == 0) return "0 o";
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {Units[unit]}";
    }

    public static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return new DirectoryInfo(path)
            .GetFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}
