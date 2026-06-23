using System.Diagnostics;

namespace EKIPPP.Helpers;

public static class ProcessHelper
{
    public static bool IsFiveMRunning()
    {
        return Process.GetProcessesByName("FiveM").Length > 0
            || Process.GetProcessesByName("FiveM_b").Length > 0
            || Process.GetProcessesByName("fivem").Length > 0;
    }
}
