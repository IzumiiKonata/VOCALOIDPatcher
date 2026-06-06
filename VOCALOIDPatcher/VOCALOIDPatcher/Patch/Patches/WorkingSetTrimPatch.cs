using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

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
            WorkingSetTrimmer.ScheduleAfterLoadTrim();
    }
}
