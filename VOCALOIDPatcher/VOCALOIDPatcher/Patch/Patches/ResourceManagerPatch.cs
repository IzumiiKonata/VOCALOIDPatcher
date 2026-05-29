using System;
using System.Globalization;
using System.Resources;
using HarmonyLib;
using VOCALOIDPatcher.Translation;

namespace VOCALOIDPatcher.Patch.Patches;

public class ResourceManagerPatch : PatchBase
{
    public override string PatchName => "ResourceManagerPatch";
    public override Type TargetClass => typeof(ResourceManager);
    public override string TargetMethodName => nameof(ResourceManager.GetString);
    public override Type[] ArgumentTypes => new[] { typeof(string), typeof(CultureInfo) };

    [HarmonyPrefix]
    private static bool Prefix(object __instance, string name, CultureInfo? culture, ref string __result)
    {
        if (string.IsNullOrEmpty(name))
            return true;

        var translated = TranslationManager.Get(name);

        if (string.IsNullOrEmpty(translated))
            return true;

        __result = translated;
        return false;
    }
}
