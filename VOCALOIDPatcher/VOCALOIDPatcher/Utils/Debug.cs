using System;
using System.Runtime.InteropServices;

namespace VOCALOIDPatcher.Utils;

using System.Runtime.CompilerServices;

public static class Debug
{
    #pragma warning disable SYSLIB1054
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public static void ShowMessageBox(string msg, string title = "VOCALOID Patcher")
    {
        MessageBox(IntPtr.Zero, msg, title, 0x00001000);
    }
    
    public static void Print(
        string message, 
        string? level = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        string fileName = System.IO.Path.GetFileName(file);
        string lvl = level is null ? "" : $" [{level}]";
        Console.WriteLine($@"[{fileName}:{line}]{lvl} {message}");
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
        Console.WriteLine($@"[{fileName}:{line}] {message}");
        
        ShowErrorMessage(message + Environment.NewLine + e.Message + Environment.NewLine + e.StackTrace);
    }
}