using System;
using HarmonyLib;

namespace VOCALOIDPatcher.Patch.Patches;

public static class VstPluginPatch
{

    public static void ApplyPatches(Harmony harmony)
    {
        new VstPluginControllerSetActivePatch().Apply(harmony);
    }

    class VstPluginControllerSetActivePatch : PatchBase
    {
        public override string PatchName => "VSTPluginControllerSetActivePatch";
        public override Type TargetClass => AccessTools.TypeByName("Yamaha.VOCALOID.VST.VSTPluginController");
        public override string TargetMethodName => "ShowMainWindow";

        private static bool Triggered = false;

        [HarmonyPrefix]
        static bool Prefix(object __instance)
        {
            if (Triggered)
                return true;
            
            Triggered = true;
            
            Patcher.PostInject();
            return true;
        }
        
    }
    
}