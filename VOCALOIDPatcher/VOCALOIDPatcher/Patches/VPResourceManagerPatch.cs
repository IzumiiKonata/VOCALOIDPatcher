using System.Globalization;
using System.Resources;
using HarmonyLib;
using VOCALOIDPatcher.Translation;

namespace VOCALOIDPatcher.Patches;

[HarmonyPatch(typeof(ResourceManager), "GetString", typeof(string), typeof(CultureInfo))]
public class VPResourceManagerPatch
{

    public static bool Skip = false;
    
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