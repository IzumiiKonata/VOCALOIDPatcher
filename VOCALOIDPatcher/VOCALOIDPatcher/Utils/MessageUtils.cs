using System.Runtime.InteropServices;

namespace VOCALOIDPatcher.Utils;

using System.Runtime.CompilerServices;

public static class MessageUtils
{
    #pragma warning disable SYSLIB1054
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public static void ShowMessageBox(string msg, string title = "VOCALOID Patcher")
    {
        MessageBox(IntPtr.Zero, msg, title, 0x00001000);
    }
    
    public static void Dbg(
        string message, 
        string title = "Debug",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        string fileName = System.IO.Path.GetFileName(file);
        Console.WriteLine($"[{fileName}:{line}] [{title}] {message}");
    }

    public static void ShowErrorMessage(string message, string title = "VOCALOID Patcher Error")
    {
        ShowMessageBox(message, title);
    }

    public static void ShowErrorMessage(
        string message,
        Exception e,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        string fileName = System.IO.Path.GetFileName(file);
        Console.WriteLine($"[{fileName}:{line}] {message}");
        
        ShowErrorMessage(message + Environment.NewLine + e.Message + Environment.NewLine + e.StackTrace);
    }
}