using System.Text;

namespace VOCALOIDPatcher.Utils;

using System;
using System.Runtime.InteropServices;

public static class ConsoleHelper
{
    #pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    public static void InitConsole()
    {
        AllocConsole();
        Console.OutputEncoding = new UTF8Encoding();
    }
}