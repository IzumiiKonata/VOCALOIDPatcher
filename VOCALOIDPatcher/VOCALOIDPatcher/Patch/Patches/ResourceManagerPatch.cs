using System.Globalization;
using System.Resources;
using HarmonyLib;
using VOCALOIDPatcher.Translation;

namespace VOCALOIDPatcher.Patch.Patches;

public class ResourceManagerPatch : PatchBase
{
    public override string PatchName => "ResourceManagerPatch";
    public override Type TargetClass => typeof(ResourceManager);
    public override string TargetMethodName => "GetString";

    public override Type[]? ArgumentTypes => [ typeof(string), typeof(CultureInfo) ];

    public static bool Skip = false;
    
    [HarmonyPrefix]
    static bool Prefix(string name, CultureInfo culture, ref string __result)
    {

        if (Skip)
            return true;
        
        if (string.IsNullOrEmpty(name))
            return true;

        var translated = TranslationManager.Get(name);

        if (!string.IsNullOrEmpty(translated))
        {
            __result = translated;
            return false;
        }

        return true;
    }
}