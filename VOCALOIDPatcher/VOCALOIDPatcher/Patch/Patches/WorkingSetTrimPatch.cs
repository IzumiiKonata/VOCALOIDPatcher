using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class WorkingSetTrimPatch : PatchBase
{
    public override string PatchName        => "WorkingSetTrimPatch";
    public override Type   TargetClass      => typeof(Sequence);
    public override string TargetMethodName => "Load";

    public override Type[] ArgumentTypes => new[] { typeof(string) };

    [HarmonyPostfix]
    private static void Postfix(object __result)
    {
        if (!Settings.TrimWorkingSet)
            return;

        if (__result?.ToString() == "Success")
            WorkingSetTrimmer.ScheduleTrim("工程载入完成");
    }
}

public class RenderCompleteTrimPatch : PatchBase
{
    public override string PatchName        => "RenderCompleteTrimPatch";
    public override Type   TargetClass      => typeof(AudioPlayer);
    public override string TargetMethodName => "OnRendererCompleted";

    public override Type[] ArgumentTypes => new[] { typeof(object), typeof(RendererObserverCompleteEventArgs) };

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (Settings.TrimWorkingSet)
            WorkingSetTrimmer.ScheduleTrim("乐段渲染完成");
    }
}
