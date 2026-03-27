using System.Globalization;
using HarmonyLib;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

public class AppLanguagePatch : PatchBase
{
    public override string PatchName        => "AppLanguagePatch";
    public override Type   TargetClass      => typeof(App);
    public override string TargetMethodName => "SetupAppLanguage";

    [HarmonyPostfix]
    static void Postfix()
    {
        CultureInfo.CurrentCulture = new CultureInfo("zh-Hans");
        CultureInfo.CurrentUICulture = new CultureInfo("zh-Hans");
    }
}