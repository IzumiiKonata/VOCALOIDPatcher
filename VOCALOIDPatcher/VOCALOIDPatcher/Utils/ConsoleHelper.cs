using System.Text;

namespace VOCALOIDPatcher.Utils;

using System;
using System.Runtime.InteropServices;

public static class ConsoleHelper
{
    private const int AttachParentProcess = -1;
    
    #pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
    
    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;

    public static void InitConsole()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            int error = Marshal.GetLastWin32Error();

            if (error != 5)
            {
                AllocConsole();
            }
        }

        if (!Patcher.DebugMode)
        {
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
        }

        Console.OutputEncoding = Encoding.UTF8;
    }
}