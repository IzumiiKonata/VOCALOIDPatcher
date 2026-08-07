using System;
using HarmonyLib;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

public class UnhandledExceptionDialogPatch : PatchBase
{
    public override string PatchName => "UnhandledExceptionDialogPatch";
    public override Type TargetClass => typeof(App);
    public override string TargetMethodName => "OnUnhandledException";
    public override Type[] ArgumentTypes => new[] { typeof(object), typeof(UnhandledExceptionEventArgs) };

    [HarmonyPrefix]
    private static bool Prefix(UnhandledExceptionEventArgs __1)
    {
        var details = __1.ExceptionObject is Exception exception
            ? exception.ToString()
            : __1.ExceptionObject?.ToString() ?? "Unknown unhandled exception.";

        Debug.ShowErrorMessage(details, "UnhandledException");
        return false;
    }
}
