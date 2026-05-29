using System;
using HarmonyLib;

namespace VOCALOIDPatcher.Patch.Patches;

public static class VstPluginPatch
{
    [CLSCompliant(false)]
    public static void ApplyPatches(Harmony harmony)
    {
        new VstPluginControllerSetActivePatch().Apply(harmony);
    }

    private class VstPluginControllerSetActivePatch : PatchBase
    {
        private static bool Triggered;
        public override string PatchName => "VSTPluginControllerSetActivePatch";
        public override Type TargetClass => AccessTools.TypeByName("Yamaha.VOCALOID.VST.VSTPluginController");
        public override string TargetMethodName => "ShowMainWindow";

        [HarmonyPrefix]
        private static bool Prefix(object __instance)
        {
            if (Triggered)
                return true;

            Triggered = true;

            Patcher.PostInject();
            return true;
        }
    }
}
