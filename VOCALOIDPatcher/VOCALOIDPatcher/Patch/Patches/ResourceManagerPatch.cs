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
    public override Type[] ArgumentTypes => [ typeof(string), typeof(CultureInfo) ];

    public static bool Skip = false;

    [HarmonyPrefix]
    static bool Prefix(object __instance, string name, CultureInfo? culture, ref string __result)
    {
        if (Skip)
            return true;
        
        if (string.IsNullOrEmpty(name))
            return true;

        var translated = TranslationManager.Get(name);

        if (string.IsNullOrEmpty(translated))
            return true;
        
        __result = translated;
        return false;
    }

    
    [HarmonyReversePatch]
    public static string? GetString(object instance, string name, CultureInfo? culture)
    {
        throw new NotImplementedException("Stub");
    }
}