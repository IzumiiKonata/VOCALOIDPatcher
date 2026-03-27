using System.Globalization;
using System.Resources;
using HarmonyLib;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Patch.Patches;

public class ResourceManagerPatch : PatchBase
{
    public override string PatchName => "ResourceManagerPatch";
    public override Type TargetClass => typeof(ResourceManager);
    public override string TargetMethodName => "GetString";
    public override Type[] ArgumentTypes => [ typeof(string), typeof(CultureInfo) ];

    public static bool Skip = false;

    public static Dictionary<string, string> ReversedMap = new();
    
    [HarmonyPrefix]
    static bool Prefix(object __instance, string name, CultureInfo? culture, ref string __result)
    {

        if (Skip)
            return true;
        
        if (string.IsNullOrEmpty(name))
            return true;

        var translated = TranslationManager.Get(name);

        if (!string.IsNullOrEmpty(translated))
        {
            // var s = GetString(__instance, name, culture);
            // if (s != null)
                // ReversedMap[translated] = s;
            __result = translated;
            return false;
        }

        return true;
    }

    
    [HarmonyReversePatch]
    public static string? GetString(object instance, string name, CultureInfo? culture)
    {
        throw new NotImplementedException("Stub");
    }
}