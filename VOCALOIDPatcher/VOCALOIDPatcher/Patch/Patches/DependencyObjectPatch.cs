using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

public class DependencyObjectPatch : PatchBase
{
    public override string PatchName => "DependencyObjectPatch";
    public override Type TargetClass => typeof(DependencyObject);
    public override string TargetMethodName => "SetValue";
    public override Type[] ArgumentTypes => [ typeof(DependencyProperty), typeof(object) ];
    
    [HarmonyPrefix]
    static void Prefix(object __instance, DependencyProperty dp, ref object value)
    {
        if (__instance is PushButton && dp.Name == "NormalIcon" && value is Viewbox vb)
        {
            MenuItemsTranslationPatch.TranslateTextBox = true;
            MenuItemsTranslationPatch.TranslateElement(vb);
            MenuItemsTranslationPatch.TranslateTextBox = false;
        }
    }
}