using System.Runtime.InteropServices;

namespace VOCALOIDPatcher.Utils;

public static class KeyState
{
    #pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
}