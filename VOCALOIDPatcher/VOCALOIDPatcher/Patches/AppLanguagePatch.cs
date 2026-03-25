using System.Globalization;
using HarmonyLib;
using Yamaha.VOCALOID;

namespace Microsoft.Xaml.Behaviors.VOCALOIDPatcher.Patches;

[HarmonyPatch(typeof(App), "SetupAppLanguage")]
public class AppLanguagePatch
{
    static void Postfix()
    {
        CultureInfo.CurrentCulture = new CultureInfo("zh-Hans");
        CultureInfo.CurrentUICulture = new CultureInfo("zh-Hans");
    }
}