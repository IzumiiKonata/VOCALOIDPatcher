using System.Diagnostics;

namespace VOCALOIDPatcher.Utils;

public static class BrowseUtils
{
    public static void Browse(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}