using System.Runtime.InteropServices;

namespace VOCALOIDPatcher.Utils;

using System.Runtime.CompilerServices;

public class MessageUtils
{
    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static void ShowMessageBox(string msg, string title)
    {
        MessageBox(IntPtr.Zero, msg, title, 0x00001000);
    }
    
    public static void ShowDbgMessage(
        string message, 
        string title = "Patcher Debug Message",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        if (!Patcher.DebugMode)
        {
            return;
        }
        
        var fileName = System.IO.Path.GetFileName(file);
        ShowMessageBox($"{fileName}:{line}\n{message}", title);
    }
    
    public static void ShowErrorMessage(string message, string title = "VOCALOIDPatcher Error")
    {
        ShowMessageBox(message, title);
    }
}